using System;
using System.Globalization;
using System.Text;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>
    /// Turns native identifiers into readable text. Pure: the caller resolves a station's
    /// display name from the game and passes it in, so nothing here hardcodes a yard list
    /// that would rot when the game's stations change.
    /// </summary>
    public static class RouteNaming
    {
        /// <summary>One lower-case word per observation kind, for the row's second column.</summary>
        public static string KindWord(RouteEventKind kind)
        {
            switch (kind)
            {
                case RouteEventKind.Track: return "track";
                case RouteEventKind.Junction: return "switch";
                case RouteEventKind.Speed: return "limit";
                case RouteEventKind.Grade: return "grade";
                case RouteEventKind.Signal: return "signal";
                case RouteEventKind.OtherSign: return "sign";
                default: return "item";
            }
        }

        /// <summary>
        /// The native single-letter track types declared by the game's track identifiers.
        /// An unrecognised value is returned as given rather than guessed at.
        /// </summary>
        public static string TrackTypeWord(string? trackType)
        {
            switch ((trackType ?? "").Trim().ToUpperInvariant())
            {
                case "L": return "loading";
                case "S": return "storage";
                case "I": return "inbound";
                case "O": return "outbound";
                case "P": return "parking";
                case "M": return "main line";
                case "LP": return "passenger loading";
                case "SP": return "passenger storage";
                case "": return "";
                default: return (trackType ?? "").Trim();
            }
        }

        /// <summary>
        /// Describes a yard track as "Goods Factory · B yard, track 5, loading". Missing parts are
        /// left out instead of padded. With no identifying part at all the caller's fallback name
        /// is used, and failing that the track is reported as unnamed rather than blank.
        /// </summary>
        public static string TrackName(string? yardId, string? subYardId, string? orderNumber,
            string? trackType, string? stationName, string? fallback)
        {
            var station = Trim(stationName);
            var yard = Trim(yardId);
            var head = station.Length > 0 ? station : yard;
            var detail = new StringBuilder();
            var sub = Trim(subYardId);
            if (sub.Length > 0) Append(detail, sub + " yard");
            var order = Trim(orderNumber);
            if (order.Length > 0) Append(detail, "track " + order);
            // A main line carries no yard or number of its own, so its type becomes the whole name.
            var type = TrackTypeWord(trackType);
            if (type.Length > 0) Append(detail, type);

            if (head.Length == 0 && detail.Length == 0)
            {
                return FallbackTrackName(fallback);
            }
            if (head.Length == 0) return Capitalise(detail.ToString());
            return detail.Length == 0 ? head : head + " · " + detail;
        }

        /// <summary>
        /// Splits the game's combined track part into its order number and type letters:
        /// "5L" is track 5 loading, "12SP" is track 12 passenger storage, "M" is a main line
        /// with no number of its own. Anything unexpected is left whole as the type.
        /// </summary>
        public static void SplitTrackPart(string? trackPart, out string orderNumber, out string trackType)
        {
            var text = Trim(trackPart);
            var digits = 0;
            while (digits < text.Length && char.IsDigit(text[digits])) digits++;
            orderNumber = text.Substring(0, digits);
            trackType = text.Substring(digits);
        }

        /// <summary>Names a track from the identifier parts the game exposes publicly.</summary>
        public static string TrackNameFromParts(string? yardId, string? subYardPart, string? trackPart,
            string? stationName, string? fallback)
        {
            // DV uses literal #Y/#S/#T placeholders outside real yards. A numeric portion
            // alongside those placeholders does not establish a station or a yard track.
            if (Trim(yardId).Contains("#") || Trim(subYardPart).Contains("#") || Trim(trackPart).Contains("#"))
                return FallbackTrackName(fallback);
            SplitTrackPart(trackPart, out var orderNumber, out var trackType);
            return TrackName(yardId, subYardPart, orderNumber, trackType, stationName, fallback);
        }

        /// <summary>Native structural object names are implementation labels, not station names.</summary>
        public static string FallbackTrackName(string? fallback)
        {
            var text = Trim(fallback);
            if (text.StartsWith("[track diverging]", StringComparison.OrdinalIgnoreCase)) return "Diverging track";
            if (text.StartsWith("[track through]", StringComparison.OrdinalIgnoreCase)) return "Through track";
            if (text.StartsWith("[#]", StringComparison.Ordinal)) return "Main line";
            // Remove the provider prefix, retaining custom tokens without guessing their meaning.
            if (text.StartsWith("doubletrack-", StringComparison.OrdinalIgnoreCase))
                return text.Substring("doubletrack-".Length).Replace('-', ' ');
            return text.Length > 0 ? text : "Unnamed track";
        }

        /// <summary>
        /// Names native switch branches by their connected station destinations. Branch numbers
        /// remain the fallback when destinations are not yet established; their order never implies
        /// left or right. Callers supply actual graph results rather than nearby station guesses.
        /// </summary>
        public static string JunctionDestinations(string? stationName, int selectedBranch,
            string[] branchDestinations, bool trailing, bool setForRoute, string? onwardDestination,
            RouteTurnDirection turn)
        {
            var station = Trim(stationName);
            var head = station.Length > 0 ? station + " junction" : "Junction";
            if (trailing)
                return head + " · trailing, " + (setForRoute ? "set for your route" : "set against you")
                    + " · onward towards " + Destination(onwardDestination);
            var text = new StringBuilder(head);
            if (selectedBranch >= 0 && selectedBranch < branchDestinations.Length)
            {
                text.Append(" · set towards ").Append(Destination(branchDestinations[selectedBranch]));
                var direction = RouteTurn.Word(turn);
                if (direction.Length > 0) text.Append(" (").Append(direction).Append(')');
            }
            else text.Append(" · selected branch unavailable");
            for (var branch = 0; branch < branchDestinations.Length; branch++)
            {
                if (branch == selectedBranch) continue;
                text.Append(" · branch ").Append((branch + 1).ToString(CultureInfo.InvariantCulture))
                    .Append(" towards ").Append(Destination(branchDestinations[branch]));
            }
            return text.ToString();
        }

        private static string Destination(string? value) => Trim(value).Length > 0 ? Trim(value) : "destination unknown";

        /// <summary>
        /// Describes a switch as "Goods Factory switch · branch 2 of 3". Branch numbers are
        /// reported only when the game supplied a usable count.
        /// </summary>
        public static string SwitchName(string? stationName, string? fallback, int selectedBranch, int branchCount)
        {
            var station = Trim(stationName);
            var head = (station.Length > 0 ? station + " switch" : Trim(fallback));
            if (head.Length == 0) head = "Switch";
            if (branchCount <= 0 || selectedBranch < 0 || selectedBranch >= branchCount) return head;
            return head + " · branch " + (selectedBranch + 1).ToString(CultureInfo.InvariantCulture)
                + " of " + branchCount.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Describes a switch for the direction it is approached from. A facing switch states the
        /// selected branch and which way it diverges; a trailing switch has no side to report, so
        /// it states whether the switch is set to the track the route is actually on.
        /// </summary>
        public static string SwitchText(string? stationName, string? fallback, int selectedBranch,
            int branchCount, bool trailing, bool setForRoute, RouteTurnDirection turn)
        {
            var text = SwitchName(stationName, fallback, trailing ? -1 : selectedBranch, branchCount);
            if (trailing)
                return text + " · trailing, " + (setForRoute ? "set for your route" : "set against you");
            var word = RouteTurn.Word(turn);
            return word.Length == 0 ? text : text + ", " + word;
        }

        /// <summary>Signed grade with its sense spelled out, so a sign's number needs no decoding.</summary>
        public static string GradeText(double percent)
        {
            if (double.IsNaN(percent) || double.IsInfinity(percent)) return "grade unavailable";
            if (percent == 0) return "level";
            var magnitude = Math.Abs(percent).ToString("0.0", CultureInfo.InvariantCulture);
            return (percent > 0 ? "+" : "-") + magnitude + " % " + (percent > 0 ? "climbing" : "falling");
        }

        /// <summary>
        /// Splits a native PascalCase type name into words, keeping runs of capitals and digits
        /// intact: "SpeedLimitDual" reads as "Speed limit dual", "SignLP" as "Sign LP".
        /// </summary>
        public static string Words(string? value)
        {
            var text = Trim(value);
            if (text.Length == 0) return "";
            var result = new StringBuilder(text.Length + 8);
            for (var index = 0; index < text.Length; index++)
            {
                var current = text[index];
                var starts = index > 0 && char.IsUpper(current)
                    && (!char.IsUpper(text[index - 1])
                        || (index + 1 < text.Length && char.IsLower(text[index + 1])));
                if (starts) result.Append(' ');
                var runOfCapitals = char.IsUpper(current)
                    && ((index + 1 < text.Length && char.IsUpper(text[index + 1]))
                        || (index > 0 && char.IsUpper(text[index - 1])));
                result.Append(index == 0 || runOfCapitals ? current : char.ToLowerInvariant(current));
            }
            return Capitalise(result.ToString());
        }

        private static void Append(StringBuilder target, string value)
        {
            if (target.Length > 0) target.Append(", ");
            target.Append(value);
        }

        private static string Capitalise(string value) => value.Length == 0 ? value
            : char.ToUpperInvariant(value[0]) + value.Substring(1);

        private static string Trim(string? value) => (value ?? "").Trim();
    }
}
