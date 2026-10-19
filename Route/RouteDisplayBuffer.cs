namespace DvMod.HeadsUpDisplay
{
    /// <summary>Keeps a completed presentation during a same-route replacement; never retains across invalidation.</summary>
    public sealed class RouteDisplayBuffer<TTrack> where TTrack : notnull
    {
        public RouteSnapshot<TTrack>? Capture { get; private set; }
        public void Clear() => Capture = null;
        public RouteSnapshot<TTrack> Select(RouteSnapshot<TTrack> incoming, bool holdPrevious)
        {
            if (holdPrevious && Capture != null) return Capture;
            Capture = incoming;
            return incoming;
        }
    }
}
