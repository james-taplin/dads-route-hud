using DV.PointSet;
using DV.Signs;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    public static class TrackIndexer
    {
        internal const int SIGN_COLLIDER_LAYER = 30;
        private const float SIMPLIFIED_RESOLUTION = 10f;

        private static readonly Dictionary<RailTrack, List<TrackEvent>> indexedTracks =
            new Dictionary<RailTrack, List<TrackEvent>>();
        private static readonly Dictionary<GameObject, GameObject> signProbes = new Dictionary<GameObject, GameObject>();
        private static readonly HashSet<Collider> ownedColliders = new HashSet<Collider>();
        private static readonly HashSet<RailTrack> watchedTracks = new HashSet<RailTrack>();
        private static float nextRefresh;

        public static void Clear()
        {
            indexedTracks.Clear();
            TrackFollower.Clear();
            nextRefresh = 0;
        }

        public static void Shutdown()
        {
            foreach (var track in watchedTracks)
                if (track != null) track.TrackPointsUpdated -= OnTrackPointsUpdated;
            watchedTracks.Clear();
            foreach (var probe in signProbes.Values)
                if (probe != null) UnityEngine.Object.Destroy(probe);
            signProbes.Clear();
            ownedColliders.Clear();
            Clear();
        }

        // Streamed signs and optional route mods can appear after LoadingFinished.
        // A modest refresh also expires geometry caches after signs are repositioned.
        public static void Refresh()
        {
            if (Time.realtimeSinceStartup < nextRefresh) return;
            nextRefresh = Time.realtimeSinceStartup + 5f;
            indexedTracks.Clear();
            TrackFollower.Clear();
            foreach (var pair in signProbes.ToArray())
                if (pair.Key == null || pair.Value == null) signProbes.Remove(pair.Key!);
            ownedColliders.RemoveWhere(collider => collider == null);
            watchedTracks.RemoveWhere(track => track == null);
            foreach (var data in UnityEngine.Object.FindObjectsOfType<SignGeneratorData>()) RegisterSign(data);
            foreach (var debug in UnityEngine.Object.FindObjectsOfType<SignDebug>())
                RegisterProbe(debug.gameObject, debug.text);
        }

        private static void OnTrackPointsUpdated(RailTrack track)
        {
            indexedTracks.Remove(track);
            TrackFollower.Clear();
        }

        public static IEnumerable<TrackEvent> GetTrackEvents(RailTrack track)
        {
            if (!indexedTracks.TryGetValue(track, out var data))
            {
                if (watchedTracks.Add(track)) track.TrackPointsUpdated += OnTrackPointsUpdated;
                data = indexedTracks[track] = GenerateTrackEvents(track).ToList();
            }
            return data;
        }

        public static IEnumerable<TrackEvent> GetTrackEvents(RailTrack track, bool first, double start)
        {
            var allTrackEvents = GetTrackEvents(track);
            return allTrackEvents.RelativeFromSpan(start, first);
        }

        private static IEnumerable<TrackEvent> ParseSign(string colliderName, bool direction, double span)
        {
            string[] parts = colliderName.Split('\n');
            switch (parts.Length)
            {
                case 1:
                    if (int.TryParse(parts[0], out var limit))
                        yield return new SpeedLimitEvent(span, direction, limit * 10);
                    break;

                case 2:
                    if (int.TryParse(parts[0], out var top))
                    {
                        if (parts[1].Length > 0 && (parts[1][0] == '+' || parts[1][0] == '-'))
                        {
                            yield return new SpeedLimitEvent(span, direction, top * 10);
                            if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var grade))
                                yield return new GradeEvent(span, direction, grade);
                        }
                        else if (int.TryParse(parts[1], out var bottom))
                        {
                            yield return new DualSpeedLimitEvent(span, direction, top * 10, bottom * 10);
                        }
                    }
                    else if (int.TryParse(parts[1], out var bottom))
                    {
                        yield return new SpeedLimitEvent(span, direction, bottom * 10);
                    }
                    else
                    {
                        Main.DebugLog($"Unable to parse sign: \"{colliderName.Replace("\n", "\\n")}\"");
                    }
                    break;
            }
        }

        public static float Grade(EquiPointSet.Point point)
        {
            return Mathf.RoundToInt(point.forward.y * 200) / 2f;
        }

        private static IEnumerable<TrackEvent> FindSigns(EquiPointSet.Point point)
        {
            var hits = Physics.RaycastAll(
                new Ray((Vector3)point.position + WorldMover.currentMove, point.forward),
                (float)point.spanToNextPoint,
                1 << SIGN_COLLIDER_LAYER,
                QueryTriggerInteraction.Collide);

            foreach (var hit in hits)
            {
                if (!ownedColliders.Contains(hit.collider)) continue;
                var dp = Vector3.Dot(hit.collider.transform.forward, point.forward);
                bool direction = dp < 0f;
                foreach (var trackEvent in ParseSign(hit.collider.name, direction, point.span + hit.distance))
                    yield return trackEvent;
            }
        }

        private static IEnumerable<TrackEvent> GenerateTrackEvents(RailTrack track)
        {
            var pointSet = track.GetKinkedPointSet();
            if (pointSet == null || pointSet.span <= 0) yield break;
            EquiPointSet simplified = EquiPointSet.ResampleEquidistant(
                pointSet,
                Mathf.Min(SIMPLIFIED_RESOLUTION, (float)pointSet.span / 3));

            foreach (var point in simplified.points)
            {
                foreach (var trackEvent in FindSigns(point))
                    yield return trackEvent;
            }
        }

        // Legacy text-list support is retained as source only. This fork uses exact native
        // placement observations; do not register the earlier sign-probe scene patch.
        public static class AddSceneGOPatch
        {
            public static void Postfix(GameObject sceneGO)
            {
                if (!Main.enabled || !Main.settings.trackInfoSettings.enabled) return;
                foreach (var data in sceneGO.GetComponentsInChildren<SignGeneratorData>()) RegisterSign(data);
                foreach (var debug in sceneGO.GetComponentsInChildren<SignDebug>()) RegisterProbe(debug.gameObject, debug.text);
                Clear();
            }
        }

        private static void RegisterSign(SignGeneratorData data)
        {
            if (data.signParameters == null) return;
            var text = string.Join("\n", data.signParameters.Select(parameter => parameter.signText)
                .Where(line => !string.IsNullOrWhiteSpace(line)));
            RegisterProbe(data.gameObject, text);
        }

        private static void RegisterProbe(GameObject sign, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (signProbes.TryGetValue(sign, out var existing) && existing != null)
            {
                existing.name = text;
                return;
            }
            // Native post-generation metadata does not retain the associated track/span.
            // Preserve upstream's ray geometry instead of assigning a nearest parallel
            // track. Own an isolated child so sign layers and colliders remain untouched.
            var probe = new GameObject(text) { layer = SIGN_COLLIDER_LAYER };
            probe.transform.SetParent(sign.transform, false);
            var collider = probe.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(2f, 0f, 0f);
            collider.radius = 0.5f;
            collider.height = 10f;
            collider.direction = 1;
            collider.isTrigger = true;
            ownedColliders.Add(collider);
            signProbes[sign] = probe;
            indexedTracks.Clear();
        }
    }
}
