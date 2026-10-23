using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DV.Signs;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HeadsUpDisplay
{
    public sealed class NativeRouteSign
    {
        public string SourceId { get; }
        public double Span { get; }
        public bool Forward { get; }
        public RouteEventKind Kind { get; }
        public string Label { get; }
        public string Fact { get; }
        internal NativeRouteSign(string sourceId, double span, bool forward, RouteEventKind kind, string label, string fact)
        { SourceId = sourceId; Span = span; Forward = forward; Kind = kind; Label = label; Fact = fact; }
    }

    /// <summary>
    /// Native sign placement supplies track identity before the game discards it.
    /// Late enablement validates a read-only native recipe against an actual sign root;
    /// an unmatched recipe never becomes an invented posted limit.
    /// </summary>
    public static class NativeRouteSignIndex
    {
        private static readonly Dictionary<Type, PlacerContract> contracts = new Dictionary<Type, PlacerContract>();
        private static readonly FieldInfo? rawParts = typeof(Sign).GetField("signParameters", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly Dictionary<int, Source> sources = new Dictionary<int, Source>();
        private static readonly Dictionary<RailTrack, HashSet<int>> sourceIdsByTrack = new Dictionary<RailTrack, HashSet<int>>();
        private static readonly Dictionary<RailTrack, IReadOnlyList<NativeRouteSign>> cache = new Dictionary<RailTrack, IReadOnlyList<NativeRouteSign>>();
        private static readonly HashSet<RailTrack> watched = new HashSet<RailTrack>();
        private static readonly HashSet<RailTrack> bootstrapped = new HashSet<RailTrack>();
        private static readonly HashSet<int> ambiguousRoots = new HashSet<int>();
        private static readonly HashSet<string> problems = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<Cell, List<ObservedRoot>> observed = new Dictionary<Cell, List<ObservedRoot>>();
        private static readonly Dictionary<int, ObservedRoot> observedById = new Dictionary<int, ObservedRoot>();
        private static readonly List<ObservedRoot> observedRoots = new List<ObservedRoot>();
        private static readonly List<RailTrack> watchedTracks = new List<RailTrack>();
        private static readonly HashSet<int> unassociatedRoots = new HashSet<int>();
        private static readonly Dictionary<RailTrack, PlanCounts> planCounts = new Dictionary<RailTrack, PlanCounts>();
        private static readonly List<Component> placers = new List<Component>();
        private static float nextPrune;
        private static int refreshCursor, refreshRemaining, trackCursor, tracksRemaining;
        private static Type? observedDoubleTrackType;
        private static bool observedReady;

        public static long SnapshotRevision { get; private set; }
        public static IReadOnlyList<string> Diagnostics => problems.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        public static string Coverage
        {
            get
            {
                var unassigned = unassociatedRoots.Count;
                var matchedPlans = planCounts.Values.Sum(value => value.Matched);
                var unmatchedPlans = planCounts.Values.Sum(value => value.Unmatched);
                return "Observed native signs; " + matchedPlans + " validated plan matches; " + unmatchedPlans
                    + " unmatched plans; " + unassigned + " scene roots unassociated"
                    + (problems.Count == 0 && unmatchedPlans == 0 && unassigned == 0 ? "" : "; coverage partial");
            }
        }

        public static IReadOnlyList<NativeRouteSign> EnsureTrack(RailTrack track)
        {
            if (track == null) return Array.Empty<NativeRouteSign>();
            EnsureObservedRoots();
            Prune();
            if (watched.Add(track)) { watchedTracks.Add(track); track.TrackPointsUpdated += OnTrackPointsUpdated; }
            if (bootstrapped.Add(track))
                using (HudTimings.Measure(HudTimingStage.SignBootstrap)) Bootstrap(track);
            if (cache.TryGetValue(track, out var saved)) return saved;
            var entries = new List<NativeRouteSign>();
            var sourceIds = sourceIdsByTrack.TryGetValue(track, out var ids) ? ids.ToArray() : Array.Empty<int>();
            foreach (var sourceId in sourceIds)
            {
                if (!sources.TryGetValue(sourceId, out var source)) continue;
                if (source.Root == null || ambiguousRoots.Contains(source.Id)) continue;
                // Placement supplied the actual track anchor. Preserve it independently
                // of later clearance adjustments to the visible pole, and evaluate it
                // in the current scene frame after origin shifts.
                var anchor = source.Anchor + WorldMover.currentMove;
                double span;
                bool projected;
                using (HudTimings.Measure(HudTimingStage.SignProjection))
                    projected = NativeRouteGeometry.TryProjectKnownTrack(track, anchor, out span);
                if (!projected)
                {
                    Problem("Known-track sign projection unavailable: " + source.Id);
                    continue;
                }
                var data = source.Root.GetComponent<SignGeneratorData>();
                var parts = data != null && data.signParameters != null ? data.signParameters : source.Parts;
                AppendParts(entries, source, parts, span);
            }
            saved = Array.AsReadOnly(entries.OrderBy(item => item.Span).ThenBy(item => item.SourceId, StringComparer.Ordinal).ToArray());
            cache[track] = saved;
            return saved;
        }

        public static void Prune()
        {
            if (!observedReady) return;
            using (HudTimings.Measure(HudTimingStage.SignMaintenance))
            {
                if (refreshRemaining == 0 && tracksRemaining == 0)
                {
                    if (Time.realtimeSinceStartup < nextPrune) return;
                    nextPrune = Time.realtimeSinceStartup + 5;
                    RefreshOptionalPlacer();
                    refreshCursor = trackCursor = 0;
                    refreshRemaining = observedRoots.Count;
                    tracksRemaining = watchedTracks.Count;
                }
                // One collector step must not reread thousands of roots. Finish this
                // sweep across subsequent steps without truncating the retained index.
                using (HudTimings.Measure(HudTimingStage.SignRootRefresh))
                    for (var work = 0; work < 32 && refreshRemaining > 0 && refreshCursor < observedRoots.Count; work++)
                    {
                        var entry = observedRoots[refreshCursor];
                        refreshRemaining--;
                        if (entry.Root == null) { RemoveObserved(entry); continue; }
                        if (sources.TryGetValue(entry.Id, out var source) && source.Track == null)
                            RemoveSource(source);
                        var moved = entry.Position != entry.Root.transform.position - WorldMover.currentMove;
                        if (moved) MoveObserved(entry);
                        if (entry.Refresh() || moved && !sources.ContainsKey(entry.Id))
                        {
                            if (sources.TryGetValue(entry.Id, out source)) cache.Remove(source.Track);
                            else InvalidateUnassociatedPlans();
                            SnapshotRevision++;
                        }
                        refreshCursor++;
                    }
                for (var work = 0; work < 16 && tracksRemaining > 0 && trackCursor < watchedTracks.Count; work++)
                {
                    var track = watchedTracks[trackCursor];
                    tracksRemaining--;
                    if (track != null) { trackCursor++; continue; }
                    watched.Remove(track!); bootstrapped.Remove(track!); cache.Remove(track!); planCounts.Remove(track!);
                    var last = watchedTracks.Count - 1;
                    watchedTracks[trackCursor] = watchedTracks[last]; watchedTracks.RemoveAt(last);
                    SnapshotRevision++;
                }
            }
        }

        public static void Reset()
        {
            foreach (var track in watched) if (track != null) track.TrackPointsUpdated -= OnTrackPointsUpdated;
            watched.Clear(); bootstrapped.Clear(); cache.Clear(); sources.Clear(); ambiguousRoots.Clear();
            sourceIdsByTrack.Clear();
            observed.Clear(); observedById.Clear(); observedRoots.Clear(); watchedTracks.Clear(); unassociatedRoots.Clear();
            problems.Clear(); planCounts.Clear(); placers.Clear();
            observedReady = false;
            observedDoubleTrackType = null;
            refreshCursor = refreshRemaining = trackCursor = tracksRemaining = 0;
            nextPrune = 0;
            SnapshotRevision++;
        }

        private static void OnTrackPointsUpdated(RailTrack track)
        {
            cache.Remove(track);
            bootstrapped.Remove(track);
            SnapshotRevision++;
        }

        private static void ObservePlacement(Component placer, object data, GameObject root)
        {
            if (root == null) return;
            try
            {
                var contract = Contract(placer.GetType());
                var track = contract.GetTrack?.Invoke(data, null) as RailTrack;
                if (track == null || !(contract.GetPosition?.Invoke(data, null) is Vector3 anchor)
                    || !(contract.Flip?.GetValue(data) is bool isFlipped))
                { Problem("Native sign placement metadata unavailable: " + root.GetInstanceID()); return; }
                var parts = ReadParts(contract, data, root);
                AddPlacer(placer);
                using (HudTimings.Measure(HudTimingStage.SignIndexRoots)) AddObserved(root, "");
                Register(new Source(root, track, !isFlipped, anchor - root.transform.position, parts, true));
            }
            catch (Exception exception) { Problem("Native sign observation failed: " + exception.GetBaseException().Message); }
        }

        private static void Register(Source source)
        {
            if (ambiguousRoots.Contains(source.Id)) return;
            if (sources.TryGetValue(source.Id, out var old) && old.Track != source.Track)
            {
                if (source.Captured) { cache.Remove(old.Track); RemoveTrackMembership(old); }
                else if (old.Captured) return;
                else
                {
                    sources.Remove(source.Id); ambiguousRoots.Add(source.Id); cache.Remove(old.Track);
                    if (observedById.ContainsKey(source.Id)) unassociatedRoots.Add(source.Id);
                    RemoveTrackMembership(old);
                    Problem("Ambiguous native-plan association rejected: sign root " + source.Id);
                    SnapshotRevision++;
                    return;
                }
            }
            sources[source.Id] = source;
            unassociatedRoots.Remove(source.Id);
            if (!sourceIdsByTrack.TryGetValue(source.Track, out var ids))
                sourceIdsByTrack[source.Track] = ids = new HashSet<int>();
            ids.Add(source.Id);
            cache.Remove(source.Track);
            SnapshotRevision++;
        }

        private static void RemoveTrackMembership(Source source)
        {
            if (ReferenceEquals(source.Track, null) || !sourceIdsByTrack.TryGetValue(source.Track, out var ids)) return;
            ids.Remove(source.Id);
            if (ids.Count == 0) sourceIdsByTrack.Remove(source.Track);
        }

        private static void Bootstrap(RailTrack track)
        {
            EnsureObservedRoots();
            var counts = planCounts[track] = new PlanCounts();
            // With normal startup capture every observed native root already has an
            // exact association. A second geometric recipe pass adds no evidence.
            if (observedById.Count > 0 && unassociatedRoots.Count == 0) return;
            if (placers.Count == 0) { Problem("No live native sign placer available for late-enable validation"); return; }
            foreach (var placer in placers)
            {
                if (placer == null) continue;
                try
                {
                    var contract = Contract(placer.GetType());
                    if (!contract.Ready) { Problem("Sign recipe contract unavailable: " + placer.GetType().FullName); continue; }
                    var boundTrack = placer.GetComponent<RailTrack>();
                    var includeTrack = boundTrack != null ? boundTrack == track
                        : contract.ShouldInclude!.Invoke(null, new object[] { track }) is bool include && include;
                    if (contract.PlaceTrackSigns!.GetValue(placer) is true && includeTrack)
                    {
                        MatchPlans(placer, contract, contract.TrackPlan!.Invoke(placer, new object[] { track, false }) as IEnumerable, track, counts);
                        MatchPlans(placer, contract, contract.TrackPlan.Invoke(placer, new object[] { track, true }) as IEnumerable, track, counts);
                    }
                    if (contract.PlaceJunctionSigns!.GetValue(placer) is true)
                    {
                        var boundJunction = placer.GetComponent<Junction>();
                        var junctions = new HashSet<Junction>();
                        if (track.inJunction != null) junctions.Add(track.inJunction);
                        if (track.outJunction != null) junctions.Add(track.outJunction);
                        foreach (var junction in junctions)
                            if (junction.inBranch.track == track && (boundJunction == null || boundJunction == junction))
                                MatchPlans(placer, contract, contract.JunctionPlan!.Invoke(placer, new object[] { junction }) as IEnumerable, track, counts);
                    }
                }
                catch (Exception exception) { Problem("Native sign validation unavailable on " + track.name + ": " + exception.GetBaseException().Message); }
            }
        }

        private static void MatchPlans(Component placer, PlacerContract contract, IEnumerable? plans, RailTrack requestedTrack, PlanCounts counts)
        {
            if (plans == null) { Problem("Native sign plan unavailable: " + requestedTrack.name); return; }
            foreach (var data in plans)
            {
                if (data == null) continue;
                var track = contract.GetTrack!.Invoke(data, null) as RailTrack;
                if (track == null || track != requestedTrack) continue;
                var excluded = contract.NoSignsMarks!.GetValue(placer) as IEnumerable<string>;
                if (excluded != null && excluded.Any(mark => mark != null && track.name.StartsWith(mark))) continue;
                if (!(contract.GetPosition!.Invoke(data, null) is Vector3 anchor) || !(contract.GetRotation!.Invoke(data, null) is Quaternion rotation)
                    || !(contract.Flip!.GetValue(data) is bool isFlipped))
                { counts.Unmatched++; Problem("Invalid native sign recipe: " + track.name); continue; }
                if (!(contract.Sideways!.GetValue(placer) is float sideways))
                { counts.Unmatched++; Problem("Native sign sideways offset unavailable: " + track.name); continue; }
                var expected = anchor + rotation * Vector3.left * sideways;
                var euler = rotation.eulerAngles;
                euler.x = 0;
                var facing = Quaternion.Euler(euler);
                var parts = ReadParts(contract, data, null);
                var signature = Signature(parts);
                var text = VisibleText(parts);
                var candidates = Candidates(expected).Where(root => root.Root != null
                    && !ambiguousRoots.Contains(root.Root.GetInstanceID())
                    && Vector3.SqrMagnitude(root.Root.transform.position - expected) <= 0.0625f
                    && Quaternion.Angle(root.Root.transform.rotation, facing) <= 1
                    && (root.Parts != null ? root.ContentSignature == signature : root.Text == text)).ToArray();
                if (candidates.Length != 1)
                {
                    counts.Unmatched++;
                    Problem((candidates.Length == 0 ? "Unmatched" : "Ambiguous") + " native sign recipe on " + track.name
                        + " at " + anchor.ToString("F2") + "; no event invented");
                    continue;
                }
                var rootObject = candidates[0].Root;
                var id = rootObject.GetInstanceID();
                if (sources.TryGetValue(id, out var captured) && captured.Captured)
                {
                    if (captured.Track == track) counts.Matched++;
                    else { counts.Unmatched++; Problem("Native recipe disagrees with captured identity: " + id); }
                    continue;
                }
                Register(new Source(rootObject, track, !isFlipped, anchor - rootObject.transform.position, parts, false));
                counts.Matched++;
            }
        }

        private static void EnsureObservedRoots()
        {
            if (observedReady) return;
            // Existing roots require one late-enable census. Both supported native
            // placers subsequently pass every generated root through ObservePlacement.
            using (HudTimings.Measure(HudTimingStage.SignDiscoveryNativePlacers))
                foreach (var placer in UnityEngine.Object.FindObjectsOfType<SignPlacer>()) AddPlacer(placer);
            var doubleTrack = DoubleTrackPlacerType();
            if (doubleTrack != null)
                DiscoverOptionalPlacers(doubleTrack);
            DiscoverExistingRoots();
            observedDoubleTrackType = doubleTrack;
            observedReady = true;
        }
        private static void RefreshOptionalPlacer()
        {
            var type = DoubleTrackPlacerType();
            if (type == observedDoubleTrackType) return;
            observedDoubleTrackType = type;
            if (type != null)
            {
                DiscoverOptionalPlacers(type);
                // The optional patch may have been unavailable at HUD load. Recover
                // its already-created roots once when that source becomes available.
                DiscoverExistingRoots();
            }
            InvalidateUnassociatedPlans();
            SnapshotRevision++;
        }
        private static void DiscoverOptionalPlacers(Type type)
        {
            EnsureOptionalObserver(type);
            using (HudTimings.Measure(HudTimingStage.SignDiscoveryDoubleTrackPlacers))
                foreach (var placer in UnityEngine.Object.FindObjectsOfType(type).OfType<Component>()) AddPlacer(placer);
        }
        private static void EnsureOptionalObserver(Type type)
        {
            try
            {
                var contract = Contract(type);
                var postfix = typeof(ObserveDoubleTrackPlacement).GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static)!;
                if (!contract.Ready || contract.PlaceSign?.ReturnType != typeof(GameObject))
                { Problem("DoubleTrack sign placement contract unavailable; coverage partial"); return; }
                const string owner = "DadsRouteHUD";
                var patches = Harmony.GetPatchInfo(contract.PlaceSign);
                if (patches != null && patches.Postfixes.Any(patch => patch.owner == owner && patch.PatchMethod == postfix)) return;
                // PatchAll can legitimately skip this optional target while the mod
                // is inactive. Attach the same checked passive observer on activation.
                new Harmony(owner).Patch(contract.PlaceSign, postfix: new HarmonyMethod(postfix) { priority = Priority.Last });
            }
            catch (Exception exception)
            { Problem("DoubleTrack placement observer unavailable: " + exception.GetBaseException().Message + "; coverage partial"); }
        }
        private static void DiscoverExistingRoots()
        {
            SignDebug[] debugRoots;
            SignGeneratorData[] generatedRoots;
            using (HudTimings.Measure(HudTimingStage.SignDiscoveryDebugRoots))
                debugRoots = UnityEngine.Object.FindObjectsOfType<SignDebug>();
            using (HudTimings.Measure(HudTimingStage.SignDiscoveryGeneratorRoots))
                generatedRoots = UnityEngine.Object.FindObjectsOfType<SignGeneratorData>();
            using (HudTimings.Measure(HudTimingStage.SignIndexRoots))
            {
                foreach (var debug in debugRoots) AddObserved(debug.gameObject, debug.text);
                foreach (var data in generatedRoots) AddObserved(data.gameObject, "");
            }
        }
        private static void AddPlacer(Component placer)
        {
            if (placers.Contains(placer)) return;
            placers.Add(placer);
            if (observedReady && unassociatedRoots.Count > 0) InvalidateUnassociatedPlans();
        }
        private static void InvalidateUnassociatedPlans()
        {
            bootstrapped.Clear();
            cache.Clear();
        }
        private static void AddObserved(GameObject root, string text)
        {
            var id = root.GetInstanceID();
            if (observedById.ContainsKey(id)) return;
            var entry = new ObservedRoot(root, id, observedRoots.Count, text);
            observedById[id] = entry;
            observedRoots.Add(entry);
            if (!sources.ContainsKey(id)) unassociatedRoots.Add(id);
            var key = new Cell(entry.Position);
            if (!observed.TryGetValue(key, out var group)) observed[key] = group = new List<ObservedRoot>();
            group.Add(entry);
        }
        private static void MoveObserved(ObservedRoot entry)
        {
            RemoveFromCell(entry);
            entry.Position = entry.Root.transform.position - WorldMover.currentMove;
            var key = new Cell(entry.Position);
            if (!observed.TryGetValue(key, out var group)) observed[key] = group = new List<ObservedRoot>();
            group.Add(entry);
        }
        private static void RemoveFromCell(ObservedRoot entry)
        {
            var key = new Cell(entry.Position);
            if (!observed.TryGetValue(key, out var group)) return;
            group.Remove(entry);
            if (group.Count == 0) observed.Remove(key);
        }
        private static void RemoveObserved(ObservedRoot entry)
        {
            RemoveFromCell(entry);
            observedById.Remove(entry.Id);
            if (unassociatedRoots.Remove(entry.Id)) InvalidateUnassociatedPlans();
            var last = observedRoots.Count - 1;
            var replacement = observedRoots[last];
            observedRoots[entry.Index] = replacement;
            replacement.Index = entry.Index;
            observedRoots.RemoveAt(last);
            if (sources.TryGetValue(entry.Id, out var source)) RemoveSource(source);
            SnapshotRevision++;
        }
        private static void RemoveSource(Source source)
        {
            sources.Remove(source.Id);
            RemoveTrackMembership(source);
            if (!ReferenceEquals(source.Track, null)) cache.Remove(source.Track);
            if (observedById.ContainsKey(source.Id) && unassociatedRoots.Add(source.Id)) InvalidateUnassociatedPlans();
            SnapshotRevision++;
        }
        private static IEnumerable<ObservedRoot> Candidates(Vector3 position)
        {
            // Origin-relative cells stay valid when the entire scene is shifted.
            var key = new Cell(position - WorldMover.currentMove);
            for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                    for (var z = -1; z <= 1; z++)
                        if (observed.TryGetValue(new Cell(key.X + x, key.Y + y, key.Z + z), out var group))
                            foreach (var entry in group) yield return entry;
        }

        private static SignParameters[] ReadParts(PlacerContract contract, object data, GameObject? root)
        {
            var generated = root != null ? root.GetComponent<SignGeneratorData>() : null;
            if (generated != null && generated.signParameters != null) return generated.signParameters.ToArray();
            var recipe = contract.GetSign?.Invoke(data, null);
            var values = recipe != null ? rawParts?.GetValue(recipe) as IEnumerable<SignParameters> : null;
            return values?.ToArray() ?? Array.Empty<SignParameters>();
        }
        private static string Signature(IEnumerable<SignParameters> parts)
            => string.Join("\n", parts.Select(part => ((int)part.type).ToString(CultureInfo.InvariantCulture) + ":" + (part.signText ?? "")
                + ":prefab=" + (part.sign != null ? part.sign.GetInstanceID().ToString(CultureInfo.InvariantCulture) : "<null>")
                + ":" + string.Join(",", (part.accessories ?? Array.Empty<GameObject>()).Select(item => item != null ? item.GetInstanceID().ToString(CultureInfo.InvariantCulture) : "<null>"))));
        private static string VisibleText(IEnumerable<SignParameters> parts)
            => string.Join("\n", parts.Select(part => part.signText).Where(text => !string.IsNullOrWhiteSpace(text)));
        private static void AppendParts(List<NativeRouteSign> result, Source source, SignParameters[] parts, double span)
        {
            var identity = "native-sign:" + source.Id.ToString(CultureInfo.InvariantCulture);
            var evidence = source.Captured ? "Captured native placement" : "Native recipe matched to observed sign";
            if (parts.Length == 0)
                result.Add(new NativeRouteSign(identity + ":pole", span, source.Forward, RouteEventKind.OtherSign,
                    "Sign pole · no parameters retained", evidence));
            for (var index = 0; index < parts.Length; index++)
            {
                var part = parts[index];
                var type = part.type.ToString();
                var kind = type.StartsWith("SpeedLimit", StringComparison.Ordinal) ? RouteEventKind.Speed
                    : type.StartsWith("Grade", StringComparison.Ordinal) ? RouteEventKind.Grade : RouteEventKind.OtherSign;
                var label = RouteNaming.Words(type)
                    + (string.IsNullOrWhiteSpace(part.signText) ? "" : " · " + part.signText);
                if (kind == RouteEventKind.Speed && TryNumber(part.signText, out var limit))
                    label = (limit * 10).ToString("0.###", CultureInfo.InvariantCulture) + " km/h";
                else if (kind == RouteEventKind.Grade && TryNumber(part.signText, out var grade))
                    label = RouteNaming.GradeText(grade);
                var fact = evidence + "; native type " + type + "; raw text: " + (part.signText ?? "<null>")
                    + "; prefab: " + (part.sign != null ? part.sign.name : "<unavailable>");
                result.Add(new NativeRouteSign(identity + ":part:" + index, span, source.Forward, kind, label, fact));
                if (part.accessories == null) continue;
                for (var accessory = 0; accessory < part.accessories.Length; accessory++)
                {
                    var item = part.accessories[accessory];
                    result.Add(new NativeRouteSign(identity + ":part:" + index + ":accessory:" + accessory, span, source.Forward,
                        RouteEventKind.OtherSign, "Accessory · " + (item != null ? RouteNaming.Words(item.name) : "unavailable"), fact));
                }
            }
        }
        private static bool TryNumber(string text, out double value)
            => (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                && !double.IsNaN(value) && !double.IsInfinity(value);
        private static void Problem(string message) { if (problems.Add(message)) SnapshotRevision++; }

        private static Type? DoubleTrackPlacerType()
        {
            // DoubleTrack replaces SignPlacer rather than calling its PlaceSign.
            // Bind only the existing active optional mod; never add a static dependency.
            var mod = UnityModManager.FindMod("DoubleTrack");
            return mod != null && mod.Loaded && mod.Active
                ? mod.Assembly?.GetType("DV.Signs.CustomSignPlacer", false) : null;
        }
        private static PlacerContract Contract(Type type)
        {
            if (typeof(SignPlacer).IsAssignableFrom(type)) type = typeof(SignPlacer);
            if (!contracts.TryGetValue(type, out var contract))
                contracts[type] = contract = new PlacerContract(type);
            return contract;
        }

        private sealed class PlacerContract
        {
            public readonly MethodInfo? GetTrack, GetPosition, GetRotation, GetSign, TrackPlan, JunctionPlan, ShouldInclude, PlaceSign;
            public readonly FieldInfo? Flip, Sideways, PlaceTrackSigns, PlaceJunctionSigns, NoSignsMarks;
            public readonly bool Ready;
            public PlacerContract(Type type)
            {
                const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var data = type.GetNestedType("SignData", BindingFlags.NonPublic);
                GetTrack = data?.GetMethod("GetTrack");
                GetPosition = data?.GetMethod("GetPosition");
                GetRotation = data?.GetMethod("GetRotation");
                GetSign = data?.GetMethod("GetSign");
                Flip = data?.GetField("flip", instance);
                TrackPlan = type.GetMethod("GetTrackSigns", instance, null, new[] { typeof(RailTrack), typeof(bool) }, null);
                JunctionPlan = type.GetMethod("GetJunctionSigns", instance, null, new[] { typeof(Junction) }, null);
                ShouldInclude = type.GetMethod("ShouldIncludeTrack", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(RailTrack) }, null);
                PlaceSign = data == null ? null : type.GetMethod("PlaceSign", instance, null, new[] { data }, null);
                Sideways = type.GetField("sideways", instance);
                PlaceTrackSigns = type.GetField("placeTrackSigns", instance);
                PlaceJunctionSigns = type.GetField("placeJunctionSigns", instance);
                NoSignsMarks = type.GetField("noSignsTrackNameMarks", instance);
                Ready = typeof(Component).IsAssignableFrom(type)
                    && GetTrack?.ReturnType == typeof(RailTrack) && GetPosition?.ReturnType == typeof(Vector3)
                    && GetRotation?.ReturnType == typeof(Quaternion) && GetSign?.ReturnType == typeof(Sign)
                    && Flip?.FieldType == typeof(bool) && Sideways?.FieldType == typeof(float)
                    && PlaceTrackSigns?.FieldType == typeof(bool) && PlaceJunctionSigns?.FieldType == typeof(bool)
                    && NoSignsMarks != null && typeof(IEnumerable<string>).IsAssignableFrom(NoSignsMarks.FieldType)
                    && TrackPlan != null && typeof(IEnumerable).IsAssignableFrom(TrackPlan.ReturnType)
                    && JunctionPlan != null && typeof(IEnumerable).IsAssignableFrom(JunctionPlan.ReturnType)
                    && ShouldInclude?.ReturnType == typeof(bool);
            }
        }

        [HarmonyPatch(typeof(SignPlacer), "PlaceSign")]
        private static class ObserveNativePlacement
        {
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void Postfix(SignPlacer __instance, object __0, GameObject __result)
                => ObservePlacement(__instance, __0, __result);
        }

        [HarmonyPatch]
        private static class ObserveDoubleTrackPlacement
        {
            private static bool Prepare()
            {
                var type = DoubleTrackPlacerType();
                if (type == null) return false;
                var contract = Contract(type);
                if (contract.Ready && contract.PlaceSign?.ReturnType == typeof(GameObject)) return true;
                Problem("DoubleTrack sign placement contract unavailable; coverage partial");
                return false;
            }
            private static MethodBase? TargetMethod()
                => DoubleTrackPlacerType() is Type type ? Contract(type).PlaceSign : null;
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void Postfix(Component __instance, object __0, GameObject __result)
                => ObservePlacement(__instance, __0, __result);
        }

        private sealed class Source
        {
            public readonly GameObject Root;
            public readonly int Id;
            public readonly RailTrack Track;
            public readonly bool Forward;
            public readonly Vector3 Anchor;
            public readonly SignParameters[] Parts;
            public readonly bool Captured;
            public Source(GameObject root, RailTrack track, bool forward, Vector3 offset, SignParameters[] parts, bool captured)
            {
                Root = root; Id = root.GetInstanceID(); Track = track; Forward = forward;
                Anchor = root.transform.position + offset - WorldMover.currentMove;
                Parts = parts; Captured = captured;
            }
        }
        private sealed class ObservedRoot
        {
            public readonly GameObject Root;
            public readonly int Id;
            public int Index;
            public Vector3 Position;
            public string Text = "", ContentSignature = "";
            public SignParameters[]? Parts;
            private PartContent[]? content;
            public ObservedRoot(GameObject root, int id, int index, string text)
            {
                Root = root; Id = id; Index = index;
                Position = root.transform.position - WorldMover.currentMove;
                ReadContent(text);
            }
            public bool Refresh() => ReadContent(Root.GetComponent<SignDebug>()?.text ?? "");
            private bool ReadContent(string text)
            {
                var data = Root.GetComponent<SignGeneratorData>();
                var parts = data != null ? data.signParameters : null;
                if (parts == null && content == null && Text == text) return false;
                if (parts != null && content != null && parts.Length == content.Length)
                {
                    var same = true;
                    for (var i = 0; i < parts.Length && same; i++) same = content[i].Matches(parts[i]);
                    if (same) return false;
                }
                Parts = parts; Text = text;
                content = parts?.Select(part => new PartContent(part)).ToArray();
                ContentSignature = parts != null ? Signature(parts) : text;
                return true;
            }
        }
        private sealed class PartContent
        {
            private readonly int type, prefab;
            private readonly string text;
            private readonly int[] accessories;
            public PartContent(SignParameters part)
            {
                type = (int)part.type; text = part.signText;
                prefab = Identity(part.sign);
                accessories = (part.accessories ?? Array.Empty<GameObject>()).Select(Identity).ToArray();
            }
            public bool Matches(SignParameters part)
            {
                if (type != (int)part.type || text != part.signText || prefab != Identity(part.sign)) return false;
                var values = part.accessories ?? Array.Empty<GameObject>();
                if (values.Length != accessories.Length) return false;
                for (var i = 0; i < values.Length; i++) if (accessories[i] != Identity(values[i])) return false;
                return true;
            }
            private static int Identity(UnityEngine.Object? value) => value != null ? value.GetInstanceID() : 0;
        }
        private sealed class PlanCounts { public int Matched, Unmatched; }
        private readonly struct Cell : IEquatable<Cell>
        {
            public readonly int X, Y, Z;
            public Cell(Vector3 position) : this(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), Mathf.FloorToInt(position.z)) { }
            public Cell(int x, int y, int z) { X = x; Y = y; Z = z; }
            public bool Equals(Cell other) => X == other.X && Y == other.Y && Z == other.Z;
            public override bool Equals(object? value) => value is Cell other && Equals(other);
            public override int GetHashCode() { unchecked { return (X * 397 ^ Y) * 397 ^ Z; } }
        }
    }
}
