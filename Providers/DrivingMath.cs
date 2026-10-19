using System;
using System.Globalization;

namespace DvMod.HeadsUpDisplay
{
    // Pure conversions shared by telemetry and its boundary tests. SI internally.
    public static class DrivingMath
    {
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
        public static float SignedReverser(float value) => Clamp01(value) * 2f - 1f;
        public static float GaugePressure(float absoluteBar) => Math.Max(0f, absoluteBar - 1f);
        public static float MechanicalPower(float forceNewtons, float speedMetersPerSecond) =>
            Math.Abs(forceNewtons * speedMetersPerSecond);
        public static string Percent(float value) => (Clamp01(value) * 100f).ToString("F0", CultureInfo.InvariantCulture) + " %";
        public static string Reverser(float value)
        {
            var signed = SignedReverser(value);
            if (Math.Abs(signed) < 0.001f) return "N";
            return (signed > 0 ? "F " : "R ") + Percent(Math.Abs(signed));
        }
        public static string Number(float value, int precision, string unit) =>
            value.ToString("F" + precision, CultureInfo.InvariantCulture) + (unit.Length == 0 ? "" : " " + unit);
    }
}
