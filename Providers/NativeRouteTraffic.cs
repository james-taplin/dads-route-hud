using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>Read-only native registry sampling in Update, never a Unity scene search or IMGUI scan.</summary>
    internal static class NativeRouteTraffic
    {
        private sealed class Observation
        {
            public TrainCar Car = null!;
            public string Id = "";
            public bool IsLoco;
            public double Length;
            public RouteTrafficPoint<RailTrack>[] Points = Array.Empty<RouteTrafficPoint<RailTrack>>();
        }
        private static readonly Stopwatch timer = new Stopwatch();
        private static readonly List<Observation> pending = new List<Observation>();
        private static readonly HashSet<RailTrack> routeTracks = new HashSet<RailTrack>();
        private static RouteSnapshot<RailTrack>? membership;
        private static IReadOnlyList<Observation> observations = Array.Empty<Observation>();
        private static List<TrainCar>? registry;
        private static TrainCar? player;
        private static Trainset? train;
        private static int cursor, passCount;
        private static float nextRead, nextOwnRead;
        private static bool reading;
        private static int unreadable;
        private static RouteSnapshot<RailTrack>? projected;
        public static IReadOnlyList<RouteTrafficMarker> Markers { get; private set; } = Array.Empty<RouteTrafficMarker>();
        public static IReadOnlyList<RouteTrafficMarker> OwnCars { get; private set; } = Array.Empty<RouteTrafficMarker>();
        public static double? SpeedKph { get; private set; }
        public static string Status { get; private set; } = "Traffic: not sampled";

        public static void Reset()
        {
            registry = null; player = null; train = null; reading = false; cursor = passCount = 0; nextRead = nextOwnRead = 0;
            pending.Clear(); observations = Array.Empty<Observation>(); projected = null;
            routeTracks.Clear(); membership = null;
            Markers = Array.Empty<RouteTrafficMarker>(); SpeedKph = null; Status = "Traffic: not sampled";
            OwnCars = Array.Empty<RouteTrafficMarker>(); unreadable = 0;
        }

        public static void Update(TrainCar? selected, RouteSnapshot<RailTrack> route)
        {
            using var timing = HudTimings.Measure(HudTimingStage.RouteTraffic);
            if (selected == null || route.Intervals.Count == 0) { Reset(); return; }
            if (player != selected || train != selected.trainset) { Reset(); player = selected; train = selected.trainset; }
            try
            {
                var speed = Math.Abs(selected.GetForwardSpeed()) * 3.6;
                SpeedKph = double.IsNaN(speed) || double.IsInfinity(speed) ? (double?)null : speed;
                if (Time.realtimeSinceStartup >= nextOwnRead)
                {
                    var own = new List<RouteTrafficMarker>();
                    var ownSeen = new HashSet<TrainCar>();
                    if (train != null)
                        foreach (var car in train.cars)
                            if (car != null && ownSeen.Add(car)) own.Add(new RouteTrafficMarker(car.ID, car.IsLoco, 0, 0));
                    if (own.Count == 0) own.Add(new RouteTrafficMarker(selected.ID, selected.IsLoco, 0, 0));
                    OwnCars = own.AsReadOnly(); nextOwnRead = Time.realtimeSinceStartup + 0.5f;
                }
                if (!ReferenceEquals(membership, route))
                {
                    routeTracks.Clear();
                    foreach (var interval in route.Intervals) routeTracks.Add(interval.Track);
                    membership = route;
                }
                // The installed CarSpawner has no AllowAutoCreate method. With the loaded-world
                // guard its native singleton already exists; AllCars is the maintained registry.
                var current = CarSpawner.Instance?.AllCars;
                if (current == null)
                {
                    observations = Array.Empty<Observation>(); reading = false; Status = "Traffic: native registry unavailable";
                    Markers = Array.Empty<RouteTrafficMarker>(); projected = route; return;
                }
                else
                {
                    if (registry != current) { registry = current; reading = false; observations = Array.Empty<Observation>(); projected = null; nextRead = 0; }
                    if (!reading && Time.realtimeSinceStartup >= nextRead)
                    { pending.Clear(); cursor = unreadable = 0; passCount = current.Count; reading = true; }
                    if (reading)
                    {
                        timer.Restart();
                        for (var count = 0; cursor < passCount && cursor < current.Count && count < 32; count++)
                        {
                            if (count > 0 && timer.Elapsed.TotalMilliseconds >= 0.5) break;
                            var car = current[cursor++];
                            try { var sample = Sample(car); if (sample != null) pending.Add(sample); }
                            catch (Exception) { unreadable++; }
                        }
                        if (cursor >= passCount || cursor >= current.Count)
                        {
                            observations = pending.ToArray(); projected = null; reading = false;
                            nextRead = Time.realtimeSinceStartup + 0.5f;
                            Status = unreadable > 0 ? "Traffic: partial local sample · " + unreadable + " unreadable"
                                : "Traffic: locally loaded cars · sampled";
                        }
                    }
                }
                if (ReferenceEquals(projected, route)) return;
                var projector = new RouteTrafficProjector<RailTrack>(route.Intervals, route.ReachedDistance);
                var markers = new List<RouteTrafficMarker>();
                var seen = new HashSet<TrainCar>();
                foreach (var item in observations)
                {
                    if (item.Car != null && item.Car != player && (train == null || item.Car.trainset != train)
                        && !item.Car.derailed && item.Car.gameObject.activeInHierarchy && seen.Add(item.Car)
                        && projector.TryProject(item.Id, item.IsLoco, item.Points, item.Length, out var marker)) markers.Add(marker);
                }
                Markers = markers.AsReadOnly(); projected = route;
            }
            catch (Exception)
            {
                // Invalidate stale occupancy; a failed read must not freeze the existing route HUD.
                pending.Clear(); observations = Array.Empty<Observation>(); Markers = Array.Empty<RouteTrafficMarker>();
                OwnCars = Array.Empty<RouteTrafficMarker>();
                projected = null; reading = false; nextRead = Time.realtimeSinceStartup + 1;
                Status = "Traffic: native sample unavailable";
                SpeedKph = null;
            }
        }

        private static Observation? Sample(TrainCar car)
        {
            if (car == null || !car.gameObject.activeInHierarchy) return null;
            var own = car == player || (train != null && car.trainset == train);
            if (own) return null;
            if (car.derailed) return null;
            var bogies = car.Bogies;
            var onRoute = false;
            foreach (var bogie in bogies)
                if (bogie != null && bogie.track != null && routeTracks.Contains(bogie.track)) { onRoute = true; break; }
            if (!onRoute) return null;
            var points = new List<RouteTrafficPoint<RailTrack>>(bogies.Length);
            foreach (var bogie in bogies)
                if (bogie != null && bogie.track != null && bogie.traveller != null)
                    points.Add(new RouteTrafficPoint<RailTrack>(bogie.track, bogie.traveller.Span));
            return points.Count == 0 ? null : new Observation { Car = car, Id = car.ID, IsLoco = car.IsLoco,
                Length = car.InterCouplerDistance, Points = points.ToArray() };
        }
    }
}
