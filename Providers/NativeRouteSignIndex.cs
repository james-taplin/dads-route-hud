using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DV.Signs;
using HarmonyLib;
using UnityEngine;

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
        private static readonly Type? signDataType = typeof(SignPlacer).GetNestedType("SignData", BindingFlags.NonPublic);
        private static readonly MethodInfo? getTrack = signDataType?.GetMethod("GetTrack");
        private static readonly MethodInfo? getPosition = signDataType?.GetMethod("GetPosition");
        private static readonly MethodInfo? getRotation = signDataType?.GetMethod("GetRotation");
        private static readonly MethodInfo? getSign = signDataType?.GetMethod("GetSign");
        private static readonly FieldInfo? flip = signDataType?.GetField("flip", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo? rawParts = typeof(Sign).GetField("signParameters", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo? trackPlan = typeof(SignPlacer).GetMethod("GetTrackSigns", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(RailTrack), typeof(bool) }, null);
        private static readonly MethodInfo? junctionPlan = typeof(SignPlacer).GetMethod("GetJunctionSigns", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Junction) }, null);
        private static readonly MethodInfo? shouldInclude = typeof(SignPlacer).GetMethod("ShouldIncludeTrack", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly Dictionary<int, Source> sources = new Dictionary<int, Source>();
        private static readonly Dictionary<RailTrack, HashSet<int>> sourceIdsByTrack = new Dictionary<RailTrack, HashSet<int>>();
        private static readonly Dictionary<RailTrack, IReadOnlyList<NativeRouteSign>> cache = new Dictionary<RailTrack, IReadOnlyList<NativeRouteSign>>();
        private static readonly HashSet<RailTrack> watched = new HashSet<RailTrack>();
        private static readonly HashSet<RailTrack> bootstrapped = new HashSet<RailTrack>();
        private static readonly HashSet<int> ambiguousRoots = new HashSet<int>();
        private static readonly HashSet<string> problems = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<Cell, List<ObservedRoot>> observed = new Dictionary<Cell, List<ObservedRoot>>();
        private static readonly Dictionary<int, ObservedRoot> observedById = new Dictionary<int, ObservedRoot>();
        private static readonly Dictionary<RailTrack, PlanCounts> planCounts = new Dictionary<RailTrack, PlanCounts>();
        private static SignPlacer[] placers = Array.Empty<SignPlacer>();
        private static float nextPrune;
        private static Vector3 indexedWorldMove;
        private static bool observedReady;

        public static long SnapshotRevision { get; private set; }
        public static IReadOnlyList<string> Diagnostics => problems.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        public static string Coverage
        {
            get
            {
                var unassigned = observedById.Values.Count(root => root.Root != null && !sources.ContainsKey(root.Root.GetInstanceID()));
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
            Prune();
            EnsureObservedRoots();
            if (watched.Add(track)) track.TrackPointsUpdated += OnTrackPointsUpdated;
            if (bootstrapped.Add(track)) Bootstrap(track);
            if (cache.TryGetValue(track, out var saved)) return saved;
            var entries = new List<NativeRouteSign>();
            var sourceIds = sourceIdsByTrack.TryGetValue(track, out var ids) ? ids.ToArray() : Array.Empty<int>();
            foreach (var sourceId in sourceIds)
            {
                if (!sources.TryGetValue(sourceId, out var source)) continue;
                if (source.Root == null || ambiguousRoots.Contains(source.Id)) continue;
                // The offset cancels the native pole's lateral displacement. Keep the
                // known track association even after a world-origin shift.
                var anchor = source.Root.transform.position + source.AnchorOffset;
                if (!NativeRouteGeometry.TryProjectKnownTrack(track, anchor, out var span))
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
            if (Time.realtimeSinceStartup < nextPrune) return;
            nextPrune = Time.realtimeSinceStartup + 5;
            var changed = false;
            foreach (var item in sources.Values.Where(source => source.Root == null || source.Track == null).ToArray())
            {
                sources.Remove(item.Id);
                RemoveTrackMembership(item);
                if (!ReferenceEquals(item.Track, null)) cache.Remove(item.Track);
                changed = true;
            }
            foreach (var track in watched.Where(track => track == null).ToArray())
            { watched.Remove(track); bootstrapped.Remove(track); cache.Remove(track); planCounts.Remove(track); changed = true; }
            // Re-read raw text and transformed anchors without discarding exact identity.
            cache.Clear();
            observedReady = false;
            if (changed) SnapshotRevision++;
        }

        public static void Reset()
        {
            foreach (var track in watched) if (track != null) track.TrackPointsUpdated -= OnTrackPointsUpdated;
            watched.Clear(); bootstrapped.Clear(); cache.Clear(); sources.Clear(); ambiguousRoots.Clear();
            sourceIdsByTrack.Clear();
            observed.Clear(); observedById.Clear(); problems.Clear(); planCounts.Clear();
            placers = Array.Empty<SignPlacer>();
            observedReady = false;
            nextPrune = 0;
            SnapshotRevision++;
        }

        private static void OnTrackPointsUpdated(RailTrack track)
        {
            cache.Remove(track);
            bootstrapped.Remove(track);
            observedReady = false;
            SnapshotRevision++;
        }

        private static void ObservePlacement(SignPlacer placer, object data, GameObject root)
        {
            if (root == null) return;
            try
            {
                var track = getTrack?.Invoke(data, null) as RailTrack;
                if (track == null || !(getPosition?.Invoke(data, null) is Vector3 anchor)
                    || !(flip?.GetValue(data) is bool isFlipped))
                { Problem("Native sign placement metadata unavailable: " + root.GetInstanceID()); return; }
                var parts = ReadParts(data, root);
                Register(new Source(root, track, !isFlipped, anchor - root.transform.position, parts, true));
                observedReady = false;
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
                    RemoveTrackMembership(old);
                    Problem("Ambiguous native-plan association rejected: sign root " + source.Id);
                    SnapshotRevision++;
                    return;
                }
            }
            sources[source.Id] = source;
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
            if (observedById.Count > 0 && observedById.Keys.All(id => sources.ContainsKey(id))) return;
            if (placers.Length == 0) { Problem("No live native sign placer available for late-enable validation"); return; }
            if (trackPlan == null || junctionPlan == null || shouldInclude == null || getPosition == null || getRotation == null || getTrack == null || flip == null)
            { Problem("Native sign recipe contract unavailable"); return; }
            foreach (var placer in placers)
            {
                if (placer == null) continue;
                try
                {
                    var boundTrack = placer.GetComponent<RailTrack>();
                    var includeTrack = boundTrack != null ? boundTrack == track
                        : shouldInclude.Invoke(null, new object[] { track }) is bool include && include;
                    if (placer.placeTrackSigns && includeTrack)
                    {
                        MatchPlans(placer, trackPlan.Invoke(placer, new object[] { track, false }) as IEnumerable, track, counts);
                        MatchPlans(placer, trackPlan.Invoke(placer, new object[] { track, true }) as IEnumerable, track, counts);
                    }
                    if (placer.placeJunctionSigns)
                    {
                        var boundJunction = placer.GetComponent<Junction>();
                        var junctions = new HashSet<Junction>();
                        if (track.inJunction != null) junctions.Add(track.inJunction);
                        if (track.outJunction != null) junctions.Add(track.outJunction);
                        foreach (var junction in junctions)
                            if (junction.inBranch.track == track && (boundJunction == null || boundJunction == junction))
                                MatchPlans(placer, junctionPlan.Invoke(placer, new object[] { junction }) as IEnumerable, track, counts);
                    }
                }
                catch (Exception exception) { Problem("Native sign validation unavailable on " + track.name + ": " + exception.GetBaseException().Message); }
            }
        }

        private static void MatchPlans(SignPlacer placer, IEnumerable? plans, RailTrack requestedTrack, PlanCounts counts)
        {
            if (plans == null) { Problem("Native sign plan unavailable: " + requestedTrack.name); return; }
            foreach (var data in plans)
            {
                if (data == null) continue;
                var track = getTrack!.Invoke(data, null) as RailTrack;
                if (track == null || track != requestedTrack) continue;
                if (placer.noSignsTrackNameMarks != null && placer.noSignsTrackNameMarks.Any(mark => mark != null && track.name.StartsWith(mark))) continue;
                if (!(getPosition!.Invoke(data, null) is Vector3 anchor) || !(getRotation!.Invoke(data, null) is Quaternion rotation)
                    || !(flip!.GetValue(data) is bool isFlipped))
                { counts.Unmatched++; Problem("Invalid native sign recipe: " + track.name); continue; }
                var expected = anchor + rotation * Vector3.left * placer.sideways;
                var euler = rotation.eulerAngles;
                euler.x = 0;
                var facing = Quaternion.Euler(euler);
                var parts = ReadParts(data, null);
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
            if (observedReady && WorldMover.currentMove == indexedWorldMove) return;
            var previous = observedById.ToDictionary(pair => pair.Key, pair => pair.Value.ContentSignature);
            observed.Clear(); observedById.Clear();
            placers = UnityEngine.Object.FindObjectsOfType<SignPlacer>();
            foreach (var debug in UnityEngine.Object.FindObjectsOfType<SignDebug>())
                AddObserved(debug.gameObject, debug.text);
            foreach (var data in UnityEngine.Object.FindObjectsOfType<SignGeneratorData>())
                AddObserved(data.gameObject, data.GetComponent<SignDebug>()?.text ?? "");
            indexedWorldMove = WorldMover.currentMove;
            observedReady = true;
            if (previous.Count != observedById.Count || observedById.Any(pair =>
                !previous.TryGetValue(pair.Key, out var signature) || signature != pair.Value.ContentSignature))
            {
                // Late-generated or edited signs deserve another validation pass. Do
                // not rebuild native recipes every five seconds on an unchanged map.
                bootstrapped.Clear();
                cache.Clear();
                SnapshotRevision++;
            }
        }
        private static void AddObserved(GameObject root, string text)
        {
            var id = root.GetInstanceID();
            if (observedById.ContainsKey(id)) return;
            var data = root.GetComponent<SignGeneratorData>();
            var entry = new ObservedRoot(root, text, data != null ? data.signParameters : null);
            observedById[id] = entry;
            var key = new Cell(root.transform.position);
            if (!observed.TryGetValue(key, out var group)) observed[key] = group = new List<ObservedRoot>();
            group.Add(entry);
        }
        private static IEnumerable<ObservedRoot> Candidates(Vector3 position)
        {
            var key = new Cell(position);
            for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                    for (var z = -1; z <= 1; z++)
                        if (observed.TryGetValue(new Cell(key.X + x, key.Y + y, key.Z + z), out var group))
                            foreach (var entry in group) yield return entry;
        }

        private static SignParameters[] ReadParts(object data, GameObject? root)
        {
            var generated = root != null ? root.GetComponent<SignGeneratorData>() : null;
            if (generated != null && generated.signParameters != null) return generated.signParameters.ToArray();
            var recipe = getSign?.Invoke(data, null);
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

        [HarmonyPatch(typeof(SignPlacer), "PlaceSign")]
        private static class ObserveNativePlacement
        {
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void Postfix(SignPlacer __instance, object __0, GameObject __result)
                => ObservePlacement(__instance, __0, __result);
        }

        private sealed class Source
        {
            public readonly GameObject Root;
            public readonly int Id;
            public readonly RailTrack Track;
            public readonly bool Forward;
            public readonly Vector3 AnchorOffset;
            public readonly SignParameters[] Parts;
            public readonly bool Captured;
            public Source(GameObject root, RailTrack track, bool forward, Vector3 offset, SignParameters[] parts, bool captured)
            { Root = root; Id = root.GetInstanceID(); Track = track; Forward = forward; AnchorOffset = offset; Parts = parts; Captured = captured; }
        }
        private sealed class ObservedRoot
        {
            public readonly GameObject Root;
            public readonly string Text;
            public readonly SignParameters[]? Parts;
            public readonly string ContentSignature;
            public ObservedRoot(GameObject root, string text, SignParameters[]? parts)
            { Root = root; Text = text; Parts = parts; ContentSignature = parts != null ? Signature(parts) : text; }
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
