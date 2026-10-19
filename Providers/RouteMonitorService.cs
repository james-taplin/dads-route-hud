using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using DV.Simulation.Controllers;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>Owns one retained native route capture; IMGUI only reads immutable snapshots.</summary>
    public static class RouteMonitorService
    {
        private static readonly NativeRouteGraph graph = new NativeRouteGraph();
        private static readonly Stopwatch frameTimer = new Stopwatch();
        private static readonly HashSet<RailTrack> watched = new HashSet<RailTrack>();
        private static readonly List<RouteEvent> collectingEvents = new List<RouteEvent>();
        private static readonly List<RouteProfilePoint> collectingProfile = new List<RouteProfilePoint>();
        private static readonly List<string> collectingProblems = new List<string>();
        private static readonly Dictionary<string, RailTrack> eventTracks = new Dictionary<string, RailTrack>();
        private static readonly HashSet<string> loggedFailures = new HashSet<string>();
        private static RouteWalkBuilder<RailTrack>? walk;
        private static IEnumerator<int>? collection;
        private static RouteSnapshot<RailTrack>? retained;
        private static readonly RouteDisplayBuffer<RailTrack> display = new RouteDisplayBuffer<RailTrack>();
        private static bool signalPublicationPending;
        private static double displayOffset;
        private static object? displayedSignalSource, displayedTrafficSource;
        private static TrainCar? currentCar;
        private static bool capturedVehicleForward;
        private static RouteInterval<RailTrack>? startBoundary;
        private static IReadOnlyList<RouteInterval<RailTrack>> collectionIntervals = Array.Empty<RouteInterval<RailTrack>>();
        private static bool geometryChanged;
        private static long revision;
        private static long signRevision;
        private static double captureOffset;
        private static float nextRefresh;
        private static float nextPublish;
        private static int topologyCursor;
        private static double? initialElevation;

        public static RouteSnapshot<RailTrack> Current { get; private set; } = RouteSnapshot<RailTrack>.Empty();
        public static bool IsActive => currentCar != null || walk != null || retained != null || collection != null;

        public static void Reset()
        {
            collection?.Dispose();
            collection = null;
            walk = null;
            retained = null;
            display.Clear();
            signalPublicationPending = false;
            displayOffset = 0;
            displayedSignalSource = displayedTrafficSource = null;
            startBoundary = null;
            collectionIntervals = Array.Empty<RouteInterval<RailTrack>>();
            foreach (var track in watched)
                if (track != null) track.TrackPointsUpdated -= OnGeometryChanged;
            watched.Clear();
            collectingEvents.Clear();
            collectingProfile.Clear();
            collectingProblems.Clear();
            eventTracks.Clear();
            currentCar = null;
            initialElevation = null;
            captureOffset = 0;
            geometryChanged = false;
            nextRefresh = nextPublish = 0;
            topologyCursor = 0;
            Current = RouteSnapshot<RailTrack>.Empty(Main.settings.trackInfoSettings.maxEventSpan);
        }

        private static void OnGeometryChanged(RailTrack track) => geometryChanged = true;

        public static void Update(TrainCar? car)
        {
            try
            {
                if (!TryAnchor(car, out var track, out var span, out var forward, out var vehicleForward))
                {
                    if (IsActive) Reset();
                    return;
                }
                var requested = Main.settings.trackInfoSettings.maxEventSpan;
                var hardChange = car != currentCar || vehicleForward != capturedVehicleForward || geometryChanged
                    || (retained != null && requested != retained.RequestedDistance)
                    || (walk != null && requested != walk.RequestedDistance);
                if (!hardChange && walk == null && retained == null && Time.realtimeSinceStartup < nextRefresh)
                    return;
                if (!hardChange && retained != null)
                {
                    if (!TryOffset(retained.Intervals, track, span, forward, captureOffset, out captureOffset)
                        || !CheckNextConnections(retained.Intervals, 16)) hardChange = true;
                }
                if (hardChange || (walk == null && retained == null))
                    Begin(car!, track, span, forward, vehicleForward, requested, true);
                else if (walk == null && Time.realtimeSinceStartup >= nextRefresh && !OptionalSignalTelemetry.IsReading)
                    Begin(car!, track, span, forward, vehicleForward, requested, false);

                frameTimer.Restart();
                while (walk != null && !walk.IsComplete && frameTimer.Elapsed.TotalMilliseconds < 2)
                    walk.Step(16);
                if (walk != null && walk.IsComplete && collection == null)
                {
                    collectingEvents.Clear();
                    collectingProfile.Clear();
                    collectingProblems.Clear();
                    initialElevation = null;
                    collectionIntervals = IncludeStartBoundary(walk.Intervals);
                    collection = Collect(walk, collectionIntervals).GetEnumerator();
                }
                while (collection != null && frameTimer.Elapsed.TotalMilliseconds < 2)
                {
                    if (!collection.MoveNext())
                    {
                        collection.Dispose();
                        collection = null;
                        FinishCapture();
                        break;
                    }
                }

                if (retained != null)
                {
                    OptionalSignalTelemetry.Update(car, retained.Intervals, retained.Revision);
                    if (Time.realtimeSinceStartup >= nextPublish)
                    {
                        // Position is measured against this capture's exact native intervals.
                        if (TryOffset(retained.Intervals, track, span, forward, captureOffset, out var offset))
                        {
                            captureOffset = offset;
                            var incoming = MergeOptional(retained);
                            var previous = display.Capture;
                            var oldOffset = 0d;
                            var hold = signalPublicationPending && OptionalSignalTelemetry.IsReading
                                && OptionalSignalTelemetry.IsAvailable && previous != null
                                && ReferenceEquals(displayedSignalSource, OptionalSignalTelemetry.ActiveSignalSource)
                                && ReferenceEquals(displayedTrafficSource, OptionalSignalTelemetry.ActiveTrafficSource)
                                && TryOffset(previous.Intervals, track, span, forward, displayOffset, out oldOffset);
                            var selected = display.Select(incoming, hold);
                            if (hold)
                            {
                                displayOffset = oldOffset;
                                selected = new RouteSnapshot<RailTrack>(selected.Revision, selected.RequestedDistance,
                                    selected.ReachedDistance, selected.Termination, selected.Intervals, selected.Events,
                                    selected.Profile, true, selected.EventCoverage + "; replacement pending",
                                    "Refreshing signals; previous same-route sample retained", selected.Diagnostics,
                                    selected.UpdatedAtUtc, alreadySorted: true);
                            }
                            else
                            {
                                signalPublicationPending = false;
                                displayOffset = offset;
                                displayedSignalSource = OptionalSignalTelemetry.ActiveSignalSource;
                                displayedTrafficSource = OptionalSignalTelemetry.ActiveTrafficSource;
                            }
                            Current = RouteSnapshotView.Shift(selected, displayOffset);
                        }
                        else if (retained.Intervals.Count == 0) Current = MergeOptional(retained);
                        nextPublish = Time.realtimeSinceStartup + 0.25f;
                    }
                }
                else if (walk != null && Time.realtimeSinceStartup >= nextPublish)
                {
                    var partial = new RouteSnapshot<RailTrack>(revision, walk.RequestedDistance, walk.ReachedDistance,
                        walk.Termination, IncludeStartBoundary(walk.Intervals), collectingEvents, collectingProfile, true,
                        collection == null ? "Walking native selected route; event collection pending"
                            : "Collecting every native sign part and track item; observations partial",
                        "Optional signals pending route capture", collectingProblems);
                    if (TryOffset(partial.Intervals, track, span, forward, captureOffset, out var offset))
                    {
                        captureOffset = offset;
                        Current = RouteSnapshotView.Shift(partial, offset);
                    }
                    else Current = new RouteSnapshot<RailTrack>(revision, requested, 0, RouteWalkTermination.InProgress,
                        Array.Empty<RouteInterval<RailTrack>>(), Array.Empty<RouteEvent>(), Array.Empty<RouteProfilePoint>(),
                        true, "Live position is beyond the intervals collected so far; waiting for capture",
                        "Optional signals pending route capture");
                    nextPublish = Time.realtimeSinceStartup + 0.25f;
                }
            }
            catch (Exception exception)
            {
                var message = exception.GetBaseException().Message;
                if (loggedFailures.Add(message)) Main.mod?.Logger.Warning("Route collection failed: " + message);
                var requested = Main.settings.trackInfoSettings.maxEventSpan;
                var intervals = walk?.Intervals ?? retained?.Intervals ?? Array.Empty<RouteInterval<RailTrack>>();
                Current = new RouteSnapshot<RailTrack>(revision, requested, walk?.ReachedDistance ?? 0,
                    RouteWalkTermination.CollectionFailed, intervals, collectingEvents, collectingProfile,
                    false, "Route capture failed; retry pending", OptionalSignalTelemetry.Status, new[] { message });
                collection?.Dispose();
                collection = null;
                walk = null;
                retained = null;
                nextRefresh = Time.realtimeSinceStartup + 2;
            }
        }

        private static void Begin(TrainCar car, RailTrack track, double span, bool forward, bool vehicleForward,
            double requested, bool discardPrevious)
        {
            collection?.Dispose();
            collection = null;
            collectingEvents.Clear();
            collectingProfile.Clear();
            collectingProblems.Clear();
            initialElevation = null;
            if (discardPrevious)
            {
                retained = null;
                display.Clear();
                signalPublicationPending = false;
                displayOffset = 0;
                displayedSignalSource = displayedTrafficSource = null;
                eventTracks.Clear();
                captureOffset = 0;
                OptionalSignalTelemetry.Reset();
                Current = RouteSnapshot<RailTrack>.Empty(requested);
            }
            currentCar = car;
            capturedVehicleForward = vehicleForward;
            startBoundary = graph.TryGetLength(track, out var length) && length > 0 && span == (forward ? length : 0)
                ? new RouteInterval<RailTrack>(track, forward, span, span, 0, 0) : (RouteInterval<RailTrack>?)null;
            collectionIntervals = Array.Empty<RouteInterval<RailTrack>>();
            geometryChanged = false;
            walk = new RouteWalkBuilder<RailTrack>(graph, track, span, forward, requested);
            revision++;
            nextPublish = 0;
        }

        private static IReadOnlyList<RouteInterval<RailTrack>> IncludeStartBoundary(IReadOnlyList<RouteInterval<RailTrack>> intervals)
        {
            // The walker skips an empty first interval. Keep its 0 m observations at an exact track exit.
            if (!startBoundary.HasValue) return intervals;
            var result = new List<RouteInterval<RailTrack>>(intervals.Count + 1) { startBoundary.Value };
            result.AddRange(intervals);
            return result;
        }

        private static IEnumerable<int> Collect(RouteWalkBuilder<RailTrack> source, IReadOnlyList<RouteInterval<RailTrack>> intervals)
        {
            for (var index = 0; index < intervals.Count; index++)
            {
                var interval = intervals[index];
                if (interval.Track == null)
                {
                    collectingProblems.Add("Native track destroyed while collecting");
                    yield return 0;
                    continue;
                }
                if (watched.Add(interval.Track)) interval.Track.TrackPointsUpdated += OnGeometryChanged;
                var trackId = interval.Track.LogicTrack()?.ID?.ToString();
                var label = string.IsNullOrWhiteSpace(trackId) ? interval.Track.name : trackId + " · " + interval.Track.name;
                var eventId = "track:" + interval.Track.GetInstanceID() + ":" + index;
                collectingEvents.Add(new RouteEvent(eventId, interval.StartDistance, RouteEventKind.Track, label));
                eventTracks[eventId] = interval.Track;
                yield return 0;

                var signs = NativeRouteSignIndex.EnsureTrack(interval.Track);
                foreach (var sign in signs)
                {
                    if (!interval.TryProject(sign.Span, out var distance) || AtRepeatedEndpoint(source, distance)) continue;
                    collectingEvents.Add(new RouteEvent(sign.SourceId + ":encounter:" + index,
                        distance, sign.Kind, sign.Label, fact: sign.Fact, appliesToTravel: sign.Forward == interval.Forward));
                    yield return 0;
                }

                // Geometry samples are a profile, separate from the retained physical sign events.
                AddProfile(interval, interval.StartDistance);
                yield return 0;
                for (var distance = (Math.Floor(interval.StartDistance / 25) + 1) * 25;
                    distance < interval.EndDistance; distance += 25)
                {
                    AddProfile(interval, distance);
                    yield return 0;
                }
                AddProfile(interval, interval.EndDistance);
                yield return 0;

                var pointSet = interval.Track.GetKinkedPointSet();
                var nativeEnd = interval.Forward ? pointSet.span : 0;
                if (interval.EndSpan == nativeEnd && !AtRepeatedEndpoint(source, interval.EndDistance))
                {
                    var junction = interval.Forward ? interval.Track.outJunction : interval.Track.inJunction;
                    if (junction != null)
                    {
                        var count = junction.outBranches?.Count ?? 0;
                        var junctionLabel = junction.name + " · branch " + (junction.selectedBranch + 1) + "/" + count;
                        collectingEvents.Add(new RouteEvent("junction:" + junction.GetInstanceID() + ":encounter:" + index,
                            interval.EndDistance, RouteEventKind.Junction, junctionLabel));
                    }
                }
            }
        }

        private static bool AtRepeatedEndpoint(RouteWalkBuilder<RailTrack> source, double distance) =>
            source.Termination == RouteWalkTermination.LoopClosed && distance == source.ReachedDistance;

        private static void AddProfile(RouteInterval<RailTrack> interval, double distance)
        {
            var nativeSpan = interval.StartSpan + (interval.Forward ? 1 : -1) * (distance - interval.StartDistance);
            // Preserve exact native endpoints instead of recomputing their floating-point sums.
            if (distance == interval.StartDistance) nativeSpan = interval.StartSpan;
            if (distance == interval.EndDistance) nativeSpan = interval.EndSpan;
            if (!NativeRouteGeometry.TrySample(interval.Track, nativeSpan, interval.Forward, out var elevation, out var grade))
            {
                var problem = "Profile sample unavailable on " + interval.Track.name;
                if (!collectingProblems.Contains(problem)) collectingProblems.Add(problem);
                return;
            }
            if (!initialElevation.HasValue) initialElevation = elevation;
            var point = new RouteProfilePoint(distance, elevation - initialElevation.Value, grade);
            if (collectingProfile.Count > 0 && collectingProfile[collectingProfile.Count - 1].Distance == distance)
                collectingProfile[collectingProfile.Count - 1] = point;
            else collectingProfile.Add(point);
        }

        private static void FinishCapture()
        {
            if (walk == null) return;
            if (!CheckAllConnections(collectionIntervals))
            {
                geometryChanged = true;
                retained = null;
                walk = null;
                Current = RouteSnapshot<RailTrack>.Empty(Main.settings.trackInfoSettings.maxEventSpan);
                return;
            }
            retained = new RouteSnapshot<RailTrack>(revision, walk.RequestedDistance, walk.ReachedDistance,
                walk.Termination, collectionIntervals, collectingEvents, collectingProfile, false,
                NativeRouteSignIndex.Coverage, "Optional signals pending sample",
                collectingProblems.Concat(NativeRouteSignIndex.Diagnostics));
            signRevision = NativeRouteSignIndex.SnapshotRevision;
            signalPublicationPending = display.Capture != null;
            walk = null;
            captureOffset = 0;
            topologyCursor = 0;
            nextRefresh = Time.realtimeSinceStartup + 2;
            nextPublish = 0;
            OptionalSignalTelemetry.Reset();
            var needed = new HashSet<RailTrack>(retained.Intervals.Select(i => i.Track));
            foreach (var track in watched.ToArray())
                if (track == null || !needed.Contains(track))
                {
                    if (track != null) track.TrackPointsUpdated -= OnGeometryChanged;
                    watched.Remove(track!);
                }
            // Drop mappings from older captures without removing any current observations.
            var ids = new HashSet<string>(retained.Events.Where(e => e.Kind == RouteEventKind.Track).Select(e => e.Id));
            foreach (var id in eventTracks.Keys.ToArray()) if (!ids.Contains(id)) eventTracks.Remove(id);
        }

        private static RouteSnapshot<RailTrack> MergeOptional(RouteSnapshot<RailTrack> source)
        {
            var events = new List<RouteEvent>(source.Events.Count + OptionalSignalTelemetry.UpcomingSignals.Count);
            foreach (var item in source.Events)
            {
                var fact = item.Fact;
                var freshness = item.Freshness;
                if (item.Kind == RouteEventKind.Track && eventTracks.TryGetValue(item.Id, out var track) && track != null)
                {
                    fact = OptionalSignalTelemetry.GetTrackReservation(track);
                    freshness = OptionalSignalTelemetry.IsAvailable ? "Local occupancy/reservation observation; " + OptionalSignalTelemetry.Status : "";
                }
                events.Add(new RouteEvent(item.Id, item.Distance, item.Kind, item.Label, item.Aspect,
                    item.ColorHex, fact, freshness, item.AppliesToTravel));
            }
            foreach (var signal in OptionalSignalTelemetry.UpcomingSignals)
            {
                if (signal.span < 0 || signal.span > source.ReachedDistance
                    || (source.Termination == RouteWalkTermination.LoopClosed && signal.span == source.ReachedDistance)) continue;
                events.Add(new RouteEvent(signal.SourceId + "@" + signal.span.ToString("R", CultureInfo.InvariantCulture),
                    signal.span, RouteEventKind.Signal, signal.Name, signal.Aspect, signal.ColorHex,
                    signal.Reservation, signal.Freshness, signal.FacingTravel));
            }
            var collecting = walk != null || (!OptionalSignalTelemetry.IsComplete && OptionalSignalTelemetry.IsAvailable);
            var eventCoverage = source.EventCoverage;
            if (NativeRouteSignIndex.SnapshotRevision != signRevision)
            {
                nextRefresh = 0;
                eventCoverage += "; native sign index changed, refresh pending";
                collecting = true;
            }
            return new RouteSnapshot<RailTrack>(source.Revision, source.RequestedDistance, source.ReachedDistance,
                source.Termination, source.Intervals, events, source.Profile, collecting, eventCoverage,
                OptionalSignalTelemetry.Status, source.Diagnostics.Concat(OptionalSignalTelemetry.Diagnostics));
        }

        private static bool CheckAllConnections(IReadOnlyList<RouteInterval<RailTrack>> intervals)
        {
            for (var i = 0; i + 1 < intervals.Count; i++)
                if (!ConnectionMatches(intervals[i], intervals[i + 1])) return false;
            return true;
        }

        private static bool CheckNextConnections(IReadOnlyList<RouteInterval<RailTrack>> intervals, int budget)
        {
            if (intervals.Count < 2) return true;
            for (var step = 0; step < budget; step++)
            {
                if (topologyCursor + 1 >= intervals.Count) topologyCursor = 0;
                var index = topologyCursor++;
                if (!ConnectionMatches(intervals[index], intervals[index + 1])) return false;
            }
            return true;
        }

        private static bool ConnectionMatches(RouteInterval<RailTrack> a, RouteInterval<RailTrack> b) =>
            graph.TryGetNext(a.Track, a.Forward, out var next, out var forward) && next == b.Track && forward == b.Forward;

        private static bool TryOffset(IReadOnlyList<RouteInterval<RailTrack>> intervals, RailTrack track,
            double span, bool forward, double previous, out double offset)
        {
            offset = 0;
            var error = double.PositiveInfinity;
            var found = false;
            foreach (var interval in intervals)
            {
                if (interval.Track != track || interval.Forward != forward || !interval.TryProject(span, out var candidate)) continue;
                var delta = Math.Abs(candidate - previous);
                if (delta >= error) continue;
                found = true;
                offset = candidate;
                error = delta;
            }
            // Crossing an old loop closure can map back to the original suffix. Re-capture there.
            return found && offset + 0.5 >= previous;
        }

        private static bool TryAnchor(TrainCar? car, out RailTrack track, out double span, out bool forward, out bool vehicleForward)
        {
            track = null!;
            span = 0;
            forward = true;
            vehicleForward = true;
            if (car == null || car.Bogies == null || car.Bogies.Length == 0) return false;
            var bogie = car.Bogies[Math.Min(1, car.Bogies.Length - 1)];
            if (bogie == null || bogie.track == null || bogie.traveller == null) return false;
            var controls = car.SimController?.controlsOverrider ?? PlayerManager.LastLoco?.SimController?.controlsOverrider;
            var reverser = controls?.Reverser?.Value;
            var speed = car.GetForwardSpeed();
            vehicleForward = Math.Abs(speed) > 0.15f ? speed > 0
                : reverser.HasValue && Math.Abs(reverser.Value - ReverserControl.NEUTRAL_VALUE) > 0.001
                    ? reverser.Value > ReverserControl.NEUTRAL_VALUE : true;
            track = bogie.track;
            span = bogie.traveller.Span;
            forward = vehicleForward == (bogie.TrackDirectionSign > 0);
            return !double.IsNaN(span) && !double.IsInfinity(span);
        }
    }
}
