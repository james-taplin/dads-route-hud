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
        private static readonly GUIContent measurement = new GUIContent();
        private static int appliedFontSize;

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
        }

        public static void SetFontSize(int fontSize)
        {
            if (appliedFontSize == fontSize) return;
            appliedFontSize = fontSize;
            foreach (var style in new[] { noWrap, noWrapBold, centeredBold, rightAlign, richText })
            {
                style.fontSize = fontSize;
                style.fixedHeight = fontSize + 6;
                style.margin = new RectOffset();
                style.padding = new RectOffset();
                style.normal.textColor = Color.white;
            }
        }

        public static float TextWidth(string text, GUIStyle style)
        {
            measurement.text = text;
            return Mathf.Ceil(style.CalcSize(measurement).x) + 1;
        }
    }

    public class Overlay : MonoBehaviour
    {
        public const int ColumnSpacing = 10;
        public static Overlay? instance;
        private readonly HudWindow mainWindow = new HudWindow(320);
        private readonly HudWindow graphWindow = new HudWindow(400);
        private bool layoutReady, showMain, showGraph, dockGraph, showEvents, compact, drawMetrics, drawCarList;
        private float innerWidth;
        private int fontSize, columns;
        private string identity = "", summary = "";
        private TrainCar? sampledCar;
        private Rect mainRect, graphRect;
        private float background, content, graphBackground, graphContent;
        private bool locked;
        private string routeActionLabel = "";
        private Action drawMain = null!, drawGraph = null!, toggleRoute = null!, attachRoute = null!;
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
            attachRoute = AttachRouteWindow;
        }
        public void OnGUI()
        {
            if (!Main.enabled || drawMain == null) { Release(); layoutReady = false; return; }
            if (!layoutReady && Event.current.type != EventType.Layout) return;
            if (Event.current.type == EventType.Layout) CaptureLayout();
            if (showMain)
            {
                var allowStart = !showGraph || !graphRect.Contains(Event.current.mousePosition) || mainWindow.OwnsInput;
                mainWindow.Draw(ref mainRect, identity, background, content, locked, allowStart, drawMain, routeActionLabel, toggleRoute);
                Main.settings.hudPosition = mainRect.position;
                if (compact) Main.settings.compactSize = mainRect.size;
                else Main.settings.hudSize = mainRect.size;
            }
            else mainWindow.Release();
            if (showGraph)
            {
                graphWindow.Draw(ref graphRect, "Track ahead", graphBackground, graphContent, locked, true, drawGraph, "Attach", attachRoute);
                Main.settings.graphPosition = graphRect.position;
                Main.settings.graphSize = graphRect.size;
            }
            else graphWindow.Release();
        }
        private static void ToggleRouteWindow() => SetRouteWindow(!Main.settings.layoutSettings.popOutGraph);
        private static void AttachRouteWindow() => SetRouteWindow(false);
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
            mainRect = mainWindow.Clamp(new Rect(settings.hudPosition, compact ? settings.compactSize : settings.hudSize));
            graphRect = graphWindow.Clamp(new Rect(settings.graphPosition, settings.graphSize));
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
            innerWidth = Math.Max(1, mainRect.width - 38);
            fontSize = layout.fontSize;
            Styles.SetFontSize(fontSize);
            background = layout.backgroundOpacity; content = layout.contentOpacity;
            graphBackground = layout.graphBackgroundOpacity; graphContent = layout.graphContentOpacity;
            locked = settings.lockPosition;
            RouteMonitor.PlotBackgroundOpacity = layout.popOutGraph ? graphBackground : background;
            showMain = layout.showHud;
            routeActionLabel = !settings.trackInfoSettings.enabled ? "" : layout.popOutGraph ? "Attach route" : "Detach route";
            showGraph = settings.trackInfoSettings.enabled && layout.popOutGraph && layout.showGraphWindow;
            dockGraph = settings.trackInfoSettings.enabled && !layout.popOutGraph
                && layout.routeDisplay != Settings.LayoutSettings.RouteDisplay.EventList;
            showEvents = settings.trackInfoSettings.enabled && layout.routeDisplay != Settings.LayoutSettings.RouteDisplay.Graph;
            drawMetrics = settings.drivingInfoSettings.enabled;
            sampledCar = PlayerManager.Car ? PlayerManager.Car : PlayerManager.LastLoco;
            identity = (compact ? "HUD" : "DRIVING") + (sampledCar != null ? $"  {sampledCar.ID}  {sampledCar.carType}" : "  No locomotive selected");
            CopyTrainSettings();
            summary = BuildTrainSummary();
            if (dockGraph || showGraph)
                RouteMonitor.PrepareLayout(RouteMonitorService.Current, showGraph ? graphRect.width - 38 : innerWidth, fontSize, layout);
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
            var used = 0f;
            for (var index = 0; index < metrics.Count; index++)
            {
                var cellWidth = compact ? innerWidth / columns : Mathf.Min(innerWidth, metrics[index].labelWidth + 5 + metrics[index].valueWidth);
                if (metricRows.Count == 0 || (compact ? metricRows[metricRows.Count - 1].Count == columns : used + ColumnSpacing + cellWidth > innerWidth))
                { metricRows.Add(new List<int>()); used = 0; }
                metricRows[metricRows.Count - 1].Add(index);
                used += (used > 0 ? ColumnSpacing : 0) + cellWidth;
            }
            layoutReady = true;
        }
        private void DrawMainContent()
        {
            if (drawMetrics) DrawMetrics();
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
            foreach (var row in metricRows)
            {
                GUILayout.BeginHorizontal(GUILayout.Width(innerWidth));
                foreach (var index in row)
                {
                    if (index != row[0]) GUILayout.Space(ColumnSpacing);
                    var metric = metrics[index];
                    var cellWidth = compact ? (innerWidth - (columns - 1) * ColumnSpacing) / columns : innerWidth;
                    var valueWidth = Mathf.Min(metric.valueWidth, Mathf.Max(1, cellWidth - 6));
                    var labelWidth = Mathf.Min(metric.labelWidth, Mathf.Max(1, cellWidth - valueWidth - 5));
                    GUILayout.BeginHorizontal(GUILayout.Width(compact ? cellWidth : labelWidth + 5 + valueWidth));
                    GUILayout.Label(metric.label, Styles.noWrap, GUILayout.Width(labelWidth));
                    if (compact) GUILayout.FlexibleSpace(); else GUILayout.Space(5);
                    GUILayout.Label(metric.value, Styles.noWrapBold, GUILayout.Width(valueWidth));
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndHorizontal();
            }
        }
        private void DrawEvents()
        {
            if (eventPage == null) return;
            GUILayout.Space(4);
            GUILayout.Label($"Route events  {eventPage.TotalCount} displayed / {RouteMonitorService.Current.Events.Count} retained", Styles.noWrapBold, GUILayout.Width(innerWidth));
            GUILayout.BeginHorizontal();
            var enabled = GUI.enabled;
            GUI.enabled = enabled && eventPage.PageIndex > 0;
            if (GUILayout.Button("Previous", GUILayout.ExpandWidth(false))) requestedPage--;
            GUI.enabled = enabled && eventPage.PageIndex + 1 < eventPage.PageCount;
            if (GUILayout.Button("Next", GUILayout.ExpandWidth(false))) requestedPage++;
            GUI.enabled = enabled;
            GUILayout.Label($"Page {(eventPage.PageCount == 0 ? 0 : eventPage.PageIndex + 1)} / {eventPage.PageCount}", Styles.noWrap);
            GUILayout.EndHorizontal();
            foreach (var item in eventPage.Items)
            {
                var saved = GUI.contentColor;
                if (item.Kind == RouteEventKind.Signal) GUI.contentColor = RouteMonitor.EventColor(item);
                GUILayout.Label(RouteMonitor.FormatEvent(item), Styles.noWrap, GUILayout.Width(innerWidth));
                GUI.contentColor = saved;
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
        private void Release() { mainWindow.Release(); graphWindow.Release(); }
        public void OnDisable() { Release(); layoutReady = false; if (instance == this) instance = null; }
        public void OnApplicationQuit() => Main.SavePendingSettings();
    }
}
