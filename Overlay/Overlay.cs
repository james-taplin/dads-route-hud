using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using QuantitiesNet;
using UnityEngine;
using static QuantitiesNet.Units;

namespace DvMod.HeadsUpDisplay
{
    public static class Styles
    {
        public static readonly GUIStyle noChrome;
        public static readonly GUIStyle noWrap;
        public static readonly GUIStyle noWrapBold;
        public static readonly GUIStyle centeredBold;
        public static readonly GUIStyle rightAlign;
        public static readonly GUIStyle richText;
        public static readonly GUIStyle panel;
        /// <summary>Overlay buttons, sized from the HUD's own font rather than the default skin.</summary>
        public static readonly GUIStyle button;
        private static readonly GUIContent measurement = new GUIContent();
        private static int appliedFontSize;
        private static int appliedPadding = -1;

        /// <summary>Space between a window's frame and its content, and the unit other gaps follow.</summary>
        public static int Padding { get; private set; } = 4;

        /// <summary>
        /// Width a window loses to its own frame: padding on both sides, plus room for the
        /// scroll bar and the resize grip, which exist whatever the padding is.
        /// </summary>
        public static float ContentInset => Padding * 2 + 22;

        /// <summary>The gap between columns, following the padding rather than a fixed figure.</summary>
        public static int ColumnGap => Mathf.Max(3, Padding + 2);

        static Styles()
        {
            noChrome = new GUIStyle(GUI.skin.window);
            noChrome.normal.background = null;
            noChrome.onNormal = noChrome.normal;
            noWrap = new GUIStyle(GUI.skin.label) { wordWrap = false, richText = false, clipping = TextClipping.Clip };
            noWrapBold = new GUIStyle(noWrap) { fontStyle = FontStyle.Bold };
            centeredBold = new GUIStyle(noWrapBold) { alignment = TextAnchor.MiddleCenter };
            rightAlign = new GUIStyle(noWrap) { alignment = TextAnchor.MiddleRight };
            richText = new GUIStyle(noWrap) { richText = true };
            panel = new GUIStyle { padding = new RectOffset(10, 10, 8, 8), margin = new RectOffset() };
            button = new GUIStyle(GUI.skin.button) { wordWrap = false, alignment = TextAnchor.MiddleCenter };
        }

        public static void SetFontSize(int fontSize, int padding)
        {
            Padding = Mathf.Clamp(padding, 0, 16);
            if (appliedFontSize == fontSize && appliedPadding == Padding) return;
            appliedFontSize = fontSize;
            appliedPadding = Padding;
            foreach (var style in new[] { noWrap, noWrapBold, centeredBold, rightAlign, richText })
            {
                style.fontSize = fontSize;
                style.fixedHeight = fontSize + 6;
                style.margin = new RectOffset();
                style.padding = new RectOffset();
                style.normal.textColor = Color.white;
            }
            // Buttons keep the skin's own colours, but take the HUD's size and proportional
            // padding so they shrink with a small font instead of staying at the skin's default.
            var sides = Mathf.Max(4, Mathf.RoundToInt(fontSize * 0.6f));
            var ends = Mathf.Max(2, Mathf.RoundToInt(fontSize * 0.25f));
            button.fontSize = fontSize;
            button.fixedHeight = 0;
            button.padding = new RectOffset(sides, sides, ends, ends);
            button.margin = new RectOffset(0, 0, 0, 0);
        }

        /// <summary>A button's natural width at the current font size, including its padding.</summary>
        public static float ButtonWidth(string text) => TextWidth(text, button) + 2;

        public static float TextWidth(string text, GUIStyle style)
        {
            measurement.text = text;
            return Mathf.Ceil(style.CalcSize(measurement).x) + 1;
        }
    }

    public class Overlay : MonoBehaviour
    {
        public static int ColumnSpacing => Styles.ColumnGap;
        public static Overlay? instance;
        private readonly HudWindow mainWindow = new HudWindow(320);
        private readonly HudWindow graphWindow = new HudWindow(400);
        private readonly HudWindow metricsWindow = new HudWindow(320, 80);
        private bool showMetrics, dockMetrics, metricsDetached;
        private float metricsWidth, metricsBackground, metricsContent;
        private IReadOnlyList<List<int>> metricColumns = Array.Empty<List<int>>();
        private bool layoutReady, showMain, showGraph, dockGraph, showEvents, compact, drawMetrics, drawCarList;
        private float innerWidth;
        private int fontSize, columns;
        private string identity = "", summary = "";
        private TrainCar? sampledCar;
        private Rect mainRect, graphRect, metricsRect;
        private float background, content, graphBackground, graphContent;
        private bool locked;
        private string routeActionLabel = "", metricsActionLabel = "";
        private Action drawMain = null!, drawGraph = null!, toggleRoute = null!, drawMetricContent = null!, toggleMetrics = null!;
        private readonly List<(string label, string value, float labelWidth, float valueWidth)> metrics =
            new List<(string, string, float, float)>();
        private readonly List<List<int>> metricRows = new List<List<int>>();
        private RoutePage<RouteEvent>? eventPage;
        private int requestedPage;
        private readonly Settings.TrainInfoSettings trainSettings = new Settings.TrainInfoSettings();

        public void Start()
        {
            instance = this;
            drawMain = DrawMainContent;
            drawGraph = RouteMonitor.DrawPassive;
            toggleRoute = ToggleRouteWindow;
            drawMetricContent = DrawMetrics;
            toggleMetrics = () => SetMetricsWindow(!Main.settings.layoutSettings.popOutMetrics);
        }
        public void OnGUI()
        {
            if (!Main.enabled || drawMain == null) { Release(); layoutReady = false; return; }
            if (!layoutReady && Event.current.type != EventType.Layout) return;
            if (Event.current.type == EventType.Layout) CaptureLayout();
            if (showMain)
            {
                var allowStart = mainWindow.OwnsInput || ((!showGraph || !graphRect.Contains(Event.current.mousePosition))
                    && (!showMetrics || !metricsRect.Contains(Event.current.mousePosition)));
                mainWindow.Draw(ref mainRect, identity, background, content, locked, allowStart, drawMain, routeActionLabel, toggleRoute, metricsActionLabel, toggleMetrics);
                Main.settings.hudPosition = mainRect.position;
                if (metricsDetached) Main.settings.detailsSize = mainRect.size;
                else if (compact) Main.settings.compactSize = mainRect.size;
                else Main.settings.hudSize = mainRect.size;
            }
            else mainWindow.Release();
            if (showGraph)
            {
                graphWindow.Draw(ref graphRect, "Track ahead", graphBackground, graphContent, locked,
                    !showMetrics || !metricsRect.Contains(Event.current.mousePosition) || graphWindow.OwnsInput, drawGraph);
                Main.settings.graphPosition = graphRect.position;
                Main.settings.graphSize = graphRect.size;
            }
            else graphWindow.Release();
            if (showMetrics)
            {
                metricsWindow.Draw(ref metricsRect, compact ? "Metrics · columns" : "Metrics · tape", metricsBackground,
                    metricsContent, locked, true, drawMetricContent);
                Main.settings.metricsPosition = metricsRect.position;
                Main.settings.metricsSize = metricsRect.size;
            }
            else metricsWindow.Release();
        }
        private static void SetMetricsWindow(bool detached)
        {
            Main.settings.layoutSettings.popOutMetrics = detached;
            Main.settings.layoutSettings.showMetricsWindow = true;
            if (!detached) Main.settings.layoutSettings.showHud = true;
            Main.RequestSettingsSave();
        }
        private static void ToggleRouteWindow() => SetRouteWindow(!Main.settings.layoutSettings.popOutGraph);
        private static void SetRouteWindow(bool detached)
        {
            var layout = Main.settings.layoutSettings;
            layout.popOutGraph = detached;
            layout.showGraphWindow = true;
            // An explicit graph action makes the graph visible even after selecting event-list-only mode.
            if (layout.routeDisplay == Settings.LayoutSettings.RouteDisplay.EventList)
                layout.routeDisplay = Settings.LayoutSettings.RouteDisplay.GraphAndEvents;
            if (!detached) layout.showHud = true;
            Main.RequestSettingsSave();
            // Visibility/content changes are applied at the next Layout, never mid-event.
        }
        private void CaptureLayout()
        {
            var settings = Main.settings;
            settings.ClampLayout();
            var layout = settings.layoutSettings;
            compact = layout.displayMode == Settings.LayoutSettings.DisplayMode.Compact;
            columns = layout.columns;
            metricsDetached = layout.popOutMetrics;
            mainRect = mainWindow.Clamp(new Rect(settings.hudPosition, metricsDetached ? settings.detailsSize : compact ? settings.compactSize : settings.hudSize));
            graphRect = graphWindow.Clamp(new Rect(settings.graphPosition, settings.graphSize));
            metricsRect = metricsWindow.Clamp(new Rect(settings.metricsPosition, settings.metricsSize));
            if (settings.placeHudTopRight)
            {
                mainRect = mainWindow.TopRight(mainRect);
                settings.hudPosition = mainRect.position;
                settings.placeHudTopRight = false;
                Main.RequestSettingsSave();
            }
            if (settings.placeGraphTopRight)
            {
                graphRect.position = new Vector2(Mathf.Max(0, Screen.width - graphRect.width - 10),
                    Mathf.Max(10, Mathf.Min(mainRect.yMax + 10, Screen.height - graphRect.height - 10)));
                graphRect = graphWindow.Clamp(graphRect);
                settings.graphPosition = graphRect.position;
                settings.placeGraphTopRight = false;
                Main.RequestSettingsSave();
            }
            if (settings.placeMetricsTopRight && layout.popOutMetrics)
            {
                metricsRect = metricsWindow.TopRight(metricsRect);
                // Keep existing window headers reachable when opening the third section.
                metricsRect.y = Mathf.Max(10, Mathf.Min((layout.popOutGraph ? graphRect.yMax : mainRect.yMax) + 10,
                    Screen.height - metricsRect.height - 10));
                metricsRect = metricsWindow.Clamp(metricsRect);
                settings.metricsPosition = metricsRect.position;
                settings.placeMetricsTopRight = false;
                Main.RequestSettingsSave();
            }
            innerWidth = Math.Max(1, mainRect.width - Styles.ContentInset);
            metricsWidth = Math.Max(1, (layout.popOutMetrics ? metricsRect.width : mainRect.width) - Styles.ContentInset);
            fontSize = layout.fontSize;
            Styles.SetFontSize(fontSize, layout.windowPadding);
            background = layout.backgroundOpacity; content = layout.contentOpacity;
            graphBackground = layout.graphBackgroundOpacity; graphContent = layout.graphContentOpacity;
            metricsBackground = layout.metricsBackgroundOpacity; metricsContent = layout.metricsContentOpacity;
            locked = settings.lockPosition;
            RouteMonitor.PlotBackgroundOpacity = layout.popOutGraph ? graphBackground : background;
            showMain = layout.showHud;
            routeActionLabel = !settings.trackInfoSettings.enabled ? "" : layout.popOutGraph ? "Attach route" : "Detach route";
            showGraph = settings.trackInfoSettings.enabled && layout.popOutGraph && layout.showGraphWindow;
            dockGraph = settings.trackInfoSettings.enabled && !layout.popOutGraph
                && layout.routeDisplay != Settings.LayoutSettings.RouteDisplay.EventList;
            showEvents = settings.trackInfoSettings.enabled && layout.routeDisplay != Settings.LayoutSettings.RouteDisplay.Graph;
            drawMetrics = settings.drivingInfoSettings.enabled;
            showMetrics = drawMetrics && layout.popOutMetrics && layout.showMetricsWindow;
            dockMetrics = drawMetrics && !layout.popOutMetrics;
            metricsActionLabel = drawMetrics ? (layout.popOutMetrics ? "Attach metrics" : "Detach metrics") : "";
            sampledCar = PlayerManager.Car ? PlayerManager.Car : PlayerManager.LastLoco;
            identity = (metricsDetached && layout.popOutGraph ? "DETAILS" : compact ? "HUD" : "DRIVING") + (sampledCar != null ? $"  {sampledCar.ID}  {sampledCar.carType}" : "  No locomotive selected");
            CopyTrainSettings();
            summary = BuildTrainSummary();
            if (dockGraph || showGraph)
                RouteMonitor.PrepareLayout(RouteMonitorService.Current,
                    showGraph ? graphRect.width - Styles.ContentInset : innerWidth, fontSize, layout);
            if (showEvents)
            {
                var visible = RouteMonitorService.Current.Events.Where(RouteMonitor.IsVisible).ToArray();
                eventPage = RoutePaging.GetPage(visible, requestedPage, layout.inspectorPageSize);
                requestedPage = eventPage.PageIndex;
            }
            metrics.Clear(); metricRows.Clear();
            if (drawMetrics)
                foreach (var provider in settings.drivingInfoSettings.OrderedProviders().ToArray())
                {
                    var value = "--";
                    if (sampledCar != null && provider.TryGetFormatted(sampledCar, out var formatted) && !string.IsNullOrEmpty(formatted)) value = formatted;
                    metrics.Add((provider.Label, value, Styles.TextWidth(provider.Label, Styles.noWrap), Styles.TextWidth(value, Styles.noWrapBold)));
                }
            var ordered = settings.drivingInfoSettings.OrderedProviders().ToArray();
            var assignments = ordered.Select(p => settings.drivingInfoSettings.GetProviderSettings(p).column).ToArray();
            var groups = ordered.Select(p => MetricGroups.Of(p.Label)).ToArray();
            metricColumns = MetricColumns.Arrange(assignments, groups, columns);
            var used = 0f;
            for (var index = 0; index < metrics.Count; index++)
            {
                var cellWidth = Mathf.Min(metricsWidth, metrics[index].labelWidth + 5 + metrics[index].valueWidth);
                if (metricRows.Count == 0 || used + ColumnSpacing + cellWidth > metricsWidth)
                { metricRows.Add(new List<int>()); used = 0; }
                metricRows[metricRows.Count - 1].Add(index);
                used += (used > 0 ? ColumnSpacing : 0) + cellWidth;
            }
            layoutReady = true;
        }
        private void DrawMainContent()
        {
            if (dockMetrics) DrawMetrics();
            if (dockGraph) { GUILayout.Space(4); RouteMonitor.DrawPassive(); }
            if (showEvents) DrawEvents();
            if (summary.Length > 0) { GUILayout.Space(4); GUILayout.Label(summary, Styles.noWrap, GUILayout.Width(innerWidth)); }
            if (drawCarList && sampledCar?.trainset != null)
            {
                var live = Main.settings.trainInfoSettings;
                try { Main.settings.trainInfoSettings = trainSettings; CarList.DrawCarList(sampledCar.trainset); }
                finally { Main.settings.trainInfoSettings = live; }
            }
        }
        private void DrawMetrics()
        {
            if (compact)
            {
                var columnWidth = Mathf.Max(1, (metricsWidth - (columns - 1) * ColumnSpacing) / columns);
                GUILayout.BeginHorizontal(GUILayout.Width(metricsWidth));
                for (var c = 0; c < metricColumns.Count; c++)
                {
                    if (c > 0) GUILayout.Space(ColumnSpacing);
                    GUILayout.BeginVertical(GUILayout.Width(columnWidth));
                    foreach (var index in metricColumns[c]) DrawMetric(index, columnWidth, true);
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
            else foreach (var row in metricRows)
            {
                GUILayout.BeginHorizontal(GUILayout.Width(metricsWidth));
                foreach (var index in row)
                {
                    if (index != row[0]) GUILayout.Space(ColumnSpacing);
                    DrawMetric(index, Mathf.Min(metricsWidth, metrics[index].labelWidth + 5 + metrics[index].valueWidth), false);
                }
                GUILayout.EndHorizontal();
            }
        }
        private void DrawMetric(int index, float cellWidth, bool column)
        {
            var metric = metrics[index];
            var valueWidth = Mathf.Min(metric.valueWidth, Mathf.Max(1, cellWidth - 6));
            var labelWidth = Mathf.Min(metric.labelWidth, Mathf.Max(1, cellWidth - valueWidth - 5));
            GUILayout.BeginHorizontal(GUILayout.Width(cellWidth));
            GUILayout.Label(metric.label, Styles.noWrap, GUILayout.Width(labelWidth));
            if (column) GUILayout.FlexibleSpace(); else GUILayout.Space(5);
            GUILayout.Label(metric.value, Styles.noWrapBold, GUILayout.Width(valueWidth));
            GUILayout.EndHorizontal();
        }
        private void DrawEvents()
        {
            if (eventPage == null) return;
            GUILayout.Space(4);
            GUILayout.Label($"Route events  {eventPage.TotalCount} displayed / {RouteMonitorService.Current.Events.Count} retained", Styles.noWrapBold, GUILayout.Width(innerWidth));
            GUILayout.BeginHorizontal();
            var enabled = GUI.enabled;
            GUI.enabled = enabled && eventPage.PageIndex > 0;
            if (GUILayout.Button("Previous", Styles.button, GUILayout.ExpandWidth(false))) requestedPage--;
            GUI.enabled = enabled && eventPage.PageIndex + 1 < eventPage.PageCount;
            if (GUILayout.Button("Next", Styles.button, GUILayout.ExpandWidth(false))) requestedPage++;
            GUI.enabled = enabled;
            GUILayout.Label($"Page {(eventPage.PageCount == 0 ? 0 : eventPage.PageIndex + 1)} / {eventPage.PageCount}", Styles.noWrap);
            GUILayout.EndHorizontal();
            // Measured columns, so distances line up and the kind is readable at a glance.
            var rows = eventPage.Items.Select(RouteMonitor.EventRow).ToArray();
            var distanceWidth = 0f;
            var kindWidth = 0f;
            var markerWidth = 0f;
            var codeWidth = 0f;
            foreach (var row in rows)
            {
                distanceWidth = Mathf.Max(distanceWidth, Styles.TextWidth(row.Distance, Styles.rightAlign));
                kindWidth = Mathf.Max(kindWidth, Styles.TextWidth(row.Kind, Styles.noWrap));
                markerWidth = Mathf.Max(markerWidth, Styles.TextWidth(row.Marker, Styles.noWrap));
                codeWidth = Mathf.Max(codeWidth, Styles.TextWidth(row.Code, Styles.noWrap));
            }
            // The description keeps whatever is left, and never less than a readable minimum.
            var columns = 2 + (markerWidth > 0 ? 1 : 0);
            if (innerWidth - (distanceWidth + kindWidth + markerWidth + codeWidth + (columns + 1) * ColumnSpacing) < 120)
                codeWidth = 0;
            if (codeWidth > 0) columns++;
            var descriptionWidth = Mathf.Max(60,
                innerWidth - (distanceWidth + kindWidth + markerWidth + codeWidth + columns * ColumnSpacing));
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                var saved = GUI.contentColor;
                GUILayout.BeginHorizontal(GUILayout.Width(innerWidth));
                GUILayout.Label(row.Distance, Styles.rightAlign, GUILayout.Width(distanceWidth));
                GUILayout.Space(ColumnSpacing);
                GUI.contentColor = new Color(saved.r, saved.g, saved.b, saved.a * 0.65f);
                GUILayout.Label(row.Kind, Styles.noWrap, GUILayout.Width(kindWidth));
                GUI.contentColor = saved;
                GUILayout.Space(ColumnSpacing);
                if (markerWidth > 0)
                {
                    GUILayout.Label(row.Marker, Styles.noWrap, GUILayout.Width(markerWidth));
                    GUILayout.Space(ColumnSpacing);
                }
                if (eventPage.Items[index].Kind == RouteEventKind.Signal)
                    GUI.contentColor = RouteMonitor.EventColor(eventPage.Items[index]);
                GUILayout.Label(row.Description, Styles.noWrap, GUILayout.Width(descriptionWidth));
                GUI.contentColor = saved;
                if (codeWidth > 0)
                {
                    GUILayout.Space(ColumnSpacing);
                    GUI.contentColor = new Color(saved.r, saved.g, saved.b, saved.a * 0.5f);
                    GUILayout.Label(row.Code, Styles.noWrap, GUILayout.Width(codeWidth));
                    GUI.contentColor = saved;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Display filters and pages do not remove retained events. Full source details are in the UMM inspector.", Styles.noWrap, GUILayout.Width(innerWidth));
        }
        private void CopyTrainSettings()
        {
            var live = Main.settings.trainInfoSettings;
            trainSettings.enabled = live.enabled; trainSettings.showTrainInfo = live.showTrainInfo;
            trainSettings.lengthUnits = live.lengthUnits; trainSettings.showCarList = live.showCarList;
            trainSettings.updatePeriod = live.updatePeriod; trainSettings.groupCarsByJob = live.groupCarsByJob;
            trainSettings.showCarStress = live.showCarStress; trainSettings.showCarJobs = live.showCarJobs;
            trainSettings.showCarDestinations = live.showCarDestinations; trainSettings.showCarBrakeStatus = live.showCarBrakeStatus;
            drawCarList = live.enabled && live.showCarList;
        }
        private string BuildTrainSummary()
        {
            var train = sampledCar?.trainset;
            if (!trainSettings.enabled || !trainSettings.showTrainInfo || train == null) return "";
            var length = trainSettings.lengthUnits == Settings.TrainInfoSettings.LengthUnits.ft
                ? $"{new Quantities.Length(train.OverallLength()).In(Foot):F0} ft" : $"{train.OverallLength():F0} m";
            return $"Train  {train.cars.Count} cars  {length}  {train.TotalMass() / 1000:F0} t";
        }
        public static void DrawColumn<T>(IEnumerable<T> cells, string? label = null, Func<T, string>? renderer = null, GUIStyle? style = null)
        {
            renderer ??= x => x!.ToString(); style ??= Styles.noWrap;
            GUILayout.Space(ColumnSpacing); GUILayout.BeginVertical();
            if (label != null) GUILayout.Label(label, style);
            foreach (var cell in cells) GUILayout.Label(renderer(cell), style);
            GUILayout.EndVertical();
        }
        public void ResetPosition() { Release(); layoutReady = false; }
        private void Release() { mainWindow.Release(); graphWindow.Release(); metricsWindow.Release(); }
        public void OnDisable() { Release(); layoutReady = false; if (instance == this) instance = null; }
        public void OnApplicationQuit() => Main.SavePendingSettings();
    }
}
