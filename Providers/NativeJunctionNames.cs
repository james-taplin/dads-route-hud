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
        private static readonly Dictionary<Junction, BranchMap> maps = new Dictionary<Junction, BranchMap>();
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
            bool setForRoute, RouteTurnDirection turn)
        {
            if (geometryChanged) Reset();
            if (!maps.TryGetValue(junction, out var map) || !map.Matches(junction))
            {
                if (map != null) map.Cancel();
                map = new BranchMap(junction);
                maps[junction] = map;
            }
            var branches = new string[map.Out.Length];
            for (var index = 0; index < branches.Length; index++) branches[index] = Text(map.Out[index]);
            return RouteNaming.JunctionDestinations(NativeTrackNames.StationNameForTrack(approachTrack),
                junction.selectedBranch, branches, trailing, setForRoute, Text(map.In), turn);
        }

        private static string Text(Search? search) => search?.Text ?? "destination unavailable";

        private sealed class BranchMap
        {
            public readonly Search? In;
            public readonly Search?[] Out;

            public BranchMap(Junction junction)
            {
                In = Start(junction, junction.inBranch);
                Out = new Search?[junction.outBranches?.Count ?? 0];
                for (var index = 0; index < Out.Length; index++) Out[index] = Start(junction, junction.outBranches![index]);
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

            private static Search? Start(Junction junction, Junction.Branch? branch)
            {
                if (branch?.track == null) return null;
                var search = new Search(junction, branch.track, branch.first);
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
            public Visit(DirectedTrack track, double distance) { Track = track; Distance = distance; }
        }

        private sealed class Search
        {
            private readonly Junction origin;
            private readonly Queue<Visit> remaining = new Queue<Visit>();
            private readonly HashSet<DirectedTrack> visited = new HashSet<DirectedTrack>();
            private readonly SortedSet<string> destinations = new SortedSet<string>(StringComparer.Ordinal);
            private bool partial;
            public readonly RailTrack StartTrack;
            public readonly bool StartForward;
            public bool Complete { get; private set; }

            public Search(Junction origin, RailTrack track, bool forward)
            {
                this.origin = origin;
                StartTrack = track;
                StartForward = forward;
                Enqueue(track, forward, 0);
            }

            public string Text
            {
                get
                {
                    var names = string.Join(" / ", destinations);
                    if (!Complete) return names.Length > 0 ? names + " (mapping other connections)" : "destination being mapped";
                    if (names.Length == 0) return "destination unknown";
                    return partial ? names + " (other connections unresolved)" : names;
                }
            }

            public void Step()
            {
                if (remaining.Count == 0) { Complete = true; return; }
                var visit = remaining.Dequeue();
                var track = visit.Track.Track;
                if (track == null) { partial = true; FinishIfEmpty(); return; }
                Watch(track);
                var station = NativeTrackNames.StationNameForTrack(track);
                if (station.Length > 0)
                {
                    // Stop each path at its first named station; do not imply that a branch
                    // directly serves every station further around the railway network.
                    destinations.Add(station);
                    FinishIfEmpty();
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
                if (visited.Contains(directed)) return;
                if (visited.Count >= MaximumDirectedTracks) { partial = true; return; }
                visited.Add(directed);
                remaining.Enqueue(new Visit(directed, distance));
            }
        }
    }
}
