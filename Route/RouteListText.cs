using System;
using System.Collections.Generic;
using System.Linq;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>The driving list uses readable names; raw source evidence stays in the inspector.</summary>
    public static class RouteListText
    {
        public static string Description(RouteEvent item)
        {
            var label = item.Label;
            if (item.Kind == RouteEventKind.Speed) label = Prefix(label, "Speed limit");
            else if (item.Kind == RouteEventKind.Grade) label = Prefix(label, "Grade");
            else if (item.Kind == RouteEventKind.Signal) label = Prefix(label, "Signal");
            var text = Plain(label);
            if (item.Kind == RouteEventKind.Signal)
            {
                if (!string.IsNullOrWhiteSpace(item.Aspect)) text += " · " + Plain(item.Aspect);
            }
            if (item.Kind != RouteEventKind.Junction || (!label.Contains("←") && !label.Contains("→") && !label.Contains("↑")))
            {
                if (item.Marker == RouteTurn.LeftMarker) text = "← " + text;
                else if (item.Marker == RouteTurn.RightMarker) text = "→ " + text;
                else if (item.Marker == RouteTurn.AheadMarker) text = "↑ " + text;
            }
            text = "<color=" + KindColor(item) + "><b>" + text + "</b></color>";
            // For signs Fact records provenance, raw text and prefab details, not a driving fact.
            if ((item.Kind == RouteEventKind.Signal || item.Kind == RouteEventKind.Track)
                && !string.IsNullOrWhiteSpace(item.Fact))
                text += "\n<color=#FFBD59>" + Plain(item.Fact) + "</color>";
            if (!item.AppliesToTravel) text += "\n<color=#BFC6CE>Opposite direction</color>";
            return text;
        }

        public static string Description(RouteEvent item, double? speedKph)
        {
            if (item.Kind != RouteEventKind.Speed || !item.SpeedLimitKph.HasValue || !item.AppliesToTravel)
                return Description(item);
            // Preserve the exact measured glyphs; only colour tags change with speed.
            var text = Plain(Prefix(item.Label, "Speed limit"));
            var primary = RouteSpeed.Number(item.SpeedLimitKph.Value);
            var secondary = item.AlternateSpeedLimitKph.HasValue ? "/" + RouteSpeed.Number(item.AlternateSpeedLimitKph.Value) : "";
            var reading = primary + secondary;
            var at = text.IndexOf(reading, StringComparison.Ordinal);
            if (at < 0) return Description(item);
            var coloured = "<color=" + RouteSpeed.ColorHex(RouteSpeed.Compare(speedKph, item.SpeedLimitKph)) + ">" + primary + "</color>";
            if (secondary.Length > 0)
                coloured += "/<color=" + RouteSpeed.ColorHex(RouteSpeed.Compare(speedKph, item.AlternateSpeedLimitKph))
                    + ">" + secondary.Substring(1) + "</color>";
            return "<b>" + text.Substring(0, at) + coloured + text.Substring(at + reading.Length) + "</b>";
        }

        /// <summary>Moving distances and inspector-only provenance do not change a wrapped description.</summary>
        public static bool SameDescription(RouteEvent first, RouteEvent second) => first.Kind == second.Kind
            && first.Label == second.Label && first.Marker == second.Marker
            && first.AppliesToTravel == second.AppliesToTravel
            && first.SpeedLimitKph == second.SpeedLimitKph && first.AlternateSpeedLimitKph == second.AlternateSpeedLimitKph
            && (first.Kind != RouteEventKind.Signal || (first.Aspect == second.Aspect && first.ColorHex == second.ColorHex))
            && ((first.Kind != RouteEventKind.Signal && first.Kind != RouteEventKind.Track) || first.Fact == second.Fact);

        /// <summary>A gap between observations is not evidence that the intervening line is clear.</summary>
        public static string CoverageWarning(string coverage)
        {
            return coverage.IndexOf("partial", StringComparison.OrdinalIgnoreCase) >= 0
                || coverage.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Sign coverage incomplete · speed limits or grades may be missing" : "";
        }

        public static string SignalCoverageWarning(string coverage)
        {
            if (coverage.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0
                || coverage.IndexOf("not sampled", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Signals: unavailable (see UMM)";
            if (coverage.Length == 0 || coverage.IndexOf("pending", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Signals: awaiting sample";
            return coverage.IndexOf("partial", StringComparison.OrdinalIgnoreCase) >= 0
                || coverage.IndexOf("uncertain", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Signals: partial coverage (see UMM)" : "";
        }

        public static string Termination(RouteWalkTermination termination)
        {
            switch (termination)
            {
                case RouteWalkTermination.HorizonReached: return "end of captured range";
                case RouteWalkTermination.LoopClosed: return "route loops here";
                case RouteWalkTermination.EndOfTrack: return "end of selected route";
                case RouteWalkTermination.InProgress: return "collecting route";
                case RouteWalkTermination.MissingGeometry: return "track geometry unavailable";
                case RouteWalkTermination.InvalidGeometry: return "track geometry invalid";
                case RouteWalkTermination.InvalidStart: return "no usable starting track";
                case RouteWalkTermination.InvalidDistance: return "route range invalid";
                case RouteWalkTermination.NoProgress: return "route could not advance";
                default: return "route collection interrupted";
            }
        }

        private static string Prefix(string value, string prefix) => value.StartsWith(prefix,
            StringComparison.OrdinalIgnoreCase) ? value : prefix + (value.Length > 0 ? " " + value : "");

        private static string KindColor(RouteEvent item)
        {
            switch (item.Kind)
            {
                case RouteEventKind.Track: return "#7EDDE8";
                case RouteEventKind.Speed: return "#FFD166";
                case RouteEventKind.Grade: return "#9DDB96";
                case RouteEventKind.Junction: return "#D7B1FF";
                case RouteEventKind.Signal: return AspectColor(item.ColorHex);
                default: return "#E0E5EB";
            }
        }

        private static string Plain(string value) => value.Replace('<', '‹').Replace('>', '›')
            .Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');

        private static string AspectColor(string value)
        {
            var hex = value.Trim().TrimStart('#');
            return (hex.Length == 6 || hex.Length == 8) && hex.All(c =>
                (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))
                ? "#" + hex : "#C0C0C0";
        }
    }

    /// <summary>Offsets for every wrapped row, with a binary search into the scroll viewport.</summary>
    public sealed class RouteListLayout
    {
        private readonly float[] offsets;
        public int Count => offsets.Length - 1;
        public float Height => offsets[offsets.Length - 1];

        public RouteListLayout(IReadOnlyList<float> rowHeights)
        {
            offsets = new float[rowHeights.Count + 1];
            for (var index = 0; index < rowHeights.Count; index++)
            {
                var height = rowHeights[index];
                if (float.IsNaN(height) || float.IsInfinity(height) || height <= 0)
                    throw new ArgumentOutOfRangeException(nameof(rowHeights));
                offsets[index + 1] = offsets[index] + height;
            }
        }

        public float Top(int row) => offsets[row];

        /// <summary>Returns all intersecting rows; nothing is removed from the scroll extent.</summary>
        public (int first, int count) VisibleRows(float top, float bottom)
        {
            if (Count == 0 || bottom < 0 || top > Height || bottom < top) return (0, 0);
            var lo = 0;
            var hi = Count;
            while (lo < hi)
            {
                var middle = lo + (hi - lo) / 2;
                if (offsets[middle + 1] < top) lo = middle + 1;
                else hi = middle;
            }
            var first = lo;
            hi = Count;
            while (lo < hi)
            {
                var middle = lo + (hi - lo) / 2;
                if (offsets[middle] <= bottom) lo = middle + 1;
                else hi = middle;
            }
            return (first, lo - first);
        }
    }
}
