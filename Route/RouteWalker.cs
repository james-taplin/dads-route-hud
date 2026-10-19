using System;
using System.Collections.Generic;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>Identity must distinguish actual tracks, including unnamed parallel rails.</summary>
    public interface IRouteGraph<TTrack> where TTrack : notnull
    {
        bool TryGetLength(TTrack track, out double length);
        bool TryGetNext(TTrack track, bool forward, out TTrack nextTrack, out bool nextForward);
    }

    public enum RouteWalkTermination
    {
        InProgress,
        HorizonReached,
        LoopClosed,
        EndOfTrack,
        MissingGeometry,
        InvalidGeometry,
        InvalidStart,
        InvalidDistance,
        NoProgress,
        CollectionFailed
    }

    /// <summary>A clipped, directed native-track interval and its cumulative route distance.</summary>
    public readonly struct RouteInterval<TTrack> where TTrack : notnull
    {
        public TTrack Track { get; }
        public bool Forward { get; }
        public double StartSpan { get; }
        public double EndSpan { get; }
        public double StartDistance { get; }
        public double EndDistance { get; }
        public double Length => EndDistance - StartDistance;

        public RouteInterval(TTrack track, bool forward, double startSpan, double endSpan,
            double startDistance, double endDistance)
        {
            Track = track;
            Forward = forward;
            StartSpan = startSpan;
            EndSpan = endSpan;
            StartDistance = startDistance;
            EndDistance = endDistance;
        }

        /// <summary>
        /// Includes both boundaries, including the exact requested horizon. A caller that
        /// does not want an already-seen loop-closing event can exclude EndDistance when
        /// the walk's termination is LoopClosed; profile geometry can retain that point.
        /// </summary>
        public bool TryProject(double nativeSpan, out double distance)
        {
            distance = 0;
            if (!RouteWalkMath.IsFinite(nativeSpan) || !RouteWalkMath.IsFinite(StartSpan)
                || !RouteWalkMath.IsFinite(EndSpan) || !RouteWalkMath.IsFinite(StartDistance)
                || !RouteWalkMath.IsFinite(EndDistance)) return false;
            if (nativeSpan < Math.Min(StartSpan, EndSpan) || nativeSpan > Math.Max(StartSpan, EndSpan))
                return false;
            // Preserve exact route boundaries rather than accumulating another rounding error.
            if (nativeSpan == StartSpan) distance = StartDistance;
            else if (nativeSpan == EndSpan) distance = EndDistance;
            else distance = StartDistance + (Forward ? nativeSpan - StartSpan : StartSpan - nativeSpan);
            return RouteWalkMath.IsFinite(distance) && distance >= StartDistance && distance <= EndDistance;
        }
    }

    internal static class RouteWalkMath
    {
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// Incremental distance-bounded walk. A work budget pauses the builder, never truncates
    /// a completed route. Unity/native access belongs entirely to the supplied graph adapter.
    /// </summary>
    public sealed class RouteWalkBuilder<TTrack> where TTrack : notnull
    {
        public const double DefaultRequestedDistance = 50000;
        private readonly IRouteGraph<TTrack> graph;
        private readonly List<RouteInterval<TTrack>> intervals = new List<RouteInterval<TTrack>>();
        private readonly IReadOnlyList<RouteInterval<TTrack>> intervalView;
        private readonly Dictionary<TTrack, DirectedCoverage> coverage;
        private TTrack currentTrack;
        private bool currentForward;
        private double currentSpan;
        private bool enteringAtBoundary;

        public IReadOnlyList<RouteInterval<TTrack>> Intervals => intervalView;
        public double RequestedDistance { get; }
        public double ReachedDistance { get; private set; }
        public RouteWalkTermination Termination { get; private set; }
        public bool IsComplete => Termination != RouteWalkTermination.InProgress;

        /// <summary>
        /// The directed track the walk stopped on, so a caller can name where a route ends
        /// short of the horizon. For unavailable geometry this is the track that failed, not
        /// the last one traversed. Native inspection stays with the caller's graph adapter.
        /// </summary>
        public TTrack TerminalTrack => currentTrack;
        public bool TerminalForward => currentForward;

        public RouteWalkBuilder(IRouteGraph<TTrack> graph, TTrack startTrack, double startSpan,
            bool forward, double requestedDistance = DefaultRequestedDistance,
            IEqualityComparer<TTrack>? comparer = null)
        {
            this.graph = graph ?? throw new ArgumentNullException(nameof(graph));
            currentTrack = startTrack;
            currentForward = forward;
            currentSpan = startSpan;
            RequestedDistance = requestedDistance;
            intervalView = intervals.AsReadOnly();
            coverage = new Dictionary<TTrack, DirectedCoverage>(comparer ?? EqualityComparer<TTrack>.Default);
            if (!RouteWalkMath.IsFinite(requestedDistance) || requestedDistance <= 0)
                Termination = RouteWalkTermination.InvalidDistance;
            else if (!RouteWalkMath.IsFinite(startSpan))
                Termination = RouteWalkTermination.InvalidStart;
            else if (startTrack is null)
                Termination = RouteWalkTermination.MissingGeometry;
        }

        public RouteWalkTermination Step(int maxSegments)
        {
            if (maxSegments <= 0) throw new ArgumentOutOfRangeException(nameof(maxSegments));
            for (var work = 0; work < maxSegments && !IsComplete; work++) WalkOneSegment();
            return Termination;
        }

        private void WalkOneSegment()
        {
            if (!graph.TryGetLength(currentTrack, out var length))
            {
                Termination = RouteWalkTermination.MissingGeometry;
                return;
            }
            if (!RouteWalkMath.IsFinite(length) || length < 0)
            {
                Termination = RouteWalkTermination.InvalidGeometry;
                return;
            }
            if (length == 0)
            {
                Termination = RouteWalkTermination.NoProgress;
                return;
            }
            if (enteringAtBoundary)
            {
                currentSpan = currentForward ? 0 : length;
                enteringAtBoundary = false;
            }
            if (!RouteWalkMath.IsFinite(currentSpan) || currentSpan < 0 || currentSpan > length)
            {
                Termination = RouteWalkTermination.InvalidStart;
                return;
            }

            var available = currentForward ? length - currentSpan : currentSpan;
            if (available == 0)
            {
                // Starting exactly at an exit is legitimate; skip that empty interval.
                Advance();
                return;
            }
            var remaining = RequestedDistance - ReachedDistance;
            var wanted = Math.Min(available, remaining);
            var end = wanted == available ? (currentForward ? length : 0)
                : currentSpan + (currentForward ? wanted : -wanted);
            if (!coverage.TryGetValue(currentTrack, out var directed))
            {
                directed = new DirectedCoverage();
                coverage.Add(currentTrack, directed);
            }
            var covered = currentForward ? directed.Forward : directed.Reverse;
            var closesLoop = ClipAtFirstOverlap(covered, currentSpan, ref end, currentForward);
            var traversed = Math.Abs(end - currentSpan);
            if (traversed > 0)
            {
                var reached = !closesLoop && wanted == remaining ? RequestedDistance : ReachedDistance + traversed;
                // The second test is not redundant: it also rejects progress that survives as a
                // distance but is absorbed when measured against the horizon.
                if (reached <= ReachedDistance || RequestedDistance - reached >= remaining)
                {
                    // A positive native span too small to advance representable route distance.
                    Termination = RouteWalkTermination.NoProgress;
                    return;
                }
                intervals.Add(new RouteInterval<TTrack>(currentTrack, currentForward, currentSpan, end,
                    ReachedDistance, reached));
                covered.Add(new CoveredInterval(Math.Min(currentSpan, end), Math.Max(currentSpan, end)));
                ReachedDistance = reached;
                currentSpan = end;
            }
            if (closesLoop)
            {
                Termination = RouteWalkTermination.LoopClosed;
                return;
            }
            if (traversed <= 0)
            {
                Termination = RouteWalkTermination.NoProgress;
                return;
            }
            if (ReachedDistance >= RequestedDistance)
            {
                Termination = RouteWalkTermination.HorizonReached;
                return;
            }
            Advance();
        }

        private void Advance()
        {
            if (!graph.TryGetNext(currentTrack, currentForward, out var next, out var forward))
            {
                Termination = RouteWalkTermination.EndOfTrack;
                return;
            }
            if (next is null)
            {
                Termination = RouteWalkTermination.MissingGeometry;
                return;
            }
            currentTrack = next;
            currentForward = forward;
            enteringAtBoundary = true;
        }

        private static bool ClipAtFirstOverlap(List<CoveredInterval> covered, double start,
            ref double end, bool forward)
        {
            var closesLoop = false;
            foreach (var interval in covered)
            {
                if (forward)
                {
                    // Merely touching a previously covered endpoint is not repeated traversal.
                    if (interval.Max <= start || interval.Min >= end) continue;
                    end = Math.Max(start, interval.Min);
                }
                else
                {
                    if (interval.Min >= start || interval.Max <= end) continue;
                    end = Math.Min(start, interval.Max);
                }
                closesLoop = true;
            }
            return closesLoop;
        }

        private sealed class DirectedCoverage
        {
            public readonly List<CoveredInterval> Forward = new List<CoveredInterval>();
            public readonly List<CoveredInterval> Reverse = new List<CoveredInterval>();
        }

        private readonly struct CoveredInterval
        {
            public readonly double Min;
            public readonly double Max;
            public CoveredInterval(double min, double max) { Min = min; Max = max; }
        }
    }
}
