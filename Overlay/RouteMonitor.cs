using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>Passive route geometry plus an explicitly opened, complete UMM inspector.</summary>
    public static class RouteMonitor
    {
        private sealed class Cluster
        {
            public RouteEvent First = null!;
            public int Count;
            public double MeanDistance;
        }

        private static RouteSnapshot<RailTrack> snapshot = RouteSnapshot<RailTrack>.Empty();
        private static readonly List<Cluster> nearClusters = new List<Cluster>();
        private static readonly List<Cluster> overviewClusters = new List<Cluster>();
        private static readonly List<List<Cluster>> atlasClusters = new List<List<Cluster>>();
        private static float width;
        private static float line;
        private static double nearDistance;
        private static bool expanded, showStatus, showNear, showOverview, showNext;
        private static double fullRange;
        private static float profileHeight;
        private static int previousKey = -1;
        private static int previousFont, previousNear, previousProfile;
        private static Settings.TrackInfoSettings.EventDistanceUnits previousUnits;
        private static string status = "";
        private static string nextEvent = "";
        private static Color nextEventColor = Color.white;
        private static int requestedPage;
        private static RouteSnapshot<RailTrack>? inspectorSnapshot;
        private static RoutePage<RouteEvent>? inspectorPage;
        private static Vector2 inspectorScroll;
        private static GUIStyle? inspectorText;
        private static GUIStyle? inspectorHeading;
        public static float Height { get; private set; }
        public static float PlotBackgroundOpacity { get; set; } = 0.72f;

        public static void PrepareLayout(RouteSnapshot<RailTrack> current, float availableWidth, int fontSize, Settings.LayoutSettings settings)
        {
            var flags = (settings.expandedAtlas ? 1 : 0) | (settings.showRouteStatus ? 2 : 0)
                | (settings.showNearProfile ? 4 : 0) | (settings.showOverview ? 8 : 0) | (settings.showNextEvent ? 16 : 0)
                | (settings.showTracks ? 32 : 0) | (settings.showJunctions ? 64 : 0) | (settings.showSpeeds ? 128 : 0)
                | (settings.showGrades ? 256 : 0) | (settings.showSignals ? 512 : 0) | (settings.showOtherSigns ? 1024 : 0)
                | (settings.showOpposite ? 2048 : 0) | (settings.fitRouteToWidth ? 4096 : 0);
            var units = Main.settings.trackInfoSettings.distanceUnits;
            if (ReferenceEquals(snapshot, current) && width == availableWidth && previousKey == flags
                && previousFont == fontSize && previousNear == settings.nearDistance
                && previousProfile == settings.profileHeight && previousUnits == units) return;
            previousKey = flags; previousFont = fontSize; previousNear = settings.nearDistance;
            previousProfile = settings.profileHeight; previousUnits = units;
            snapshot = current;
            width = Mathf.Max(1, availableWidth);
            line = fontSize + 6;
            fullRange = RouteGraphScale.End(snapshot.RequestedDistance, snapshot.ReachedDistance, settings.fitRouteToWidth);
            nearDistance = Math.Min(fullRange, Math.Max(1, settings.nearDistance));
            profileHeight = settings.profileHeight;
            expanded = settings.expandedAtlas; showStatus = settings.showRouteStatus;
            showNear = settings.showNearProfile; showOverview = settings.showOverview; showNext = settings.showNextEvent;
            nearClusters.Clear(); overviewClusters.Clear();
            if (showNear) BuildClusters(nearClusters, 0, nearDistance, width, true);
            if (showOverview) BuildClusters(overviewClusters, 0, fullRange, width, true);
            atlasClusters.Clear();
            if (expanded)
                for (var tile = 0; tile < 5; tile++)
                {
                    var clusters = new List<Cluster>();
                    BuildClusters(clusters, tile * fullRange / 5, (tile + 1) * fullRange / 5, (width - 16) / 5, tile == 4);
                    atlasClusters.Add(clusters);
                }
            status = $"Mapped {Metres(snapshot.ReachedDistance)} / {Metres(snapshot.RequestedDistance)} requested  ·  {snapshot.Events.Count} items"
                + (snapshot.Termination == RouteWalkTermination.HorizonReached ? "" : "  ·  " + Friendly(snapshot.Termination.ToString()))
                + (snapshot.IsCollecting ? "  ·  refreshing" : "");
            var upcoming = snapshot.Events.FirstOrDefault(e => e.Distance > 0 && IsVisible(e));
            nextEventColor = upcoming != null && upcoming.Kind == RouteEventKind.Signal ? EventColor(upcoming) : Color.white;
            nextEvent = upcoming == null ? "No displayed event ahead; complete retained catalogue in UMM." : "Next " + FormatEvent(upcoming);
            Height = (showStatus ? line : 0) + (showNear ? 3 * line + profileHeight : 0)
                + (showOverview ? 5 * line + profileHeight : 0) + (showNext ? line : 0) + (expanded ? 3 * line + profileHeight : 0);
            if (Height == 0) Height = line;
        }

        private static void BuildClusters(List<Cluster> target, double start, double end, float plotWidth, bool includeEnd)
        {
            target.Clear();
            var markerWidth = MarkerWidth(plotWidth);
            var markerSpacing = markerWidth + 2;
            Cluster? current = null;
            foreach (var observation in snapshot.Events)
            {
                if (!IsVisible(observation) || observation.Distance < start || observation.Distance > end || (!includeEnd && observation.Distance == end)) continue;
                var separation = current == null ? double.PositiveInfinity
                    : MarkerCentre(observation.Distance, start, end, plotWidth, markerWidth)
                        - MarkerCentre(current.MeanDistance, start, end, plotWidth, markerWidth);
                if (current == null || separation >= markerSpacing)
                {
                    current = new Cluster { First = observation, Count = 1, MeanDistance = observation.Distance };
                    target.Add(current);
                }
                else
                {
                    current.MeanDistance += (observation.Distance - current.MeanDistance) / (current.Count + 1);
                    current.Count++;
                }
            }
        }

        public static void DrawPassive()
        {
            var area = GUILayoutUtility.GetRect(width, Height, GUILayout.Width(width), GUILayout.Height(Height));
            var y = area.y;
            if (showStatus) { Text(new Rect(area.x, y, width, line), status, Styles.noWrap); y += line; }
            if (showOverview)
            {
                var profile = new Rect(area.x, y + line, width, profileHeight);
                var exaggeration = ProfileExaggeration(profile, 0, fullRange);
                Text(new Rect(area.x, y, width, line), $"Track ahead  0–{Metres(fullRange)}  ·  elevation ×{exaggeration:F0}", Styles.noWrap); y += line;
                DrawProfile(new Rect(area.x, y, width, profileHeight), 0, fullRange); y += profileHeight;
                DrawClusters(new Rect(area.x, y, width, line), overviewClusters, 0, fullRange); y += line;
                DrawAxis(new Rect(area.x, y, width, line), 0, fullRange, 5); y += line;
                Text(new Rect(area.x, y, width, line), "S signal   V speed   J junction   G grade   T track", Styles.noWrap); y += line;
                Text(new Rect(area.x, y, width, line), "Numbers = grouped events · all items in UMM", Styles.noWrap); y += line;
            }
            if (showNear)
            {
                var profile = new Rect(area.x, y + line, width, profileHeight);
                var exaggeration = ProfileExaggeration(profile, 0, nearDistance);
                Text(new Rect(area.x, y, width, line), $"Close-up  0–{Metres(nearDistance)}  ·  elevation ×{exaggeration:F0}", Styles.noWrap);
                DrawProfile(profile, 0, nearDistance); y += line + profileHeight;
                DrawClusters(new Rect(area.x, y, width, line), nearClusters, 0, nearDistance); y += line;
                DrawAxis(new Rect(area.x, y, width, line), 0, nearDistance, 5); y += line;
            }
            if (showNext)
            {
                var saved = GUI.contentColor; GUI.contentColor = nextEventColor;
                Text(new Rect(area.x, y, width, line), nextEvent, Styles.noWrap);
                GUI.contentColor = saved; y += line;
            }
            if (expanded)
            {
                Text(new Rect(area.x, y, width, line), "Optional atlas · five sections of the same route", Styles.noWrap); y += line;
                var tileWidth = (width - 16) / 5;
                for (var tile = 0; tile < 5; tile++)
                {
                    var x = area.x + tile * (tileWidth + 4);
                    var start = tile * fullRange / 5; var end = (tile + 1) * fullRange / 5;
                    DrawProfile(new Rect(x, y, tileWidth, profileHeight), start, end);
                    DrawClusters(new Rect(x, y + profileHeight, tileWidth, line), atlasClusters[tile], start, end);
                    Text(new Rect(x, y + profileHeight + line, tileWidth, line), $"{Km(start)}–{Km(end)} · {atlasClusters[tile].Sum(c => c.Count)}", Styles.noWrap);
                }
            }
            if (!showStatus && !showNear && !showOverview && !showNext && !expanded)
                Text(area, "Graph sections hidden in UMM display options", Styles.noWrap);
        }

        public static bool IsVisible(RouteEvent item)
        {
            var s = Main.settings.layoutSettings;
            if (!s.showOpposite && !item.AppliesToTravel) return false;
            switch (item.Kind)
            {
                case RouteEventKind.Track: return s.showTracks;
                case RouteEventKind.Junction: return s.showJunctions;
                case RouteEventKind.Speed: return s.showSpeeds;
                case RouteEventKind.Grade: return s.showGrades;
                case RouteEventKind.Signal: return s.showSignals;
                default: return s.showOtherSigns;
            }
        }
        public static string FormatEvent(RouteEvent item) =>
            $"{Metres(item.Distance)}  {item.Kind}  {Plain(item.Label)}  {Plain(item.Aspect)}  {Plain(item.Fact)}" + (item.AppliesToTravel ? "" : "  [opposite]");

        private static void DrawClusters(Rect rect, IReadOnlyList<Cluster> clusters, double start, double end)
        {
            Fill(rect, new Color(0.09f, 0.12f, 0.14f, PlotBackgroundOpacity));
            ShadeUnknown(rect, start, end);
            var markerWidth = MarkerWidth(rect.width);
            foreach (var cluster in clusters)
            {
                var x = rect.x + MarkerCentre(cluster.MeanDistance, start, end, rect.width, markerWidth);
                var box = new Rect(x - markerWidth / 2, rect.y + 1, markerWidth, rect.height - 2);
                var color = cluster.Count == 1 ? EventColor(cluster.First) : new Color(0.35f, 0.45f, 0.5f);
                if (cluster.Count == 1 && !cluster.First.AppliesToTravel) color *= 0.55f;
                Fill(box, color);
                var token = cluster.Count == 1 ? EventToken(cluster.First.Kind) : cluster.Count.ToString(CultureInfo.InvariantCulture);
                var savedContent = GUI.contentColor;
                // White/green signal backgrounds need dark tokens to remain legible.
                GUI.contentColor = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f > 0.55f ? Color.black : Color.white;
                Text(box, token, Styles.centeredBold);
                GUI.contentColor = savedContent;
            }
        }

        private static float MarkerWidth(float plotWidth) => Mathf.Min(plotWidth,
            Math.Max(20, Styles.TextWidth(snapshot.Events.Count.ToString(CultureInfo.InvariantCulture), Styles.noWrapBold) + 6));

        private static float MarkerCentre(double distance, double start, double end, float plotWidth, float markerWidth) =>
            Mathf.Clamp((float)RouteGraphScale.Fraction(distance, start, end) * plotWidth, markerWidth / 2, plotWidth - markerWidth / 2);

        private static void DrawAxis(Rect rect, double start, double end, int divisions, string? title = null)
        {
            for (var tick = 0; tick <= divisions; tick++)
            {
                var distance = start + (end - start) * tick / divisions;
                var label = tick == 0 && title != null ? title : Metres(distance);
                var textWidth = Styles.TextWidth(label, Styles.noWrap);
                var x = rect.x + rect.width * tick / divisions;
                x = Mathf.Clamp(x - textWidth / 2, rect.x, Mathf.Max(rect.x, rect.xMax - textWidth));
                Text(new Rect(x, rect.y, Mathf.Min(textWidth, rect.width), line), label, Styles.noWrap);
            }
        }

        private static float ProfileExaggeration(Rect rect, double start, double end)
        {
            GetElevationRange(start, end, out var low, out var high);
            return (float)((rect.height - 4) / Math.Max(2, high - low) / (rect.width / Math.Max(1, end - start)));
        }

        private static void DrawProfile(Rect rect, double start, double end)
        {
            if (Event.current.type != EventType.Repaint) return;
            Fill(rect, new Color(0.045f, 0.07f, 0.085f, PlotBackgroundOpacity));
            ShadeUnknown(rect, start, end);
            if (snapshot.Profile.Count < 2)
            {
                Text(rect, "Grade profile unavailable", Styles.noWrap);
                return;
            }
            GetElevationRange(start, end, out var low, out var high);
            var span = Math.Max(2, high - low);
            var centre = (high + low) / 2;
            low = centre - span / 2;
            for (var i = 1; i < snapshot.Profile.Count; i++)
            {
                var a = snapshot.Profile[i - 1];
                var b = snapshot.Profile[i];
                if (b.Distance <= a.Distance || b.Distance < start || a.Distance > end) continue;
                var d1 = Math.Max(start, a.Distance);
                var d2 = Math.Min(end, b.Distance);
                var e1 = a.Elevation + (b.Elevation - a.Elevation) * (d1 - a.Distance) / (b.Distance - a.Distance);
                var e2 = a.Elevation + (b.Elevation - a.Elevation) * (d2 - a.Distance) / (b.Distance - a.Distance);
                var p1 = new Vector2(rect.x + (float)RouteGraphScale.Fraction(d1, start, end) * rect.width,
                    rect.yMax - 2 - (float)((e1 - low) / span) * (rect.height - 4));
                var p2 = new Vector2(rect.x + (float)RouteGraphScale.Fraction(d2, start, end) * rect.width,
                    rect.yMax - 2 - (float)((e2 - low) / span) * (rect.height - 4));
                DrawLine(p1, p2, new Color(0.45f, 0.85f, 0.72f), 2);
            }
        }

        private static void GetElevationRange(double start, double end, out double low, out double high)
        {
            low = double.PositiveInfinity;
            high = double.NegativeInfinity;
            for (var i = 1; i < snapshot.Profile.Count; i++)
            {
                var a = snapshot.Profile[i - 1];
                var b = snapshot.Profile[i];
                if (b.Distance <= a.Distance || b.Distance < start || a.Distance > end) continue;
                var d1 = Math.Max(start, a.Distance);
                var d2 = Math.Min(end, b.Distance);
                var e1 = a.Elevation + (b.Elevation - a.Elevation) * (d1 - a.Distance) / (b.Distance - a.Distance);
                var e2 = a.Elevation + (b.Elevation - a.Elevation) * (d2 - a.Distance) / (b.Distance - a.Distance);
                low = Math.Min(low, Math.Min(e1, e2));
                high = Math.Max(high, Math.Max(e1, e2));
            }
            if (double.IsInfinity(low)) { low = 0; high = 0; }
        }

        private static void ShadeUnknown(Rect rect, double start, double end)
        {
            var reached = Math.Max(start, Math.Min(end, snapshot.ReachedDistance));
            var x = rect.x + (float)RouteGraphScale.Fraction(reached, start, end) * rect.width;
            Fill(new Rect(x, rect.y, rect.xMax - x, rect.height), new Color(0.18f, 0.18f, 0.19f, PlotBackgroundOpacity));
            if (Event.current.type != EventType.Repaint) return;
            for (var stripe = x; stripe < rect.xMax; stripe += 8)
                Fill(new Rect(stripe, rect.y, Mathf.Min(1, rect.xMax - stripe), rect.height), new Color(0.28f, 0.28f, 0.3f, PlotBackgroundOpacity));
        }

        private static void DrawLine(Vector2 a, Vector2 b, Color color, float thickness)
        {
            if (Event.current.type != EventType.Repaint) return;
            var matrix = GUI.matrix;
            try
            {
                var delta = b - a;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
                Fill(new Rect(a.x, a.y - thickness / 2, delta.magnitude, thickness), color);
            }
            finally { GUI.matrix = matrix; }
        }

        private static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0 || rect.height <= 0) return;
            var saved = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * saved.a);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = saved;
        }

        private static void Text(Rect rect, string value, GUIStyle style) => GUI.Label(rect, Plain(value), style);
        private static string Plain(string? value) => (value ?? "").Replace('<', '‹').Replace('>', '›').Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        private static string Km(double distance) => Main.settings.trackInfoSettings.distanceUnits == Settings.TrackInfoSettings.EventDistanceUnits.mi
            ? (distance / 1609.344).ToString("0.##", CultureInfo.InvariantCulture) + " mi"
            : Main.settings.trackInfoSettings.distanceUnits == Settings.TrackInfoSettings.EventDistanceUnits.ft
                ? (distance / 0.3048).ToString("0", CultureInfo.InvariantCulture) + " ft"
                : (distance / 1000).ToString("0.#", CultureInfo.InvariantCulture) + " km";
        private static string Metres(double distance) => Main.settings.trackInfoSettings.distanceUnits == Settings.TrackInfoSettings.EventDistanceUnits.m && distance < 1000 ? distance.ToString(distance < 1 ? "0.##" : "0", CultureInfo.InvariantCulture) + " m" : Km(distance);
        private static string Friendly(string value) => string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
        private static string EventToken(RouteEventKind kind) => kind == RouteEventKind.Signal ? "S" : kind == RouteEventKind.Speed ? "V"
            : kind == RouteEventKind.Junction ? "J" : kind == RouteEventKind.Grade ? "G" : kind == RouteEventKind.Track ? "T" : "?";
        public static Color EventColor(RouteEvent observation)
        {
            var hex = observation.ColorHex.StartsWith("#", StringComparison.Ordinal) ? observation.ColorHex : "#" + observation.ColorHex;
            return ColorUtility.TryParseHtmlString(hex, out var color) ? color : new Color(0.32f, 0.46f, 0.55f);
        }

        public static void DrawInspector(RouteSnapshot<RailTrack> current)
        {
            inspectorText ??= new GUIStyle(GUI.skin.label) { richText = false, wordWrap = true };
            inspectorHeading ??= new GUIStyle(inspectorText) { fontStyle = FontStyle.Bold };
            if (Event.current.type == EventType.Layout)
            {
                inspectorSnapshot = current;
                inspectorPage = RoutePaging.GetPage(current.Events, requestedPage, Main.settings.layoutSettings.inspectorPageSize);
                requestedPage = inspectorPage.PageIndex;
            }
            if (inspectorSnapshot == null || inspectorPage == null) return;
            GUILayout.BeginVertical("box");
            GUILayout.Label($"Complete retained catalogue — {inspectorPage.TotalCount} events, snapshot {inspectorSnapshot.Revision}, {inspectorSnapshot.UpdatedAtUtc:HH:mm:ss} UTC", inspectorHeading);
            GUILayout.Label($"Geometry: {Km(inspectorSnapshot.ReachedDistance)} / {Km(inspectorSnapshot.RequestedDistance)}; {Friendly(inspectorSnapshot.Termination.ToString())}. "
                + (inspectorSnapshot.IsCollecting ? "Collection still in progress." : "Geometry completion does not establish sign or signal coverage."), inspectorText);
            GUILayout.Label(inspectorSnapshot.EventCoverage, inspectorText);
            GUILayout.Label(inspectorSnapshot.SignalCoverage, inspectorText);
            foreach (var diagnostic in inspectorSnapshot.Diagnostics) GUILayout.Label(diagnostic, inspectorText);
            GUILayout.Label("Every retained event is listed, including repeated source observations and opposite-facing signs. IDs are native source IDs, not inferred player identities. Clusters never remove records.", inspectorText);
            GUILayout.BeginHorizontal();
            var savedEnabled = GUI.enabled;
            GUI.enabled = savedEnabled && inspectorPage.PageIndex > 0;
            if (GUILayout.Button("First", GUILayout.ExpandWidth(false))) requestedPage = 0;
            if (GUILayout.Button("Previous", GUILayout.ExpandWidth(false))) requestedPage = inspectorPage.PageIndex - 1;
            GUI.enabled = savedEnabled && inspectorPage.PageIndex + 1 < inspectorPage.PageCount;
            if (GUILayout.Button("Next", GUILayout.ExpandWidth(false))) requestedPage = inspectorPage.PageIndex + 1;
            if (GUILayout.Button("Last", GUILayout.ExpandWidth(false))) requestedPage = Math.Max(0, inspectorPage.PageCount - 1);
            GUI.enabled = savedEnabled;
            GUILayout.Label(inspectorPage.PageCount == 0 ? "No retained events" : $"Page {inspectorPage.PageIndex + 1} / {inspectorPage.PageCount}", inspectorText);
            GUILayout.EndHorizontal();
            inspectorScroll = GUILayout.BeginScrollView(inspectorScroll, GUILayout.Height(380));
            for (var index = 0; index < inspectorPage.Items.Count; index++)
            {
                var observation = inspectorPage.Items[index];
                GUILayout.BeginVertical("box");
                var savedContentColor = GUI.contentColor;
                if (observation.Kind == RouteEventKind.Signal) GUI.contentColor = EventColor(observation);
                GUILayout.Label($"{inspectorPage.FirstItemIndex + index + 1}. {Metres(observation.Distance)}  {observation.Kind}  {observation.Label}", inspectorHeading);
                GUI.contentColor = savedContentColor;
                GUILayout.Label($"Source ID: {observation.Id}", inspectorText);
                GUILayout.Label($"{observation.Aspect}  {observation.Fact}  {observation.Freshness}"
                    + (observation.AppliesToTravel ? "" : "  [opposite-facing observation]"), inspectorText);
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }
    }
}
