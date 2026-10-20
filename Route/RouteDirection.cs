using System;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>
    /// Decides which way a vehicle is travelling, for the purpose of choosing the route ahead.
    /// The reverser leads: it states the direction the driver has selected, so the road shown is
    /// the one about to be taken, including before the train moves and while it is still rolling
    /// the other way after a stop. Pure, and deliberately sticky, because changing this answer
    /// retires an entire capture.
    /// </summary>
    public static class RouteDirection
    {
        /// <summary>
        /// How far the reverser must sit from neutral to state a direction, in the control's own
        /// units, where full gear is 0.5 either side of neutral. A steam reverser's distance from
        /// neutral is its cutoff, and normal running cutoffs are well beyond this, so only
        /// mid-gear and lever float fall inside it.
        /// </summary>
        public const double NeutralBand = 0.05;

        /// <summary>With the reverser saying nothing, motion above this speed decides instead.</summary>
        public const double MotionSpeed = 0.5;

        /// <summary>
        /// The reverser decides whenever it is away from neutral. At or near neutral the lever
        /// states nothing, so clear motion decides; failing that the previous answer stands, and
        /// with nothing at all to go on the vehicle reads as forward.
        /// </summary>
        public static bool Decide(double speed, double? reverserFromNeutral, bool decided, bool previous)
        {
            if (reverserFromNeutral.HasValue && Finite(reverserFromNeutral.Value)
                && Math.Abs(reverserFromNeutral.Value) > NeutralBand)
                return reverserFromNeutral.Value > 0;
            if (Finite(speed) && Math.Abs(speed) > MotionSpeed) return speed > 0;
            return decided ? previous : true;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
