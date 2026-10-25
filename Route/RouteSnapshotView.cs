using System;
using System.Collections.Generic;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>Projects a retained capture from a later position without mutating it.</summary>
    public static class RouteSnapshotView
    {
        private const string TailPending = "tail refresh pending";
        private const string LoopPending = "captured loop boundary; refresh from current position pending";

        public static RouteSnapshot<TTrack> Shift<TTrack>(RouteSnapshot<TTrack> snapshot, double offset)
            where TTrack : notnull
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (double.IsNaN(offset) || double.IsInfinity(offset) || offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (offset == 0) return snapshot;

            var firstEvent = FirstEventAtOrAfter(snapshot.Events, offset);
            var events = new List<RouteEvent>(snapshot.Events.Count - firstEvent);
            for (var index = firstEvent; index < snapshot.Events.Count; index++)
            {
                var item = snapshot.Events[index];
                events.Add(new RouteEvent(item.Id, item.Distance - offset, item.Kind, item.Label,
                    item.Aspect, item.ColorHex, item.Fact, item.Freshness, item.AppliesToTravel, item.Code, item.Marker,
                    item.SpeedLimitKph, item.AlternateSpeedLimitKph));
            }

            var intervals = new List<RouteInterval<TTrack>>();
            foreach (var interval in snapshot.Intervals)
            {
                if (interval.EndDistance <= offset) continue;
                var clippedStart = Math.Max(interval.StartDistance, offset);
                var progress = clippedStart - interval.StartDistance;
                var nativeStart = interval.StartSpan + (interval.Forward ? progress : -progress);
                intervals.Add(new RouteInterval<TTrack>(interval.Track, interval.Forward, nativeStart,
                    interval.EndSpan, clippedStart - offset, interval.EndDistance - offset));
            }

            var profile = new List<RouteProfilePoint>();
            for (var i = 0; i < snapshot.Profile.Count; i++)
            {
                var point = snapshot.Profile[i];
                if (point.Distance < offset) continue;
                if (profile.Count == 0 && point.Distance > offset && i > 0)
                {
                    var previous = snapshot.Profile[i - 1];
                    var fraction = (offset - previous.Distance) / (point.Distance - previous.Distance);
                    profile.Add(new RouteProfilePoint(0,
                        previous.Elevation + fraction * (point.Elevation - previous.Elevation),
                        previous.GradePercent + fraction * (point.GradePercent - previous.GradePercent)));
                }
                profile.Add(new RouteProfilePoint(point.Distance - offset, point.Elevation, point.GradePercent));
            }

            var tailPending = snapshot.Termination == RouteWalkTermination.HorizonReached
                || snapshot.Termination == RouteWalkTermination.LoopClosed;
            var pendingMessage = snapshot.Termination == RouteWalkTermination.LoopClosed ? LoopPending : TailPending;
            var eventCoverage = snapshot.EventCoverage;
            if (tailPending && eventCoverage.IndexOf(pendingMessage, StringComparison.OrdinalIgnoreCase) < 0)
                eventCoverage = eventCoverage.Length == 0 ? pendingMessage : eventCoverage + "; " + pendingMessage;
            return new RouteSnapshot<TTrack>(snapshot.Revision, snapshot.RequestedDistance,
                Math.Max(0, snapshot.ReachedDistance - offset), snapshot.Termination, intervals,
                events, profile, snapshot.IsCollecting || tailPending, eventCoverage,
                snapshot.SignalCoverage, snapshot.Diagnostics, snapshot.UpdatedAtUtc, alreadySorted: true);
        }

        private static int FirstEventAtOrAfter(IReadOnlyList<RouteEvent> events, double distance)
        {
            var low = 0;
            var high = events.Count;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (events[middle].Distance < distance) low = middle + 1;
                else high = middle;
            }
            return low;
        }
    }
}
