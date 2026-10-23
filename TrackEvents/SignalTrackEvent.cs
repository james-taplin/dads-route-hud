namespace DvMod.HeadsUpDisplay
{
    /// <summary>Optional signal observation projected onto one immutable route capture.</summary>
    public sealed class SignalTrackEvent
    {
        public readonly double span;
        public readonly string Name;
        public readonly string ColorHex;
        public readonly string Aspect;
        public readonly string Reservation;
        public readonly string SourceId;
        public readonly string Freshness;
        public readonly bool FacingTravel;

        public SignalTrackEvent(double distance, string name, string colorHex, string aspect, string reservation, string sourceId = "", string freshness = "", bool facingTravel = true)
        {
            span = distance;
            Name = name;
            ColorHex = colorHex;
            Aspect = aspect;
            Reservation = reservation;
            SourceId = sourceId;
            Freshness = freshness;
            FacingTravel = facingTravel;
        }

    }
}
