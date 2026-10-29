using System;
using System.Globalization;

namespace DvMod.HeadsUpDisplay
{
    public enum RouteSpeedState { Unknown, WithinLimit, SlightlyOver, OverLimit }

    /// <summary>Native speed-board values are tens of km/h. Never parse a formatted description.</summary>
    public static class RouteSpeed
    {
        public static void ReadNative(string type, string raw, out double? primary, out double? secondary)
        {
            primary = secondary = null;
            var parts = raw.Split('/');
            if (parts.Length == 1 && TryValue(parts[0], out var value)) primary = value;
            else if (type == "SpeedLimitDual" && parts.Length == 2
                && TryValue(parts[0], out var first) && TryValue(parts[1], out var second))
            { primary = first; secondary = second; }
        }

        private static bool TryValue(string raw, out double kph)
        {
            kph = 0;
            if ((!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                && !double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                || !Finite(value) || value <= 0 || !Finite(value * 10)) return false;
            kph = value * 10;
            return true;
        }

        public static RouteSpeedState Compare(double? speedKph, double? limitKph)
        {
            if (!speedKph.HasValue || !limitKph.HasValue || !Finite(speedKph.Value)
                || !Finite(limitKph.Value) || speedKph.Value < 0 || limitKph.Value <= 0) return RouteSpeedState.Unknown;
            var excess = speedKph.Value - limitKph.Value;
            return excess <= 0 ? RouteSpeedState.WithinLimit : excess <= 5 ? RouteSpeedState.SlightlyOver : RouteSpeedState.OverLimit;
        }

        public static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        public static string ColorHex(RouteSpeedState state)
        {
            switch (state)
            {
                case RouteSpeedState.WithinLimit: return "#59FF73";
                case RouteSpeedState.SlightlyOver: return "#FFD626";
                case RouteSpeedState.OverLimit: return "#FF4040";
                default: return "#B3BFCC";
            }
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
