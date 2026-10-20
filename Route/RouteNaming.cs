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
                var name = Trim(fallback);
                return name.Length > 0 ? name : "Unnamed track";
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
            SplitTrackPart(trackPart, out var orderNumber, out var trackType);
            return TrackName(yardId, subYardPart, orderNumber, trackType, stationName, fallback);
        }

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
