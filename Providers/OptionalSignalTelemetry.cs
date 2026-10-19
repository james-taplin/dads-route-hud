using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>
    /// Optional facts projected onto the collector's exact intervals, outside OnGUI.
    /// AI Traffic enriches observations; its short-range discovery is never used.
    /// </summary>
    public static class OptionalSignalTelemetry
    {
        private const int CorruptEnumerationWatchdog = 1000000;
        private static readonly Dictionary<Type, Dictionary<string, MemberInfo?>> members = new Dictionary<Type, Dictionary<string, MemberInfo?>>();
        private static IReadOnlyList<SignalTrackEvent> readings = Array.Empty<SignalTrackEvent>();
        private static Dictionary<RailTrack, string> reservations = new Dictionary<RailTrack, string>();
        private static IReadOnlyList<string> diagnostics = Array.Empty<string>();
        private static Assembly? signalsAssembly, trafficAssembly;
        private static Type? managerType;
        private static MethodInfo? isTrackReserved, isTrackOccupied, aiAspectName, aiAspectColor, aiSignalName, aiSignalReservation, graphHolder;
        private static object? occupancyMode;
        private static FieldInfo? trafficInstance, graphInstance, signalManagerInstance;
        private static TrainCar? snapshotCar;
        private static long snapshotRevision = long.MinValue;
        private static float nextRead;
        private static IEnumerator<int>? work;
        private static ReadPass? pass;
        private static bool reportedFailure;

        public static IReadOnlyList<SignalTrackEvent> UpcomingSignals => readings;
        public static IReadOnlyList<string> Diagnostics => diagnostics;
        public static bool IsComplete { get; private set; }
        public static bool IsReading => work != null;
        internal static Assembly? ActiveSignalSource => signalsAssembly;
        internal static Assembly? ActiveTrafficSource => trafficAssembly;
        public static bool IsAvailable => managerType != null;
        public static float SampledAt { get; private set; }
        public static string Status { get; private set; } = "Signals unavailable";
        public static string GetTrackReservation(RailTrack track)
            => track != null && reservations.TryGetValue(track, out var value) ? value : string.Empty;

        public static void Reset()
        {
            Cancel();
            readings = Array.Empty<SignalTrackEvent>();
            reservations.Clear();
            diagnostics = Array.Empty<string>();
            ClearBindings();
            signalsAssembly = trafficAssembly = null;
            nextRead = SampledAt = 0;
            snapshotCar = null;
            snapshotRevision = long.MinValue;
            reportedFailure = IsComplete = false;
            Status = "Signals unavailable";
        }

        // Revision changes whenever the route intervals, direction or selected path change.
        public static void Update(TrainCar? car, IReadOnlyList<RouteInterval<RailTrack>> intervals, long routeRevision)
        {
            if (!Main.enabled || !Main.settings.trackInfoSettings.enabled || car == null) { Reset(); return; }
            if (work == null && Time.realtimeSinceStartup < nextRead
                && Status.StartsWith("Optional contract unavailable:", StringComparison.Ordinal)) return;
            bool changed;
            try { changed = Bind(); }
            catch (Exception exception)
            {
                Cancel();
                readings = Array.Empty<SignalTrackEvent>();
                reservations.Clear();
                SampledAt = 0;
                IsComplete = false;
                diagnostics = new[] { "Optional contract unavailable: " + exception.GetBaseException().Message };
                Status = diagnostics[0];
                ClearBindings();
                signalsAssembly = trafficAssembly = null;
                nextRead = Time.realtimeSinceStartup + 0.5f;
                return;
            }
            if (changed || car != snapshotCar || routeRevision != snapshotRevision)
            {
                Cancel();
                readings = Array.Empty<SignalTrackEvent>();
                reservations.Clear();
                diagnostics = Array.Empty<string>();
                IsComplete = false;
                SampledAt = 0;
                nextRead = 0;
                snapshotCar = car;
                snapshotRevision = routeRevision;
            }
            if (work == null)
            {
                if (Time.realtimeSinceStartup < nextRead) return;
                pass = new ReadPass();
                work = Collect(intervals, pass).GetEnumerator();
                Status = managerType == null ? "DVSignals not active; optional facts unavailable"
                    : SampledAt > 0 ? "Refreshing optional signals; previous sample retained"
                    : "Reading optional signals; observations partial";
            }
            var timer = Stopwatch.StartNew();
            try
            {
                do
                {
                    if (!work.MoveNext())
                    {
                        Publish(pass!, true);
                        Cancel();
                        nextRead = Time.realtimeSinceStartup + 0.5f;
                        return;
                    }
                    if (++pass!.Steps >= CorruptEnumerationWatchdog)
                        throw new InvalidOperationException("Optional enumeration watchdog reached");
                } while (timer.Elapsed.TotalMilliseconds < 2);
            }
            catch (Exception exception)
            {
                pass!.Problems.Add("Optional data partial: " + exception.GetBaseException().Message);
                Publish(pass, true);
                Cancel();
                nextRead = Time.realtimeSinceStartup + 0.5f;
                if (!reportedFailure)
                {
                    Main.mod?.Logger.Warning("Optional route telemetry partial: " + exception.GetBaseException().Message);
                    reportedFailure = true;
                }
            }
        }

        private static void Cancel() { work?.Dispose(); work = null; pass = null; }
        private static void Publish(ReadPass result, bool finished)
        {
            result.Signals.Sort((a, b) => a.span.CompareTo(b.span));
            readings = result.Signals.ToArray();
            reservations = new Dictionary<RailTrack, string>(result.Reservations);
            diagnostics = result.Problems.ToArray();
            IsComplete = finished && result.Problems.Count == 0;
            SampledAt = Time.realtimeSinceStartup;
            Status = managerType == null ? "DVSignals not active; optional facts unavailable"
                : result.Problems.Count > 0 ? "Optional signals partial (" + result.Problems.Count + " diagnostics)"
                : "Signal placements read; aspect update age unverified; occupancy/reservations sampled locally";
        }

        private static IEnumerable<int> Collect(IReadOnlyList<RouteInterval<RailTrack>> intervals, ReadPass result)
        {
            var byTrack = new Dictionary<RailTrack, List<RouteInterval<RailTrack>>>();
            foreach (var interval in intervals)
            {
                if (interval.Track == null) continue;
                if (!byTrack.TryGetValue(interval.Track, out var entries))
                    byTrack[interval.Track] = entries = new List<RouteInterval<RailTrack>>();
                entries.Add(interval);
                if (!result.Reservations.ContainsKey(interval.Track))
                    result.Reservations[interval.Track] = ReadTrackReservation(interval.Track, result);
                yield return 0;
            }
            if (managerType == null) yield break;
            // Read the native existing-instance slot. The public Instance getter can
            // create a manager, and a repeated scene-wide Find is synchronous work.
            var manager = signalManagerInstance?.GetValue(null) as UnityEngine.Object;
            if (manager == null) { result.Problems.Add("DVSignals manager unavailable"); yield break; }
            if (!managerType.IsInstanceOfType(manager))
            { result.Problems.Add("DVSignals singleton instance type is incompatible"); yield break; }
            var controllers = Read(manager, "AllControllers") as IEnumerable;
            if (controllers == null) { result.Problems.Add("DVSignals controller catalogue unavailable"); yield break; }
            var seen = new HashSet<object>(ReferenceComparer.Instance);
            foreach (var controller in controllers)
            {
                yield return 0;
                if (controller == null || !seen.Add(controller)) continue;
                if (Read(controller, "Exists") is bool exists && !exists) continue;
                var placement = Read(controller, "PlacementInfo");
                var track = Read(placement, "Track") as RailTrack;
                if (track == null) { result.UnassociatedControllers++; continue; }
                if (!byTrack.TryGetValue(track, out var candidates)) continue;
                if (!TryDouble(Read(placement, "Span"), out var nativeSpan)
                    || !TryDouble(Read(placement, "Direction"), out var direction) || (direction != 0 && direction != 1))
                {
                    result.Problems.Add("Invalid placement: " + SourceId(controller, "controller"));
                    continue;
                }
                var heads = Read(controller, "AllSignals") as IEnumerable;
                if (heads == null) { result.Problems.Add("Signal heads unavailable: " + SourceId(controller, "controller")); continue; }
                var headSeen = new HashSet<object>(ReferenceComparer.Instance);
                foreach (var firstHead in heads)
                {
                    object? signal = firstHead;
                    // Native embedded distant heads are registered separately and are
                    // not yielded by BasicSignalController.AllSignals.
                    while (signal != null && headSeen.Add(signal))
                    {
                        yield return 0;
                        foreach (var interval in candidates)
                        {
                            if (!interval.TryProject(nativeSpan, out var distance)) continue;
                            // Keep opposing signals too; facing is metadata, not a collection filter.
                            var facingTravel = interval.Forward ? direction == 1 : direction == 0;
                            var id = SourceId(signal, "signal");
                            var key = id + "@" + distance.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                            if (!result.SeenPlacements.Add(key)) continue;
                            foreach (var step in ReadSignal(controller, signal, distance, id, facingTravel, result)) yield return step;
                        }
                        signal = Read(signal, "DistantSignal");
                    }
                }
            }
            if (result.UnassociatedControllers > 0)
                result.Problems.Add(result.UnassociatedControllers
                    + " observed signal controllers lack an exact native track placement; not assigned to route (coverage uncertain)");
        }

        private static IEnumerable<int> ReadSignal(object controller, object signal, double distance, string sourceId, bool facingTravel, ReadPass result)
        {
            var aspect = Read(signal, "CurrentAspect");
            var name = Enrich(aiSignalName, signal) ?? Read(signal, "Name") as string ?? "Signal";
            var label = Enrich(aiAspectName, signal) ?? Read(aspect, "Id") as string ?? "Off";
            var color = Enrich(aiAspectColor, signal) ?? NativeAspectColor(aspect);
            var occupied = false;
            var aiReserved = false;
            var reserved = false;
            var blockTracks = Read(Read(signal, "Block"), "AllTracks") as IEnumerable;
            if (blockTracks != null)
                foreach (var item in blockTracks)
                {
                    if (item is RailTrack track)
                    {
                        if (!result.Reservations.TryGetValue(track, out var next))
                            result.Reservations[track] = next = ReadTrackReservation(track, result);
                        occupied |= next.Contains("Occupied");
                        aiReserved |= next.Contains("AI reserved");
                        reserved |= next.Contains("Reserved");
                    }
                    yield return 0;
                }
            var state = occupied ? "Block occupied" : string.Empty;
            if (aiReserved) state += (state.Length > 0 ? "; " : "Block: ") + "AI reserved";
            else if (reserved) state += (state.Length > 0 ? "; " : "Block: ") + "Reserved; owner unknown";
            var freshness = "Aspect observed; update age unverified";
            if (TryDouble(Invoke(controller, "GetCameraDistanceSqr"), out var cameraSqr) && cameraSqr >= 2250000)
                freshness = "Distant aspect may be stale; update age unverified";
            if (Read(signal, "IsShunting") is bool shunting && shunting) label += " (shunting)";
            result.Signals.Add(new SignalTrackEvent(distance, name, color, label, state, sourceId,
                freshness + "; controller anchor; occupancy/reservations sampled locally", facingTravel));
        }

        private static string ReadTrackReservation(RailTrack track, ReadPass result)
        {
            try
            {
                var occupied = isTrackOccupied != null && occupancyMode != null
                    && isTrackOccupied.Invoke(null, new[] { (object)track, occupancyMode }) is bool stock && stock;
                var state = ReadReservationOwner(track);
                return occupied ? "Occupied" + (state.Length == 0 ? "" : "; " + state) : state;
            }
            catch (Exception exception)
            {
                result.Problems.Add("Track state unavailable: " + track.name + " (" + exception.GetBaseException().Message + ")");
                return "State unavailable";
            }
        }
        private static string ReadReservationOwner(RailTrack track)
        {
            var graph = graphInstance?.GetValue(null);
            if (graph != null && graphHolder != null)
            {
                var args = new object?[] { track, null, null };
                if (graphHolder.Invoke(graph, args) is bool held && held)
                    return args[2]?.GetType().FullName == "AITraffic.Driver.AIEngineer" ? "AI reserved" : "Reserved; owner unknown";
            }
            if (isTrackReserved == null) return string.Empty;
            var signalArgs = new object?[] { track, null };
            if (!(isTrackReserved.Invoke(null, signalArgs) is bool reserved) || !reserved) return string.Empty;
            // The helper calls TrafficManager.Instance internally; require an existing manager.
            if (signalArgs[1] != null && trafficInstance?.GetValue(null) is UnityEngine.Object manager && manager != null
                && aiSignalReservation != null && aiSignalReservation.Invoke(null, new[] { signalArgs[1] }) is bool ai && ai)
                return "AI reserved";
            return "Reserved; owner unknown";
        }

        private static bool Bind()
        {
            var signals = UnityModManager.FindMod("DVSignals");
            var traffic = UnityModManager.FindMod("AITraffic");
            var loadedSignals = signals != null && signals.Active ? signals.Assembly : null;
            var loadedTraffic = traffic != null && traffic.Active ? traffic.Assembly : null;
            if (ReferenceEquals(signalsAssembly, loadedSignals) && ReferenceEquals(trafficAssembly, loadedTraffic)) return false;
            ClearBindings();
            signalsAssembly = loadedSignals;
            trafficAssembly = loadedTraffic;
            managerType = loadedSignals?.GetType("Signals.Game.SignalManager", false);
            if (loadedSignals != null)
            {
                if (managerType == null) throw new MissingMemberException("DVSignals SignalManager type unavailable");
                var singletonBase = managerType.BaseType;
                if (singletonBase == null || !singletonBase.IsGenericType
                    || singletonBase.GetGenericTypeDefinition() != typeof(DV.Utils.SingletonBehaviour<>)
                    || singletonBase.GetGenericArguments().Length != 1 || singletonBase.GetGenericArguments()[0] != managerType)
                    throw new MissingMemberException("DVSignals SignalManager native singleton ancestry changed");
                signalManagerInstance = singletonBase.GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (signalManagerInstance == null || !signalManagerInstance.IsStatic || signalManagerInstance.FieldType != managerType)
                    throw new MissingMemberException("DVSignals native existing-instance slot unavailable");
            }
            isTrackReserved = loadedSignals?.GetType("Signals.Game.Railway.TrackReserver", false)?.GetMethod("IsTrackReserved", BindingFlags.Public | BindingFlags.Static);
            isTrackOccupied = loadedSignals?.GetType("Signals.Game.Railway.TrackChecker", false)?.GetMethod("IsOccupied", BindingFlags.Public | BindingFlags.Static);
            if (isTrackOccupied != null) occupancyMode = Enum.ToObject(isTrackOccupied.GetParameters()[1].ParameterType, 0);
            var registry = loadedTraffic?.GetType("AITraffic.Navigation.SignalRegistry", false);
            aiAspectName = registry?.GetMethod("GetAspectDisplayName", BindingFlags.Public | BindingFlags.Static);
            aiAspectColor = registry?.GetMethod("GetAspectColorHex", BindingFlags.Public | BindingFlags.Static);
            aiSignalName = registry?.GetMethod("GetSignalName", BindingFlags.Public | BindingFlags.Static);
            aiSignalReservation = registry?.GetMethod("IsSignalReservedByAI", BindingFlags.Public | BindingFlags.Static);
            trafficInstance = loadedTraffic?.GetType("AITraffic.Core.TrafficManager", false)?.GetField("s_instance", BindingFlags.NonPublic | BindingFlags.Static);
            var graphType = loadedTraffic?.GetType("AITraffic.Navigation.RailGraph", false);
            graphInstance = graphType?.GetField("s_instance", BindingFlags.NonPublic | BindingFlags.Static);
            graphHolder = graphType?.GetMethod("TryGetTrackReservationHolder", BindingFlags.Public | BindingFlags.Instance);
            return true;
        }
        private static void ClearBindings()
        {
            members.Clear();
            managerType = null;
            isTrackReserved = isTrackOccupied = aiAspectName = aiAspectColor = aiSignalName = aiSignalReservation = graphHolder = null;
            occupancyMode = null;
            trafficInstance = graphInstance = signalManagerInstance = null;
        }
        private static string SourceId(object source, string prefix)
            => "dvsignals:" + prefix + ":" + (Read(source, "Id")?.ToString() ?? RuntimeHelpers.GetHashCode(source).ToString(System.Globalization.CultureInfo.InvariantCulture));
        private static bool TryDouble(object? value, out double result)
        {
            try { result = value == null ? double.NaN : Convert.ToDouble(value); }
            catch (Exception) { result = double.NaN; }
            return !double.IsNaN(result) && !double.IsInfinity(result);
        }
        private static string? Enrich(MethodInfo? method, object signal)
        {
            try { return method?.Invoke(null, new[] { signal }) as string; }
            catch (Exception) { return null; } // An enrichment failure cannot remove a discovered head.
        }
        private static string NativeAspectColor(object? aspect)
        {
            if (aspect == null) return "#AAAAAA";
            if (Read(aspect, "DisallowPassing") is bool stop && stop) return "#FF4444";
            var lamps = Read(Invoke(aspect, "GetDefinition"), "OnLights") as IEnumerable;
            if (lamps != null)
                foreach (var lamp in lamps)
                    if (Read(lamp, "Colour") is Color color) return "#" + ColorUtility.ToHtmlStringRGB(color);
            return "#FFFFFF";
        }
        private static object? Read(object? target, string name)
        {
            if (target == null) return null;
            var type = target.GetType();
            if (!members.TryGetValue(type, out var named)) members[type] = named = new Dictionary<string, MemberInfo?>();
            if (!named.TryGetValue(name, out var member))
            {
                member = (MemberInfo?)type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                    ?? type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                named[name] = member;
            }
            return member is PropertyInfo property ? property.GetValue(target) : (member as FieldInfo)?.GetValue(target);
        }
        private static object? Invoke(object? target, string name)
        {
            if (target == null) return null;
            var type = target.GetType();
            if (!members.TryGetValue(type, out var named)) members[type] = named = new Dictionary<string, MemberInfo?>();
            if (!named.TryGetValue(name, out var member)) named[name] = member = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            return (member as MethodInfo)?.Invoke(target, null);
        }
        private sealed class ReadPass
        {
            public readonly List<SignalTrackEvent> Signals = new List<SignalTrackEvent>();
            public readonly Dictionary<RailTrack, string> Reservations = new Dictionary<RailTrack, string>();
            public readonly HashSet<string> SeenPlacements = new HashSet<string>(StringComparer.Ordinal);
            public readonly List<string> Problems = new List<string>();
            public int Steps;
            public int UnassociatedControllers;
        }
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
