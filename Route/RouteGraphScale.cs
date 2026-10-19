using System;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>One distance scale shared by profile, markers, axis and coverage shading.</summary>
    public static class RouteGraphScale
    {
        public static double End(double requested, double reached, bool fitAvailable)
        {
            if (double.IsNaN(requested) || double.IsInfinity(requested) || requested <= 0) requested = 1;
            return fitAvailable && !double.IsNaN(reached) && !double.IsInfinity(reached) && reached > 0
                ? Math.Min(requested, reached) : requested;
        }
        public static double Fraction(double distance, double start, double end)
        {
            if (double.IsNaN(distance) || double.IsInfinity(distance) || double.IsNaN(start) || double.IsInfinity(start)
                || double.IsNaN(end) || double.IsInfinity(end) || end <= start) return 0;
            return Math.Max(0, Math.Min(1, (distance - start) / (end - start)));
        }
    }
}
