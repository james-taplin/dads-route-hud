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
        private static RouteSnapshot<RailTrack> snapshot = RouteSnapshot<RailTrack>.Empty();
        private sealed class BoardLabel
        {
            public RouteMapLabel Placement = null!;
            public string First = "", Second = "";
            public float FirstWidth, SecondWidth;
        }
        private sealed class BoardStrip
        {
            public readonly List<BoardLabel> Labels = new List<BoardLabel>();
            public string More = "";
            public float MoreWidth;
            public int Count;
        }
        private const int BoardLanes = 2;
        private static BoardStrip nearBoards = new BoardStrip(), overviewBoards = new BoardStrip();
        private static readonly List<BoardStrip> atlasBoards = new List<BoardStrip>();
        private static IReadOnlyList<RouteTrafficMarker> traffic = Array.Empty<RouteTrafficMarker>();
        private static IReadOnlyList<RouteTrafficMarker> ownCars = Array.Empty<RouteTrafficMarker>();
        private static double? speedKph;
        private static string trafficStatus = "";
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
            traffic = NativeRouteTraffic.Markers; speedKph = NativeTelemetry.SpeedKph; trafficStatus = NativeRouteTraffic.Status;
            ownCars = NativeRouteTraffic.OwnCars;
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
            if (showNear) nearBoards = ArrangeBoards(0, nearDistance, width, true);
            if (showOverview) overviewBoards = ArrangeBoards(0, fullRange, width, true);
            atlasBoards.Clear();
            if (expanded)
                for (var tile = 0; tile < 5; tile++)
                {
                    atlasBoards.Add(ArrangeBoards(tile * fullRange / 5, (tile + 1) * fullRange / 5,
                        Mathf.Max(1, (width - 16) / 5), tile == 4));
                }
            status = $"Mapped {Metres(snapshot.ReachedDistance)} / {Metres(snapshot.RequestedDistance)} requested  ·  {snapshot.Events.Count} items"
                + (snapshot.Termination == RouteWalkTermination.HorizonReached ? "" : "  ·  " + Friendly(snapshot.Termination.ToString()))
                + (snapshot.IsCollecting ? "  ·  refreshing" : "");
            var upcoming = snapshot.Events.FirstOrDefault(e => e.Distance > 0 && IsVisible(e));
            nextEventColor = upcoming != null && upcoming.Kind == RouteEventKind.Signal ? EventColor(upcoming) : Color.white;
            nextEvent = upcoming == null ? "No displayed event ahead; complete retained catalogue in UMM." : "Next " + FormatEvent(upcoming);
            Height = (showStatus ? line : 0) + (showNear ? (2 + BoardLanes) * line + profileHeight : 0)
                + (showOverview ? (4 + BoardLanes) * line + profileHeight : 0) + (showNext ? line : 0)
                + (expanded ? (2 + BoardLanes) * line + profileHeight : 0);
            if (Height == 0) Height = line;
        }

        public static void DrawPassive()
        {
            var area = GUILayoutUtility.GetRect(width, Height, GUILayout.Width(width), GUILayout.Height(Height));
            // The graph is passive, with no controls requiring Layout or input processing.
            // Reserve its space every pass, then format and paint only on Repaint.
            if (Event.current.type != EventType.Repaint) return;
            var y = area.y;
            if (showStatus) { Text(new Rect(area.x, y, width, line), status, Styles.noWrap); y += line; }
            if (showOverview)
            {
                var profile = new Rect(area.x, y + line, width, profileHeight);
                var exaggeration = ProfileExaggeration(profile, 0, fullRange);
                Text(new Rect(area.x, y, width, line), $"Track ahead  0–{Metres(fullRange)}  ·  elevation ×{exaggeration:F0}", Styles.noWrap); y += line;
                DrawMap(new Rect(area.x, y, width, profileHeight + BoardLanes * line), overviewBoards, 0, fullRange); y += profileHeight + BoardLanes * line;
                DrawAxis(new Rect(area.x, y, width, line), 0, fullRange, 5); y += line;
                Text(new Rect(area.x, y, width, line), "S signal · ←/↑/→ points · % grade · T track · green YOU · orange other cars", Styles.noWrap); y += line;
                Text(new Rect(area.x, y, width, line), "Boards below profile · speed km/h vs you · +N more events · your consist at 0 m · " + trafficStatus, Styles.noWrap); y += line;
            }
            if (showNear)
            {
                var profile = new Rect(area.x, y + line, width, profileHeight + BoardLanes * line);
                var exaggeration = ProfileExaggeration(new Rect(0, 0, width, profileHeight), 0, nearDistance);
                Text(new Rect(area.x, y, width, line), $"Close-up  0–{Metres(nearDistance)}  ·  elevation ×{exaggeration:F0}", Styles.noWrap);
                DrawMap(profile, nearBoards, 0, nearDistance); y += line + profileHeight + BoardLanes * line;
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
                var tileWidth = Mathf.Max(1, (width - 16) / 5);
                for (var tile = 0; tile < 5; tile++)
                {
                    var x = area.x + tile * (tileWidth + 4);
                    var start = tile * fullRange / 5; var end = (tile + 1) * fullRange / 5;
                    DrawMap(new Rect(x, y, tileWidth, profileHeight + BoardLanes * line), atlasBoards[tile], start, end);
                    Text(new Rect(x, y + profileHeight + BoardLanes * line, tileWidth, line),
                        $"{Km(start)}–{Km(end)} · {atlasBoards[tile].Count}", Styles.noWrap);
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
        /// <summary>
        /// The row's columns: distance, what it is, a short marker such as a switch's side, what it
        /// says, and the game's code. The marker is not escaped because it is one of this mod's own
        /// fixed tokens, never native text.
        /// </summary>
        public static (string Distance, string Kind, string Marker, string Description, string Code) EventRow(RouteEvent item)
        {
            var description = Plain(item.Label);
            foreach (var extra in new[] { item.Aspect, item.Fact })
            {
                var text = Plain(extra).Trim();
                if (text.Length > 0) description += "  ·  " + text;
            }
            if (!item.AppliesToTravel) description += "  (opposite direction)";
            var marker = item.Marker == RouteTurn.LeftMarker || item.Marker == RouteTurn.RightMarker
                || item.Marker == RouteTurn.AheadMarker ? item.Marker : "";
            return (Metres(item.Distance), RouteNaming.KindWord(item.Kind), marker, description,
                Plain(item.Code).Trim());
        }

        public static string FormatEvent(RouteEvent item)
        {
            var row = EventRow(item);
            return row.Distance + "  " + row.Kind + (row.Marker.Length == 0 ? "" : "  " + row.Marker)
                + "  " + row.Description + (row.Code.Length == 0 ? "" : "  " + row.Code);
        }

        private static BoardStrip ArrangeBoards(double start, double end, float plotWidth, bool includeEnd)
        {
            plotWidth -= OriginInset(plotWidth, start);
            var result = new BoardStrip();
            var placements = RouteMapLabels.ArrangeBoards(snapshot.Events, start, end, plotWidth,
                BoardLanes, BoardWidth, IsVisible, includeEnd);
            result.Count = placements.Sum(item => 1 + item.Additional);
            if (placements.Any(item => item.Additional > 0))
            {
                // Overflow belongs to one badge, not empty padding on every board.
                // One reflow reserves enough room for even the largest possible count.
                var badgeWidth = Styles.TextWidth("+" + result.Count.ToString(CultureInfo.InvariantCulture), Styles.noWrapBold) + 4;
                if (plotWidth <= badgeWidth + 4)
                {
                    result.More = "+" + result.Count.ToString(CultureInfo.InvariantCulture);
                    result.MoreWidth = Mathf.Max(1, plotWidth);
                    return result;
                }
                placements = RouteMapLabels.ArrangeBoards(snapshot.Events, start, end, plotWidth - badgeWidth - 3,
                    BoardLanes, BoardWidth, IsVisible, includeEnd);
                var additional = placements.Sum(item => item.Additional);
                if (additional > 0)
                {
                    result.More = "+" + additional.ToString(CultureInfo.InvariantCulture);
                    result.MoreWidth = Mathf.Min(plotWidth, Styles.TextWidth(result.More, Styles.noWrapBold) + 4);
                }
            }
            foreach (var placement in placements)
            {
                var item = placement.Event;
                var first = BoardFirst(item);
                var second = BoardSecond(item);
                result.Labels.Add(new BoardLabel { Placement = placement, First = first, Second = second,
                    FirstWidth = Styles.TextWidth(first, Styles.noWrapBold),
                    SecondWidth = second.Length == 0 ? 0 : Styles.TextWidth(second, Styles.noWrapBold) });
            }
            return result;
        }

        private static bool NumericBoard(RouteEvent item) => item.Kind == RouteEventKind.Speed
            && item.SpeedLimitKph.HasValue && item.AppliesToTravel;
        private static string BoardFirst(RouteEvent item) => NumericBoard(item)
            ? RouteSpeed.Number(item.SpeedLimitKph!.Value) : EventToken(item);
        private static string BoardSecond(RouteEvent item) => NumericBoard(item) && item.AlternateSpeedLimitKph.HasValue
            ? "/" + RouteSpeed.Number(item.AlternateSpeedLimitKph.Value) : "";
        private static float BoardWidth(RouteEvent item)
        {
            var second = BoardSecond(item);
            return Styles.TextWidth(BoardFirst(item), Styles.noWrapBold)
                + (second.Length == 0 ? 0 : Styles.TextWidth(second, Styles.noWrapBold)) + 4;
        }

        private static Color SpeedColor(double? limit)
        {
            // Match RouteSpeed.ColorHex without parsing strings for every painted board.
            switch (RouteSpeed.Compare(speedKph, limit))
            {
                case RouteSpeedState.WithinLimit: return new Color(89f / 255, 1, 115f / 255);
                case RouteSpeedState.SlightlyOver: return new Color(1, 214f / 255, 38f / 255);
                case RouteSpeedState.OverLimit: return new Color(1, 64f / 255, 64f / 255);
                default: return new Color(179f / 255, 191f / 255, 204f / 255);
            }
        }

        private static void DrawMap(Rect rect, BoardStrip boards, double start, double end)
        {
            Fill(rect, new Color(0.025f, 0.04f, 0.05f, PlotBackgroundOpacity));
            var plot = PlotArea(rect, start);
            var profile = new Rect(plot.x, rect.y, plot.width, profileHeight);
            DrawProfile(profile, start, end);
            var strip = new Rect(plot.x, profile.yMax, plot.width, BoardLanes * line);
            Fill(strip, new Color(0.09f, 0.12f, 0.14f, PlotBackgroundOpacity));
            ShadeUnknown(strip, start, end);
            // Leaders keep the physical route anchor clear after collision packing.
            // Paint all leaders first so none can run over another board's face.
            foreach (var label in boards.Labels)
            {
                var placement = label.Placement;
                var anchor = plot.x + (float)RouteGraphScale.Fraction(placement.Event.Distance, start, end) * plot.width;
                DrawLine(new Vector2(anchor, strip.y),
                    new Vector2(plot.x + placement.X + placement.Width / 2, strip.y + placement.Lane * line + 1),
                    new Color(0.35f, 0.43f, 0.47f, 0.5f), 1);
            }
            var saved = GUI.contentColor;
            foreach (var label in boards.Labels)
            {
                var placement = label.Placement;
                var item = placement.Event;
                GUI.BeginGroup(new Rect(plot.x + placement.X, strip.y + placement.Lane * line + 1, placement.Width, line - 2));
                try
                {
                    if (NumericBoard(item))
                    {
                        Fill(new Rect(0, 0, placement.Width, line - 2), Color.black);
                        GUI.contentColor = SpeedColor(item.SpeedLimitKph);
                        Text(new Rect(2, 0, label.FirstWidth, line - 2), label.First, Styles.noWrapBold);
                        GUI.contentColor = SpeedColor(item.AlternateSpeedLimitKph);
                        Text(new Rect(2 + label.FirstWidth, 0, label.SecondWidth, line - 2), label.Second, Styles.noWrapBold);
                    }
                    else
                    {
                        var color = EventColor(item);
                        if (!item.AppliesToTravel) color *= 0.55f;
                        Fill(new Rect(0, 0, placement.Width, line - 2), color);
                        GUI.contentColor = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f > 0.55f ? Color.black : Color.white;
                        Text(new Rect(2, 0, label.FirstWidth, line - 2), label.First, Styles.noWrapBold);
                    }
                }
                finally { GUI.EndGroup(); GUI.contentColor = saved; }
            }
            if (boards.More.Length > 0)
            {
                GUI.BeginGroup(new Rect(strip.xMax - boards.MoreWidth, strip.y + 1, boards.MoreWidth, line - 2));
                try
                {
                    Fill(new Rect(0, 0, boards.MoreWidth, line - 2), Color.black);
                    GUI.contentColor = Color.white;
                    Text(new Rect(2, 0, Mathf.Max(1, boards.MoreWidth - 4), line - 2), boards.More, Styles.noWrapBold);
                }
                finally { GUI.EndGroup(); GUI.contentColor = saved; }
            }
            GUI.contentColor = saved;
            DrawTraffic(profile, start, end);
            if (start == 0)
            {
                GetElevationRange(start, end, out var low, out var high);
                var range = Math.Max(2, high - low);
                low = (low + high) / 2 - range / 2;
                var originY = profile.yMax - 2 - (float)((ElevationAt(0) - low) / range) * (profile.height - 4);
                DrawOwnConsist(new Rect(rect.x, profile.y, plot.x - rect.x, profile.height), originY);
            }
        }

        private static float OriginInset(float plotWidth, double start) => start == 0 ? Mathf.Min(plotWidth / 4, Mathf.Max(40, line * 4)) : 0;
        private static Rect PlotArea(Rect rect, double start)
        {
            var inset = OriginInset(rect.width, start);
            return new Rect(rect.x + inset, rect.y, Mathf.Max(1, rect.width - inset), rect.height);
        }

        private static void DrawOwnConsist(Rect rect, float originY)
        {
            Text(new Rect(rect.x, rect.yMax, rect.width, line), "YOU · " + (ownCars.Count > 0 ? ownCars.Count.ToString() : "?"), Styles.noWrapBold);
            Text(new Rect(rect.x, rect.yMax + line, rect.width, line), "0 m", Styles.noWrap);
            Fill(new Rect(rect.xMax - 1, rect.y, 1, rect.height), new Color(0.3f, 0.95f, 0.4f, 0.5f));
            if (ownCars.Count == 0) return;
            var cell = Mathf.Min(8, Mathf.Max(0.1f, rect.width - 4) / ownCars.Count);
            var x = rect.xMax - 3 - cell * ownCars.Count;
            foreach (var car in ownCars)
            {
                var height = car.IsLoco ? 8f : 6f;
                Fill(new Rect(x, Mathf.Clamp(originY - height - 1, rect.y, rect.yMax - height),
                    cell - Mathf.Min(1, cell * 0.2f), height), new Color(0.25f, 0.95f, 0.4f));
                x += cell;
            }
        }

        private static void DrawTraffic(Rect rect, double start, double end)
        {
            GetElevationRange(start, end, out var low, out var high);
            var span = Math.Max(2, high - low);
            low = (low + high) / 2 - span / 2;
            foreach (var car in traffic)
            {
                if (car.End < start || car.Start > end) continue;
                var from = rect.x + (float)RouteGraphScale.Fraction(Math.Max(start, car.Start), start, end) * rect.width;
                var to = rect.x + (float)RouteGraphScale.Fraction(Math.Min(end, car.End), start, end) * rect.width;
                var centre = Math.Max(start, Math.Min(end, car.Centre));
                var y = rect.yMax - 2 - (float)((ElevationAt(centre) - low) / span) * (rect.height - 4);
                var height = car.IsLoco ? 7f : 5f;
                var size = Mathf.Min(rect.width, Mathf.Max(3, to - from));
                var x = Mathf.Clamp((from + to - size) / 2, rect.x, rect.xMax - size);
                var box = new Rect(x, Mathf.Clamp(y - height - 1, rect.y, rect.yMax - height), size, height);
                Fill(box, new Color(0.015f, 0.04f, 0.02f));
                Fill(new Rect(box.x + 0.5f, box.y + 1, Mathf.Max(1, box.width - 1), box.height - 2), new Color(1, 0.55f, 0.15f));
            }
        }

        private static double ElevationAt(double distance)
        {
            var points = snapshot.Profile;
            if (points.Count == 0) return 0;
            var lo = 0; var hi = points.Count - 1;
            while (lo < hi)
            {
                var mid = lo + (hi - lo + 1) / 2;
                if (points[mid].Distance <= distance) lo = mid; else hi = mid - 1;
            }
            var a = points[lo];
            if (lo + 1 == points.Count) return a.Elevation;
            var b = points[lo + 1];
            return b.Distance <= a.Distance ? a.Elevation : a.Elevation + (b.Elevation - a.Elevation)
                * Math.Max(0, Math.Min(1, (distance - a.Distance) / (b.Distance - a.Distance)));
        }

        private static void DrawAxis(Rect rect, double start, double end, int divisions, string? title = null)
        {
            rect = PlotArea(rect, start);
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
            rect = PlotArea(rect, start);
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
        public static string EventDistance(double distance) => Metres(distance);
        private static string Friendly(string value) => string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
        private static string EventToken(RouteEvent item) => item.Kind == RouteEventKind.Signal ? "S" : item.Kind == RouteEventKind.Speed ? item.SpeedLimitKph.HasValue ? "V" : "V?"
            : item.Kind == RouteEventKind.Junction ? item.Marker == RouteTurn.LeftMarker ? "←" : item.Marker == RouteTurn.RightMarker ? "→"
                : item.Marker == RouteTurn.AheadMarker ? "↑" : "J"
            : item.Kind == RouteEventKind.Grade ? "%" : item.Kind == RouteEventKind.Track ? "T" : "?";
        public static Color EventColor(RouteEvent observation)
        {
            if (observation.Kind == RouteEventKind.Junction) return new Color(0.84f, 0.69f, 1);
            if (observation.Kind == RouteEventKind.Track) return new Color(0.49f, 0.87f, 0.91f);
            if (observation.Kind == RouteEventKind.Grade) return new Color(0.62f, 0.86f, 0.59f);
            if (observation.Kind == RouteEventKind.Speed) return new Color(1, 0.82f, 0.4f);
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
