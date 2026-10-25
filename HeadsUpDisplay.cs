using System;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HeadsUpDisplay
{
    [EnableReloading]
    public static class Main
    {
        public static bool enabled = true;
        public static Settings settings = new Settings();
        public static UnityModManager.ModEntry? mod;
        public static GameObject? behaviourRoot;
        private static bool unloaded;
        private static bool settingsSavePending;
        private static float nextSettingsSave;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;
            unloaded = false;
            settingsSavePending = false;
            nextSettingsSave = 0;
            try { settings = Settings.Load<Settings>(modEntry) ?? new Settings(); }
            catch (Exception exception)
            {
                modEntry.Logger.Warning("Could not load HUD settings; using defaults: " + exception.Message);
                settings = new Settings();
            }

            // This fork has its own identity and settings. Retain choices across releases.
            DataProviders.Register();
            if (!settings.drivingDefaultsApplied) settings.ApplyDrivingPreset();
            settings.MigrateWindows();
            if (settings.MigrateRouteDisplay()) RequestSettingsSave();
            settings.ClampLayout();
            new Harmony(modEntry.Info.Id).PatchAll(typeof(Main).Assembly);
            modEntry.OnGUI = OnGui;
            modEntry.OnSaveGUI = OnSaveGui;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;
            modEntry.OnUpdate = OnUpdate;
            WorldStreamingInit.LoadingFinished += OnLoadingFinished;
            if (AStartGameData.carsAndJobsLoadingFinished && WorldStreamingInit.IsLoaded)
                OnLoadingFinished();
            modEntry.Logger.Log("Dad's Route HUD: separate MIT source fork; retained route data and native telemetry.");
            return true;
        }

        private static void OnGui(UnityModManager.ModEntry modEntry)
        {
            settings.Draw();
        }

        private static void OnSaveGui(UnityModManager.ModEntry modEntry) => settings.Save(modEntry);

        internal static void RequestSettingsSave() => settingsSavePending = true;
        internal static void SavePendingSettings(bool force = false)
        {
            if (!settingsSavePending || mod == null || (!force && Time.realtimeSinceStartup < nextSettingsSave)) return;
            try { settings.Save(mod); settingsSavePending = false; }
            catch (Exception exception)
            {
                nextSettingsSave = Time.realtimeSinceStartup + 5;
                mod.Logger.Warning("Could not save window layout; will retry: " + exception.Message);
            }
        }

        private static void ResetTelemetry()
        {
            HudTimings.Reset();
            HudTimings.Enabled = enabled && !unloaded && settings.enableLogging;
            NativeTelemetry.Reset();
            OptionalSignalTelemetry.Reset();
            RouteMonitorService.Reset();
            NativeRouteTraffic.Reset();
            CarList.Reset();
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            ResetTelemetry();
            NativeRouteSignIndex.Prune();
            return true;
        }

        private static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime)
        {
            if (!enabled || unloaded) return;
            HudTimings.Enabled = settings.enableLogging;
            var timing = HudTimings.Measure(HudTimingStage.HudUpdate);
            try
            {
                SavePendingSettings();
                var car = PlayerManager.Car ?? PlayerManager.LastLoco;
                var worldReady = AStartGameData.carsAndJobsLoadingFinished && WorldStreamingInit.IsLoaded;
                NativeTelemetry.Update(worldReady ? car : null);
                CarList.Update(worldReady ? car : null);
                if (settings.trackInfoSettings.enabled && worldReady)
                {
                    NativeRouteSignIndex.Prune();
                    RouteMonitorService.Update(car);
                    var mapVisible = settings.layoutSettings.popOutGraph ? settings.layoutSettings.showGraphWindow
                        : settings.layoutSettings.showHud && settings.layoutSettings.routeDisplay != Settings.LayoutSettings.RouteDisplay.EventList;
                    mapVisible &= settings.layoutSettings.showNearProfile || settings.layoutSettings.showOverview || settings.layoutSettings.expandedAtlas;
                    if (mapVisible) NativeRouteTraffic.Update(car, RouteMonitorService.Current);
                    else NativeRouteTraffic.Reset();
                }
                else
                {
                    NativeRouteTraffic.Reset();
                    if (RouteMonitorService.IsActive || OptionalSignalTelemetry.IsAvailable
                        || OptionalSignalTelemetry.IsReading || OptionalSignalTelemetry.SampledAt > 0)
                    {
                        OptionalSignalTelemetry.Reset();
                        RouteMonitorService.Reset();
                    }
                }
            }
            finally { timing.Dispose(); }
            var timingReport = HudTimings.TakeReport(Time.realtimeSinceStartup);
            if (timingReport != null) modEntry.Logger.Log(timingReport + "; selected="
                + settings.trackInfoSettings.maxEventSpan + "m retained=" + RouteMonitorService.Current.Events.Count);
        }

        private static void OnLoadingFinished()
        {
            if (unloaded) return;
            ResetTelemetry();
            NativeRouteSignIndex.Prune();
            if (behaviourRoot != null) return;
            behaviourRoot = new GameObject("DadsRouteHUD");
            behaviourRoot.AddComponent<Overlay>();
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            SavePendingSettings(force: true);
            unloaded = true;
            WorldStreamingInit.LoadingFinished -= OnLoadingFinished;
            if (behaviourRoot != null) UnityEngine.Object.Destroy(behaviourRoot);
            behaviourRoot = null;
            Overlay.instance = null;
            ResetTelemetry();
            NativeRouteSignIndex.Reset();
            Registry.providers.Clear();
            new Harmony(modEntry.Info.Id).UnpatchAll(modEntry.Info.Id);
            return true;
        }

        public static void DebugLog(string message)
        {
            if (settings.enableLogging && mod != null) mod.Logger.Log(message);
        }
    }
}
