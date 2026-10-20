using System;

namespace DvMod.HeadsUpDisplay
{
    public enum RouteTurnDirection { Unknown, Ahead, Left, Right }

    /// <summary>
    /// Which way a switch takes the route, relative to the direction of approach. Pure: the
    /// caller supplies the two horizontal headings it read from native geometry.
    /// </summary>
    public static class RouteTurn
    {
        /// <summary>Below this angle the divergence is not meaningfully left or right.</summary>
        public const double AheadDegrees = 3;

        public const string LeftMarker = "<<<";
        public const string RightMarker = ">>>";
        public const string AheadMarker = "|||";

        /// <summary>
        /// Classifies the turn from the approach heading to the departure heading in the
        /// horizontal plane. Travelling north and leaving east is a right turn. Headings that
        /// are not usable, or a reversal with no clear side, are reported as unknown rather
        /// than guessed.
        /// </summary>
        public static RouteTurnDirection Classify(double approachX, double approachZ,
            double departX, double departZ, double aheadDegrees = AheadDegrees)
        {
            if (!Finite(approachX) || !Finite(approachZ) || !Finite(departX) || !Finite(departZ))
                return RouteTurnDirection.Unknown;
            var approachLength = Math.Sqrt(approachX * approachX + approachZ * approachZ);
            var departLength = Math.Sqrt(departX * departX + departZ * departZ);
            if (approachLength <= 0 || departLength <= 0) return RouteTurnDirection.Unknown;
            approachX /= approachLength; approachZ /= approachLength;
            departX /= departLength; departZ /= departLength;

            // Right-hand turns give a negative cross product in the game's left-handed
            // horizontal plane; the dot product separates "ahead" from a reversal.
            var cross = approachX * departZ - approachZ * departX;
            var dot = approachX * departX + approachZ * departZ;
            var angle = Math.Atan2(Math.Abs(cross), dot) * 180 / Math.PI;
            if (!Finite(angle)) return RouteTurnDirection.Unknown;
            if (angle <= aheadDegrees) return RouteTurnDirection.Ahead;
            if (cross == 0) return RouteTurnDirection.Unknown;
            return cross < 0 ? RouteTurnDirection.Right : RouteTurnDirection.Left;
        }

        /// <summary>The scannable marker for a turn, or empty where the side is not established.</summary>
        public static string Marker(RouteTurnDirection direction)
        {
            switch (direction)
            {
                case RouteTurnDirection.Left: return LeftMarker;
                case RouteTurnDirection.Right: return RightMarker;
                case RouteTurnDirection.Ahead: return AheadMarker;
                default: return "";
            }
        }

        /// <summary>The turn in words, for the row's description.</summary>
        public static string Word(RouteTurnDirection direction)
        {
            switch (direction)
            {
                case RouteTurnDirection.Left: return "diverging left";
                case RouteTurnDirection.Right: return "diverging right";
                case RouteTurnDirection.Ahead: return "straight ahead";
                default: return "";
            }
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
