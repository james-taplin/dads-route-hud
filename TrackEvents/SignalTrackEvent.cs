namespace DvMod.HeadsUpDisplay
{
    public sealed class SignalTrackEvent : TrackEvent
    {
        public readonly string Name;
        public readonly string ColorHex;
        public readonly string Aspect;
        public readonly string Reservation;
        public readonly string SourceId;
        public readonly string Freshness;
        public readonly bool FacingTravel;

        public SignalTrackEvent(double distance, string name, string colorHex, string aspect, string reservation, string sourceId = "", string freshness = "", bool facingTravel = true) : base(distance)
        {
            Name = name;
            ColorHex = colorHex;
            Aspect = aspect;
            Reservation = reservation;
            SourceId = sourceId;
            Freshness = freshness;
            FacingTravel = facingTravel;
        }

        public override bool Direction => FacingTravel;
        public override TrackEvent WithSpan(double distance, bool direction)
            => new SignalTrackEvent(distance, Name, ColorHex, Aspect, Reservation, SourceId, Freshness, direction);
    }
}
