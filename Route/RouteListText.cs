using System;
using System.Linq;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>The compact driving list; source evidence remains in the complete inspector.</summary>
    public static class RouteListText
    {
        public static string Description(RouteEvent item)
        {
            var label = item.Kind == RouteEventKind.Track && !string.IsNullOrWhiteSpace(item.Code)
                && !item.Code.Contains("#") ? item.Code : item.Label;
            var text = Plain(label);
            if (item.Kind == RouteEventKind.Signal)
            {
                text = "<color=" + AspectColor(item.ColorHex) + ">" + text + "</color>";
                if (!string.IsNullOrWhiteSpace(item.Aspect)) text += "  " + Plain(item.Aspect);
            }
            // For signs Fact records provenance, raw text and prefab details, not a driving fact.
            if ((item.Kind == RouteEventKind.Signal || item.Kind == RouteEventKind.Track)
                && !string.IsNullOrWhiteSpace(item.Fact))
                text += "  <color=#FFBD59>[" + Plain(item.Fact) + "]</color>";
            if (item.Marker == RouteTurn.LeftMarker || item.Marker == RouteTurn.RightMarker
                || item.Marker == RouteTurn.AheadMarker) text = item.Marker + "  " + text;
            if (!item.AppliesToTravel) text += "  (opposite direction)";
            return text;
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
}
