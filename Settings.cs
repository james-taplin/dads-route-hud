using System;
using System.Collections.Generic;
using System.Linq;
using QuantitiesNet;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HeadsUpDisplay
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        public class DrivingInfoSettings
        {
            public bool enabled = true;
            public HashSet<string> disabledProviders = new HashSet<string>(Registry.providers.Keys);
            public List<ProviderSettings> providerSettings = new List<ProviderSettings>();

            private (int providerIndex, bool moveUp)? orderChange = null;
            private string search = "";
            private int selectedGroup;
            private readonly List<(ProviderSettings settings, int index)> visibleProviderSettings = new List<(ProviderSettings, int)>();

            private static readonly string[] Groups = { "All", "Controls", "Steam", "Performance", "Vehicle", "Native ports" };
            private static readonly HashSet<string> ControlLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Throttle", "Reverser", "Train brake", "Independent brake", "Dynamic brake", "Handbrake",
                "Sanders", "Horn / whistle", "Brake cutout", "Dynamo", "Air pump", "Throttle notch", "Train brake notch",
                "Independent brake notch", "Dynamic brake notch", "Cutoff", "Firedoor", "Damper", "Blower", "Injector",
                "Cylinder cocks", "Blowdown", "Coal dump", "Lubricator", "Gear", "Gear ratio", "Gear change"
            };
            private static readonly HashSet<string> SteamLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Cutoff", "Firedoor", "Damper", "Blower", "Injector", "Cylinder cocks", "Blowdown", "Coal dump",
                "Lubricator", "Dynamo", "Air pump", "Injector flow", "Blowdown flow", "Safety valve", "Crown sheet heat",
                "Cylinder temperature", "Water in cylinders", "Water tank level", "Coal reserve"
            };
            private static readonly HashSet<string> PerformanceLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Speed", "Acceleration", "Tractive effort", "Power", "Brake pipe", "Brake cylinder",
                "Brake force", "Brake application", "Cylinder application", "Main reservoir", "Aux reservoir",
                "Brake temperature", "Brake overheating", "Adhesion", "Adhesion demand", "Slip", "Grade", "Altitude",
                "Engine RPM", "Engine RPM limit", "Engine load torque", "Engine temperature", "Retarder effect",
                "Fuel consumption demand", "Kinetic energy", "Grade resistance"
            };

            static DrivingInfoSettings()
            {
                UnitRegistry.Default.Add(Unit.Of<Dimensions.MassFlow>("kg/h", 1, QuantitiesNet.Units.Kilogram / QuantitiesNet.Units.Hour));
                UnitRegistry.Default.Add(Unit.Of<Dimensions.MassFlow>("lb/h", 1, QuantitiesNet.Units.Pound / QuantitiesNet.Units.Hour));
                UnitRegistry.Default.Add(Unit.Of<Dimensions.Power>("btu/m", 1, QuantitiesNet.Units.Btu / QuantitiesNet.Units.Minute));
            }

            public class ProviderSettings
            {
                public string providerLabel = "";
                public string unitSymbol = "";
                public int precision;

                public override string ToString() => $"{providerLabel}: {unitSymbol}, {precision}";

                public bool TryGetUnit(Dimension dimension, out Unit unit)
                {
                    var symbol = unitSymbol;
                    if (symbol.Length == 0)
                        symbol = GetDisplaySymbols(dimension).FirstOrDefault() ?? "";
                    var maybeUnit = UnitForSymbol(dimension, symbol);
                    if (maybeUnit == null)
                    {
#pragma warning disable CS8625
                        unit = default;
#pragma warning restore CS8625
                        return false;
                    }

                    unit = (Unit)maybeUnit;
                    return true;
                }

                private static Unit? UnitForSymbol(Dimension dimension, string symbol)
                {
                    if (UnitRegistry.Default.TryGetUnits(dimension, out var units))
                    {
                        return units.Find(unit => unit.Symbol == symbol);
                    }
                    return default;
                }
            }

            private void DrawOrderButtons(int providerIndex)
            {
                GUILayout.BeginVertical(GUILayout.Width(30));

                GUI.enabled = providerIndex > 0;
                var upPressed = GUILayout.Button("^");//, GUILayout.Width(30), GUILayout.ExpandWidth(false));
                GUI.enabled = providerIndex + 1 < providerSettings.Count;
                var downPressed = GUILayout.Button("v");//, GUILayout.Width(30), GUILayout.ExpandWidth(false));
                GUI.enabled = true;

                if (upPressed)
                    orderChange = (providerIndex, moveUp: true);
                if (downPressed)
                    orderChange = (providerIndex, moveUp: false);

                GUILayout.EndVertical();
            }

            private void DrawPrecisionSettings(ProviderSettings settings)
            {
                UnityModManager.UI.DrawIntField(
                    ref settings.precision, "Precision", style: null, GUILayout.Width(20), GUILayout.ExpandWidth(false));
                settings.precision = Mathf.Clamp(settings.precision, 0, 6);
            }

            private void DrawUnitSettings(IQuantityProvider provider)
            {
                var dimension = provider.Dimension;
                var settings = GetProviderSettings(provider);

                var symbols = GetDisplaySymbols(dimension).ToList();
                if (symbols.Count == 0)
                    return;

                var currentSymbol = settings.unitSymbol;
                var selectedIndex = symbols.IndexOf(currentSymbol);
                if (selectedIndex < 0)
                {
                    settings.unitSymbol = symbols[0];
                    selectedIndex = 0;
                }

                var changed = UnityModManager.UI.ToggleGroup(
                    ref selectedIndex, symbols.ToArray(), style: null, GUILayout.MinWidth(50), GUILayout.ExpandWidth(false));
                if (changed)
                    settings.unitSymbol = symbols[selectedIndex];
            }

            private void DrawProviderSettings(ProviderSettings settings, int providerIndex)
            {
                var label = settings.providerLabel;
                if (!Registry.providers.TryGetValue(label, out var provider) || provider.Hidden)
                    return;

                GUILayout.BeginHorizontal("box");

                DrawOrderButtons(providerIndex);

                GUILayout.BeginVertical();
                GUILayout.Space(5);

                GUILayout.BeginHorizontal();

                var result = GUILayout.Toggle(
                    !disabledProviders.Contains(label), image: null, GUILayout.Width(20), GUILayout.ExpandWidth(false));
                if (result)
                    disabledProviders.Remove(label);
                else
                    disabledProviders.Add(label);

                GUILayout.Label(label, GUILayout.MinWidth(150), GUILayout.ExpandWidth(false));

                if (provider is IQuantityProvider quantityProvider)
                {
                    DrawPrecisionSettings(settings);
                    DrawUnitSettings(quantityProvider);
                }

                GUILayout.EndHorizontal();
                GUILayout.EndVertical();

                GUILayout.EndHorizontal();
            }

            private void DrawProviderSettings()
            {
                orderChange = null;

                // Native port discovery can append settings during Update. Keep the same rows
                // from Layout through Repaint instead of replaying a newly enlarged catalogue.
                if (Event.current.type == EventType.Layout)
                {
                    visibleProviderSettings.Clear();
                    foreach (var (settings, index) in providerSettings.Select((x, i) => (x, i)))
                    {
                        var label = settings.providerLabel;
                        if (label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 && MatchesGroup(label))
                            visibleProviderSettings.Add((settings, index));
                    }
                }

                foreach (var (settings, index) in visibleProviderSettings)
                    DrawProviderSettings(settings, index);

                if (orderChange.HasValue)
                {
                    var index = orderChange.Value.providerIndex;
                    var settings = providerSettings[index];
                    providerSettings.RemoveAt(index);
                    providerSettings.Insert(index + (orderChange.Value.moveUp ? -1 : 1), settings);
                }
            }

            public void Draw()
            {
                GUILayout.Label("Driving metrics");
                GUILayout.BeginVertical("box");

                GUILayout.BeginHorizontal();
                enabled = GUILayout.Toggle(enabled, "Show driving metrics");
                GUILayout.EndHorizontal();

                if (enabled)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Driving", GUILayout.ExpandWidth(false)))
                        SelectProviders(DrivingLabels);
                    if (GUILayout.Button("All metrics", GUILayout.ExpandWidth(false)))
                        SelectProviders(Registry.providers.Values.Where(p => !p.Hidden && !IsNativePort(p.Label)).Select(p => p.Label));
                    if (GUILayout.Button("Clear", GUILayout.ExpandWidth(false)))
                        SelectProviders(Enumerable.Empty<string>());
                    GUILayout.Label($"{OrderedProviders().Count()} selected", GUILayout.ExpandWidth(false));
                    GUILayout.EndHorizontal();
                    GUILayout.Label("These buttons change the selection. Your metric order, units and precision are retained.");
                    GUILayout.Label("Unavailable values show --. Native ports are optional simulation diagnostics; their raw values have no assumed units.");
                    GUILayout.Label("Power/tractive effort show demand before adhesion limits. Air pressures use gauge units; handle % and cylinder application differ.");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Search", GUILayout.ExpandWidth(false));
                    search = GUILayout.TextField(search);
                    if (GUILayout.Button("Clear search", GUILayout.ExpandWidth(false)))
                        search = "";
                    GUILayout.EndHorizontal();
                    selectedGroup = GUILayout.SelectionGrid(selectedGroup, Groups, Groups.Length);
                    DrawProviderSettings();
                }
                GUILayout.EndVertical();
            }

            private bool MatchesGroup(string label)
            {
                switch (selectedGroup)
                {
                    case 1: return ControlLabels.Contains(label);
                    case 2: return IsSteamLabel(label);
                    case 3: return PerformanceLabels.Contains(label);
                    case 4: return !IsNativePort(label) && !ControlLabels.Contains(label) && !IsSteamLabel(label) && !PerformanceLabels.Contains(label);
                    case 5: return IsNativePort(label);
                    default: return true;
                }
            }

            private static bool IsNativePort(string label) => label.StartsWith("Sim: ", StringComparison.Ordinal);
            private static bool IsSteamLabel(string label) => SteamLabels.Contains(label)
                || label.StartsWith("Boiler ", StringComparison.OrdinalIgnoreCase)
                || label.StartsWith("Steam ", StringComparison.OrdinalIgnoreCase)
                || label.StartsWith("Firebox ", StringComparison.OrdinalIgnoreCase);

            public static readonly string[] DrivingLabels =
            {
                "Speed", "Throttle", "Reverser", "Train brake", "Independent brake", "Dynamic brake",
                "Power", "Tractive effort", "Brake pipe", "Grade"
            };

            public void SelectProviders(IEnumerable<string> labels)
            {
                // Keep dormant saved ports disabled too: their providers are registered only
                // after entering a compatible car, while their settings persist across sessions.
                var selected = new HashSet<string>(labels);
                var knownLabels = providerSettings.Select(settings => settings.providerLabel).Concat(Registry.providers.Keys);
                disabledProviders = new HashSet<string>(knownLabels.Where(label => !selected.Contains(label)));
            }

            public bool IsEnabled(IDataProvider dp) => !disabledProviders.Contains(dp.Label);

            private readonly static Dictionary<Dimension, List<string>> KnownUnits = new Dictionary<Dimension, List<string>>()
            {
                { Dimensions.Force.dimension, new List<string>() { "kN", "lbf" } },
                { Dimensions.Length.dimension, new List<string>() { "m", "ft" } },
                { Dimensions.Mass.dimension, new List<string>() { "kg", "lb" } },
                { Dimensions.Power.dimension, new List<string>() { "kW", "hp", "btu/m" } },
                { Dimensions.Pressure.dimension, new List<string>() { "bar", "psi" } },
                { Dimensions.Velocity.dimension, new List<string>() { "km/h", "mph" } },
            };

            private static IEnumerable<string> GetDisplaySymbols(Dimension dimension)
            {
                if (KnownUnits.TryGetValue(dimension, out var symbols))
                    return symbols;

                // fallback to all registered units if not known
                if (UnitRegistry.Default.TryGetUnits(dimension, out var units))
                    return units.Select(unit => unit.Symbol);

                return Enumerable.Empty<string>();
            }

            public void EnsureProviderSettings(IDataProvider provider)
            {
                GetProviderSettings(provider);
            }

            public ProviderSettings GetProviderSettings(IDataProvider provider)
            {
                var label = provider.Label;
                var settings = providerSettings.Find(p => p.providerLabel == label);
                if (settings == default)
                {
                    settings = new ProviderSettings()
                    {
                        providerLabel = label,
                        precision = provider is IQuantityProvider quantity && quantity.Dimension.Equals(Dimensions.Pressure.dimension) ? 1 : 0,
                    };
                    providerSettings.Add(settings);
                }
                return settings;
            }

            public IEnumerable<IDataProvider> OrderedProviders()
            {
                foreach (var label in providerSettings.Select(settings => settings.providerLabel))
                {
                    if (Registry.providers.TryGetValue(label, out var provider)
                        && !provider.Hidden && IsEnabled(provider))
                        yield return provider;
                }
            }
        }

        public class TrackInfoSettings
        {
            public enum EventDistanceUnits
            {
                m,
                mi,
                ft,
            }
            [Draw("Enable")] public bool enabled = true;
            // Retained for the unused inherited list renderer, never a route collection cap.
            public int maxEventCount = 10;
            [Draw("Look-ahead metres (max 50000)", VisibleOn = "enabled|true")] public double maxEventSpan = 50000;
            [Draw("Distance units", Type = DrawType.ToggleGroup)] public EventDistanceUnits distanceUnits = EventDistanceUnits.m;
        }

        public class TrainInfoSettings
        {
            public enum LengthUnits
            {
                m,
                ft,
            }
            [Draw("Enable")] public bool enabled = true;
            [Draw("Length & mass")] public bool showTrainInfo = true;
            [Draw("Length units", Type = DrawType.ToggleGroup)] public LengthUnits lengthUnits = LengthUnits.m;
            [Draw("Car list")] public bool showCarList;
            [Draw("Update period", Min = 0f, VisibleOn = "showCarList|true")] public float updatePeriod = 0.25f;
            [Draw("Group by job", VisibleOn = "showCarList|true")] public bool groupCarsByJob = true;
            [Draw("Stress", VisibleOn = "showCarList|true")] public bool showCarStress = true;
            [Draw("Job ID", VisibleOn = "showCarList|true")] public bool showCarJobs = true;
            [Draw("Destination", VisibleOn = "showCarList|true")] public bool showCarDestinations = true;
            [Draw("Brake status", VisibleOn = "showCarList|true")] public bool showCarBrakeStatus = true;
        }

        public static Vector2 defaultPosition = new Vector2(10, 10);

        public class LayoutSettings
        {
            public enum DisplayMode { DrivingStrip, Compact }
            public enum RouteDisplay { Graph, EventList, GraphAndEvents }
            public DisplayMode displayMode = DisplayMode.DrivingStrip;
            public RouteDisplay routeDisplay = RouteDisplay.Graph;
            public bool showHud = true;
            public bool popOutGraph;
            public bool showGraphWindow = true;
            public float contentOpacity = 1f;
            public float graphBackgroundOpacity = 0.72f;
            public float graphContentOpacity = 1f;
            public int profileHeight = 50;
            public bool showRouteStatus = true;
            public bool showNearProfile = true;
            public bool showOverview = true;
            public bool showNextEvent = true;
            public bool showTracks = true;
            public bool showJunctions = true;
            public bool showSpeeds = true;
            public bool showGrades = true;
            public bool showSignals = true;
            public bool showOtherSigns = true;
            public bool showOpposite = true;
            public int fontSize = 15;
            public int columns = 2;
            public float backgroundOpacity = 0.72f;
            public int stripWidth = 1000;
            public int nearDistance = 5000;
            public bool expandedAtlas;
            public bool inspectEveryEvent;
            public int inspectorPageSize = 50;
        }

        public LayoutSettings layoutSettings = new LayoutSettings();
        public int windowLayoutVersion;
        public bool placeHudTopRight = true;
        public bool placeGraphTopRight = true;
        public Vector2 hudSize = new Vector2(1020, 480);
        public Vector2 compactSize = new Vector2(520, 480);
        public Vector2 graphPosition = new Vector2(40, 520);
        public Vector2 graphSize = new Vector2(1100, 360);
        public bool drivingDefaultsApplied;

        public DrivingInfoSettings drivingInfoSettings = new DrivingInfoSettings();

        [Draw("Route collection", Collapsible = true, Box = true)]
        public TrackInfoSettings trackInfoSettings = new TrackInfoSettings();

        [Draw("Train info", Collapsible = true, Box = true)]
        public TrainInfoSettings trainInfoSettings = new TrainInfoSettings();

        [Draw("Enable logging")] public bool enableLogging;
        [Draw("Lock window positions and sizes")] public bool lockPosition;

        public readonly string? version = Main.mod?.Info.Version;

        public Vector2 hudPosition = defaultPosition;

        public bool IsEnabled(IDataProvider dp) => drivingInfoSettings.IsEnabled(dp);

        override public void Save(UnityModManager.ModEntry entry) => Save<Settings>(this, entry);

        public void Draw()
        {
            ClampLayout();
            GUILayout.Label("HUD layout");
            GUILayout.BeginVertical("box");
            layoutSettings.showHud = GUILayout.Toggle(layoutSettings.showHud, "Show HUD window");
            layoutSettings.displayMode = (LayoutSettings.DisplayMode)GUILayout.Toolbar((int)layoutSettings.displayMode, new[] { "Driving strip", "Compact columns" });
            UnityModManager.UI.DrawIntField(ref layoutSettings.columns, "Compact metric columns (1-4)");
            layoutSettings.routeDisplay = (LayoutSettings.RouteDisplay)GUILayout.Toolbar((int)layoutSettings.routeDisplay, new[] { "Route graph", "Route event list", "Graph + events" });
            layoutSettings.popOutGraph = GUILayout.Toggle(layoutSettings.popOutGraph, "Render route graph in a separate window");
            if (layoutSettings.popOutGraph)
                layoutSettings.showGraphWindow = GUILayout.Toggle(layoutSettings.showGraphWindow, "Show separate route window");
            UnityModManager.UI.DrawIntField(ref layoutSettings.fontSize, "Font size (10-28)");
            if (layoutSettings.displayMode == LayoutSettings.DisplayMode.Compact) WindowSize("Compact HUD", ref compactSize);
            else WindowSize("Driving strip", ref hudSize);
            if (layoutSettings.popOutGraph) WindowSize("Separate route window", ref graphSize);
            UnityModManager.UI.DrawIntField(ref layoutSettings.nearDistance, "Near view metres (250-10000)");
            UnityModManager.UI.DrawIntField(ref layoutSettings.profileHeight, "Grade profile height (20-300)");
            Opacity("HUD background", ref layoutSettings.backgroundOpacity);
            Opacity("HUD text and graphics", ref layoutSettings.contentOpacity);
            Opacity("Separate route background", ref layoutSettings.graphBackgroundOpacity);
            Opacity("Separate route text and graphics", ref layoutSettings.graphContentOpacity);
            GUILayout.Label("Windows are independent of Mod Toolbar. With the game cursor available, drag a header or its bottom-right resize grip. Lock geometry below when finished. Scroll to see all content in smaller windows.");
            if (GUILayout.Button("Reset both window positions and sizes", GUILayout.ExpandWidth(false))) ResetWindows();
            layoutSettings.showRouteStatus = GUILayout.Toggle(layoutSettings.showRouteStatus, "Route status and coverage");
            layoutSettings.showNearProfile = GUILayout.Toggle(layoutSettings.showNearProfile, "Near grade profile");
            layoutSettings.showOverview = GUILayout.Toggle(layoutSettings.showOverview, "Full-range overview");
            layoutSettings.showNextEvent = GUILayout.Toggle(layoutSettings.showNextEvent, "Next event summary");
            layoutSettings.expandedAtlas = GUILayout.Toggle(layoutSettings.expandedAtlas, "Expand the passive route atlas");
            layoutSettings.inspectEveryEvent = GUILayout.Toggle(layoutSettings.inspectEveryEvent, "Inspect every retained route event here");
            GUILayout.Label("Displayed event types (collection and the complete inspector retain everything)");
            layoutSettings.showTracks = GUILayout.Toggle(layoutSettings.showTracks, "Track entries");
            layoutSettings.showJunctions = GUILayout.Toggle(layoutSettings.showJunctions, "Junctions");
            layoutSettings.showSpeeds = GUILayout.Toggle(layoutSettings.showSpeeds, "Speed signs");
            layoutSettings.showGrades = GUILayout.Toggle(layoutSettings.showGrades, "Grade signs");
            layoutSettings.showSignals = GUILayout.Toggle(layoutSettings.showSignals, "Signals and reservation facts");
            layoutSettings.showOtherSigns = GUILayout.Toggle(layoutSettings.showOtherSigns, "Other signs and accessories");
            layoutSettings.showOpposite = GUILayout.Toggle(layoutSettings.showOpposite, "Opposite-facing observations");
            if (layoutSettings.inspectEveryEvent)
                UnityModManager.UI.DrawIntField(ref layoutSettings.inspectorPageSize, "Inspector rows per page (10-100)");
            GUILayout.EndVertical();
            drivingInfoSettings.Draw();
            this.Draw(Main.mod);
            if (layoutSettings.inspectEveryEvent)
                RouteMonitor.DrawInspector(RouteMonitorService.Current);
            ClampLayout();
        }

        public void OnChange()
        {
            ClampLayout();
        }

        private static void Opacity(string label, ref float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {value:P0}", GUILayout.Width(240));
            value = GUILayout.HorizontalSlider(value, 0f, 1f);
            GUILayout.EndHorizontal();
        }

        private static void WindowSize(string label, ref Vector2 value)
        {
            var width = Mathf.RoundToInt(value.x);
            var height = Mathf.RoundToInt(value.y);
            UnityModManager.UI.DrawIntField(ref width, label + " width (pixels)");
            UnityModManager.UI.DrawIntField(ref height, label + " height (pixels)");
            value = new Vector2(Mathf.Clamp(width, 320, 16384), Mathf.Clamp(height, 120, 16384));
        }

        public void ResetWindows()
        {
            hudPosition = defaultPosition;
            placeHudTopRight = placeGraphTopRight = true;
            hudSize = new Vector2(1020, 480);
            compactSize = new Vector2(520, 480);
            graphPosition = new Vector2(40, 520);
            graphSize = new Vector2(1100, 360);
            Overlay.instance?.ResetPosition();
        }

        public void MigrateWindows()
        {
            if (windowLayoutVersion >= 1) return;
            hudSize.x = layoutSettings.stripWidth + 20;
            lockPosition = false;
            windowLayoutVersion = 1;
        }

        public void ClampLayout()
        {
            layoutSettings.fontSize = Mathf.Clamp(layoutSettings.fontSize, 10, 28);
            layoutSettings.columns = Mathf.Clamp(layoutSettings.columns, 1, 4);
            layoutSettings.stripWidth = Mathf.Clamp(layoutSettings.stripWidth, 400, 1800);
            layoutSettings.nearDistance = Mathf.Clamp(layoutSettings.nearDistance, 250, 10000);
            layoutSettings.inspectorPageSize = Mathf.Clamp(layoutSettings.inspectorPageSize, 10, 100);
            layoutSettings.profileHeight = Mathf.Clamp(layoutSettings.profileHeight, 20, 300);
            if (!Enum.IsDefined(typeof(LayoutSettings.DisplayMode), layoutSettings.displayMode)) layoutSettings.displayMode = LayoutSettings.DisplayMode.DrivingStrip;
            if (!Enum.IsDefined(typeof(LayoutSettings.RouteDisplay), layoutSettings.routeDisplay)) layoutSettings.routeDisplay = LayoutSettings.RouteDisplay.Graph;
            layoutSettings.backgroundOpacity = Mathf.Clamp01(layoutSettings.backgroundOpacity);
            layoutSettings.contentOpacity = Mathf.Clamp01(layoutSettings.contentOpacity);
            layoutSettings.graphBackgroundOpacity = Mathf.Clamp01(layoutSettings.graphBackgroundOpacity);
            layoutSettings.graphContentOpacity = Mathf.Clamp01(layoutSettings.graphContentOpacity);
            trackInfoSettings.maxEventCount = Mathf.Clamp(trackInfoSettings.maxEventCount, 1, 30);
            trackInfoSettings.maxEventSpan = double.IsNaN(trackInfoSettings.maxEventSpan) || double.IsInfinity(trackInfoSettings.maxEventSpan)
                ? 50000 : Math.Min(50000, Math.Max(100, trackInfoSettings.maxEventSpan));
            trainInfoSettings.updatePeriod = Mathf.Max(0.05f, trainInfoSettings.updatePeriod);
        }

        /// <summary>Apply once after providers are registered, so new installs start as a compact driving HUD.</summary>
        public void ApplyDrivingPreset()
        {
            foreach (var provider in Registry.providers.Values)
                drivingInfoSettings.EnsureProviderSettings(provider);
            if (!drivingDefaultsApplied)
            {
                // Establish the initial reading order once; later preset buttons retain the user's order.
                var priority = DrivingInfoSettings.DrivingLabels;
                drivingInfoSettings.providerSettings = priority
                    .Select(label => drivingInfoSettings.providerSettings.Find(p => p.providerLabel == label))
                    .Where(p => p != null)
                    .Concat(drivingInfoSettings.providerSettings.Where(p => !priority.Contains(p.providerLabel)))
                    .ToList();
            }
            drivingInfoSettings.SelectProviders(DrivingInfoSettings.DrivingLabels);
            drivingInfoSettings.enabled = true;
            trackInfoSettings.enabled = true;
            trackInfoSettings.maxEventSpan = 50000;
            trainInfoSettings.enabled = true;
            trainInfoSettings.showTrainInfo = true;
            trainInfoSettings.showCarList = false;
            lockPosition = false;
            drivingDefaultsApplied = true;
        }
    }
}
