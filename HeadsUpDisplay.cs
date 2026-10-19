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

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;
            unloaded = false;
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
        internal static void SavePendingSettings()
        {
            if (!settingsSavePending || mod == null) return;
            settingsSavePending = false;
            try { settings.Save(mod); }
            catch (Exception exception) { mod.Logger.Warning("Could not save window layout: " + exception.Message); }
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            NativeTelemetry.Reset();
            OptionalSignalTelemetry.Reset();
            RouteMonitorService.Reset();
            NativeRouteSignIndex.Prune();
            return true;
        }

        private static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime)
        {
            if (!enabled || unloaded) return;
            SavePendingSettings();
            var car = PlayerManager.Car ?? PlayerManager.LastLoco;
            NativeTelemetry.Update(car);
            if (settings.trackInfoSettings.enabled && AStartGameData.carsAndJobsLoadingFinished && WorldStreamingInit.IsLoaded)
            {
                RouteMonitorService.Update(car);
            }
            else
            {
                OptionalSignalTelemetry.Reset();
                if (RouteMonitorService.IsActive)
                    RouteMonitorService.Reset();
            }
        }

        private static void OnLoadingFinished()
        {
            if (unloaded) return;
            NativeTelemetry.Reset();
            OptionalSignalTelemetry.Reset();
            RouteMonitorService.Reset();
            NativeRouteSignIndex.Prune();
            if (behaviourRoot != null) return;
            behaviourRoot = new GameObject("DadsRouteHUD");
            behaviourRoot.AddComponent<Overlay>();
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            SavePendingSettings();
            unloaded = true;
            WorldStreamingInit.LoadingFinished -= OnLoadingFinished;
            if (behaviourRoot != null) UnityEngine.Object.Destroy(behaviourRoot);
            behaviourRoot = null;
            Overlay.instance = null;
            NativeTelemetry.Reset();
            OptionalSignalTelemetry.Reset();
            RouteMonitorService.Reset();
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
