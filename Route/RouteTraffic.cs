using System;
using System.Collections.Generic;

namespace DvMod.HeadsUpDisplay
{
    public readonly struct RouteTrafficPoint<TTrack> where TTrack : notnull
    {
        public TTrack Track { get; }
        public double Span { get; }
        public RouteTrafficPoint(TTrack track, double span) { Track = track; Span = span; }
    }

    public readonly struct RouteTrafficMarker
    {
        public string Id { get; }
        public bool IsLoco { get; }
        public double Start { get; }
        public double End { get; }
        public double Centre => (Start + End) / 2;
        public RouteTrafficMarker(string id, bool isLoco, double start, double end)
        { Id = id; IsLoco = isLoco; Start = start; End = end; }
    }

    /// <summary>Exact native track identity, with no world-position nearest-track search.</summary>
    public sealed class RouteTrafficProjector<TTrack> where TTrack : notnull
    {
        private readonly Dictionary<TTrack, List<RouteInterval<TTrack>>> byTrack = new Dictionary<TTrack, List<RouteInterval<TTrack>>>();
        private readonly double reached;
        public RouteTrafficProjector(IReadOnlyList<RouteInterval<TTrack>> intervals, double reached)
        {
            this.reached = reached;
            foreach (var interval in intervals)
            {
                if (!byTrack.TryGetValue(interval.Track, out var entries)) byTrack[interval.Track] = entries = new List<RouteInterval<TTrack>>();
                entries.Add(interval);
            }
        }

        public bool TryProject(string id, bool isLoco, IReadOnlyList<RouteTrafficPoint<TTrack>> points, double length, out RouteTrafficMarker marker)
        {
            marker = default;
            var first = double.PositiveInfinity;
            var last = double.NegativeInfinity;
            foreach (var point in points)
            {
                if (!byTrack.TryGetValue(point.Track, out var intervals)) continue;
                var nearest = double.PositiveInfinity;
                foreach (var interval in intervals)
                    if (interval.TryProject(point.Span, out var distance)) nearest = Math.Min(nearest, distance);
                if (double.IsInfinity(nearest)) continue;
                first = Math.Min(first, nearest); last = Math.Max(last, nearest);
            }
            if (double.IsInfinity(first) || double.IsNaN(reached) || double.IsInfinity(reached) || reached <= 0) return false;
            var hasLength = !double.IsNaN(length) && !double.IsInfinity(length) && length > 0;
            // A loop can encounter the physical car again much later. Never stretch one car
            // between unrelated encounters. One observed bogie still establishes occupancy.
            if (hasLength && last - first > length + 1) last = first;
            var centre = (first + last) / 2;
            var half = hasLength ? length / 2 : (last - first) / 2;
            marker = new RouteTrafficMarker(id, isLoco, Math.Max(0, centre - half), Math.Min(reached, centre + half));
            return marker.End >= marker.Start;
        }
    }
}
