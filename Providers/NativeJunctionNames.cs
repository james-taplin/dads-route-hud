using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>
    /// Resolves branch destinations through native directed connections. The game supplies no
    /// station-destination field on Junction; a nearby station is therefore not evidence of a
    /// branch's destination. Work is cached and advanced only from the shared Update collector.
    /// </summary>
    internal static class NativeJunctionNames
    {
        private const int TracksPerUpdate = 24;
        private const int MaximumDirectedTracks = 4096;
        private const double MaximumDistance = 50000;
        private static readonly Dictionary<(Junction junction, string originYard), BranchMap> maps =
            new Dictionary<(Junction, string), BranchMap>();
        private static readonly Queue<Search> pending = new Queue<Search>();
        private static readonly Stopwatch timer = new Stopwatch();
        private static readonly HashSet<RailTrack> watched = new HashSet<RailTrack>();
        private static bool geometryChanged;

        public static void Reset()
        {
            foreach (var track in watched)
                if (track != null) track.TrackPointsUpdated -= OnGeometryChanged;
            watched.Clear();
            maps.Clear();
            pending.Clear();
            geometryChanged = false;
        }

        private static void OnGeometryChanged(RailTrack track) => geometryChanged = true;

        private static void Watch(RailTrack track)
        {
            if (watched.Add(track)) track.TrackPointsUpdated += OnGeometryChanged;
        }

        public static void Update()
        {
            if (geometryChanged) Reset();
            timer.Restart();
            for (var count = 0; count < TracksPerUpdate && pending.Count > 0; count++)
            {
                // Always permit one item, even on a frame where the host pauses this callback.
                if (count > 0 && timer.Elapsed.TotalMilliseconds >= 0.5) break;
                var search = pending.Dequeue();
                if (!search.Complete)
                {
                    try { search.Step(); }
                    catch (Exception) { search.Fail(); }
                }
                if (!search.Complete) pending.Enqueue(search);
            }
        }

        public static string Describe(Junction junction, RailTrack approachTrack, bool trailing,
            bool setForRoute, out RouteTurnDirection turn, bool approachForward = true)
        {
            turn = RouteTurnDirection.Unknown;
            if (geometryChanged) Reset();
            var stationName = NativeTrackNames.StationNameForTrack(approachTrack);
            var originYard = stationName.Length > 0 ? approachTrack.LogicTrack()?.ID.yardId ?? "" : "";
            var key = (junction, originYard);
            if (!maps.TryGetValue(key, out var map) || !map.Matches(junction))
            {
                if (map != null) map.Cancel();
                map = new BranchMap(junction, originYard);
                maps[key] = map;
            }
            var branches = new string[map.Out.Length];
            var turns = new RouteTurnDirection[branches.Length];
            for (var index = 0; index < branches.Length; index++)
            {
                branches[index] = Text(map.Out[index]);
                turns[index] = BranchTurn(approachTrack, approachForward, junction.outBranches![index]);
            }
            if (trailing) turn = BranchTurn(approachTrack, approachForward, junction.inBranch);
            else if (junction.selectedBranch >= 0 && junction.selectedBranch < turns.Length)
                turn = turns[junction.selectedBranch];
            return RouteNaming.JunctionDestinations(stationName,
                junction.selectedBranch, branches, trailing, setForRoute, Text(map.In), turn, turns);
        }

        private static string Text(Search? search) => search?.Text ?? "destination unavailable";

        private static RouteTurnDirection BranchTurn(RailTrack approach, bool forward, Junction.Branch? branch)
        {
            if (branch?.track == null
                || !NativeRouteGeometry.TryHeading(approach, forward, true, out var ax, out var az)
                || !NativeRouteGeometry.TryJunctionHeading(branch.track, branch.first, out var bx, out var bz))
                return RouteTurnDirection.Unknown;
            return RouteTurn.Classify(ax, az, bx, bz);
        }

        private sealed class BranchMap
        {
            public readonly Search? In;
            public readonly Search?[] Out;

            public BranchMap(Junction junction, string originYard)
            {
                In = Start(junction, junction.inBranch, originYard);
                Out = new Search?[junction.outBranches?.Count ?? 0];
                for (var index = 0; index < Out.Length; index++) Out[index] = Start(junction, junction.outBranches![index], originYard);
            }

            public bool Matches(Junction junction)
            {
                if (!Same(In, junction.inBranch) || Out.Length != (junction.outBranches?.Count ?? 0)) return false;
                for (var index = 0; index < Out.Length; index++)
                    if (!Same(Out[index], junction.outBranches![index])) return false;
                return true;
            }

            public void Cancel()
            {
                In?.Cancel();
                foreach (var search in Out) search?.Cancel();
            }

            private static Search? Start(Junction junction, Junction.Branch? branch, string originYard)
            {
                if (branch?.track == null) return null;
                var search = new Search(junction, branch.track, branch.first, originYard);
                pending.Enqueue(search);
                return search;
            }

            private static bool Same(Search? search, Junction.Branch? branch) => search == null
                ? branch?.track == null : search.StartTrack == branch?.track && search.StartForward == branch.first;
        }

        private readonly struct DirectedTrack : IEquatable<DirectedTrack>
        {
            public readonly RailTrack Track;
            public readonly bool Forward;
            public DirectedTrack(RailTrack track, bool forward) { Track = track; Forward = forward; }
            public bool Equals(DirectedTrack other) => Track == other.Track && Forward == other.Forward;
            public override bool Equals(object? other) => other is DirectedTrack track && Equals(track);
            public override int GetHashCode() => (Track.GetHashCode() * 397) ^ Forward.GetHashCode();
        }

        private readonly struct Visit
        {
            public readonly DirectedTrack Track;
            public readonly double Distance;
            public readonly long Order;
            public Visit(DirectedTrack track, double distance, long order) { Track = track; Distance = distance; Order = order; }
        }

        private sealed class Search
        {
            private readonly Junction origin;
            private readonly string originYard;
            private readonly SortedSet<Visit> remaining = new SortedSet<Visit>(Comparer<Visit>.Create((a, b) =>
            {
                var distance = a.Distance.CompareTo(b.Distance);
                return distance != 0 ? distance : a.Order.CompareTo(b.Order);
            }));
            private readonly Dictionary<DirectedTrack, double> shortest = new Dictionary<DirectedTrack, double>();
            private string destination = "";
            private long order;
            private bool partial;
            public readonly RailTrack StartTrack;
            public readonly bool StartForward;
            public bool Complete { get; private set; }

            public Search(Junction origin, RailTrack track, bool forward, string originYard)
            {
                this.origin = origin;
                this.originYard = originYard;
                StartTrack = track;
                StartForward = forward;
                Enqueue(track, forward, 0);
            }

            public string Text
            {
                get
                {
                    if (!Complete) return "destination being mapped";
                    if (destination.Length == 0) return "destination unknown";
                    return partial ? destination + " (nearest known; route data incomplete)" : destination;
                }
            }

            public void Step()
            {
                if (remaining.Count == 0) { Complete = true; return; }
                var visit = remaining.Min;
                remaining.Remove(visit);
                if (shortest[visit.Track] < visit.Distance) { FinishIfEmpty(); return; }
                var track = visit.Track.Track;
                if (track == null) { partial = true; FinishIfEmpty(); return; }
                Watch(track);
                var station = NativeTrackNames.StationNameForTrack(track);
                if (station.Length > 0 && (originYard.Length == 0 || track.LogicTrack()?.ID.yardId != originYard))
                {
                    // Dijkstra order gives the nearest station by distance along rails,
                    // rather than an alphabetic list or the path with the fewest tracks.
                    destination = station;
                    remaining.Clear();
                    Complete = true;
                    return;
                }
                var length = track.GetKinkedPointSet()?.span ?? double.NaN;
                if (double.IsNaN(length) || double.IsInfinity(length) || length < 0)
                { partial = true; FinishIfEmpty(); return; }
                var distance = visit.Distance + length;
                if (distance > MaximumDistance) { partial = true; FinishIfEmpty(); return; }
                var junction = visit.Track.Forward ? track.outJunction : track.inJunction;
                // A loop back to the junction being described does not establish a destination
                // for this branch via the other original branches.
                if (junction != origin)
                {
                    var next = visit.Track.Forward ? track.GetAllOutBranches() : track.GetAllInBranches();
                    if (next != null)
                        foreach (var branch in next)
                            if (branch?.track != null) Enqueue(branch.track, branch.first, distance);
                }
                FinishIfEmpty();
            }

            public void Fail() { partial = true; remaining.Clear(); Complete = true; }
            public void Cancel() { remaining.Clear(); Complete = true; }
            private void FinishIfEmpty() { if (remaining.Count == 0) Complete = true; }
            private void Enqueue(RailTrack track, bool forward, double distance)
            {
                var directed = new DirectedTrack(track, forward);
                var known = shortest.TryGetValue(directed, out var previous);
                if (known && previous <= distance) return;
                if (!known && shortest.Count >= MaximumDirectedTracks) { partial = true; return; }
                shortest[directed] = distance;
                remaining.Add(new Visit(directed, distance, order++));
            }
        }
    }
}
