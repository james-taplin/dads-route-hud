using System;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>
    /// Decides which way a vehicle is travelling, for the purpose of choosing the route ahead.
    /// Pure, and deliberately sticky: changing this answer retires an entire capture, so a
    /// vehicle idling either side of a threshold must not keep changing it.
    /// </summary>
    public static class RouteDirection
    {
        /// <summary>Above this speed the measured direction of travel is decisive.</summary>
        public const double ReversalSpeed = 0.5;

        /// <summary>Below this speed an already decided direction is kept whatever the controls say.</summary>
        public const double HoldSpeed = 0.15;

        /// <summary>
        /// Measured motion wins above the reversal speed. Below the hold speed a direction already
        /// decided is kept. Between the two, a reverser away from neutral decides; otherwise the
        /// previous answer stands, and with nothing at all to go on the vehicle reads as forward.
        /// </summary>
        public static bool Decide(double speed, double? reverserFromNeutral, bool decided, bool previous)
        {
            if (double.IsNaN(speed) || double.IsInfinity(speed)) return decided ? previous : true;
            var magnitude = Math.Abs(speed);
            if (magnitude > ReversalSpeed) return speed > 0;
            if (decided && magnitude < HoldSpeed) return previous;
            if (reverserFromNeutral.HasValue && !double.IsNaN(reverserFromNeutral.Value)
                && Math.Abs(reverserFromNeutral.Value) > 0.001)
                return reverserFromNeutral.Value > 0;
            return decided ? previous : true;
        }
    }
}
