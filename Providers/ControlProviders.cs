using System;
using DV.Simulation.Cars;
using DV.Simulation.Controllers;
using LocoSim.Implementations;
using LocoSim.Resources;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    internal static class ControlProviders
    {
        private static readonly Func<float, string> Percent = value => $"{value * 100:F0}%";

        public static void Register()
        {
            Control("Throttle", c => c.Throttle);
            Control("Train brake", c => c.Brake);
            Control("Independent brake", c => c.IndependentBrake);
            Control("Dynamic brake", c => c.DynamicBrake);
            Control("Sanders", c => c.Sander);
            Control("Horn / whistle", c => c.Horn);
            Control("Brake cutout", c => c.BrakeCutout);
            Control("Dynamo", c => c.Dynamo);
            Control("Air pump", c => c.AirPump);
            Register("Reverser", car => NativeTelemetry.ControlValue(car, c => c.Reverser), DrivingMath.Reverser);
            Register("Handbrake", car =>
            {
                var control = car.SimController?.controlsOverrider?.Handbrake;
                if (control != null) return control.Value; // Includes native virtual/tender handbrakes.
                return car.brakeSystem != null && car.brakeSystem.hasHandbrake ? car.brakeSystem.handbrakePosition : (float?)null;
            }, DrivingMath.Percent);
            Notch("Throttle notch", c => c.Throttle);
            Notch("Train brake notch", c => c.Brake);
            Notch("Independent brake notch", c => c.IndependentBrake);
            Notch("Dynamic brake notch", c => c.DynamicBrake);

            // These external control IDs also cover custom cars that expose the supported
            // native steam controls without a BaseControlsOverrider property for them.
            PortControl("Cylinder cocks", "cylinderCock.EXT_IN");
            PortControl("Injector", "injector.EXT_IN");
            PortControl("Firedoor", "fireboxDoor.EXT_IN");
            PortControl("Blower", "blower.EXT_IN");
            PortControl("Damper", "damper.EXT_IN");
            PortControl("Blowdown", "blowdown.EXT_IN");
            PortControl("Coal dump", "coalDumpControl.EXT_IN");
            PortControl("Lubricator", "lubricatorControl.EXT_IN");

            Steam("Regulator", s => NativeTelemetry.ReferenceValue(s.throttleControl), DrivingMath.Percent);
            Steam("Cutoff", s => s.reverserControl.IsConnected ? Mathf.Lerp(s.minCutoff, s.maxCutoff, Mathf.Abs(s.reverserControl.Value)) : (float?)null, Percent);
            Steam("Steam chest pressure", s => s.steamChestPressureReadOut.Value, BarGauge);
            Steam("Steam flow", s => s.intakeFlowReadOut.Value, value => $"{value:F2} kg/s");
            Steam("Steam flow demand", s => s.intakeFlowNormalizedReadOut.Value, Percent);
            Steam("Cylinder temperature", s => s.cylinderTemperatureReadOut.Value, Celsius);
            Steam("Water in cylinders", s => s.waterInCylindersNormalizedReadOut.Value, Percent);
            Steam("Steam engine RPM", s => NativeTelemetry.ReferenceValue(s.crankRpm), value => $"{value:F0} rpm");
            Boiler("Boiler pressure", b => b.pressureReadOut.Value, BarGauge);
            Boiler("Boiler temperature", b => b.temperatureReadOut.Value, Celsius);
            Boiler("Boiler water level", b => b.waterLevelReadOut.Value, Percent);
            Boiler("Boiler water mass", b => b.waterMassReadOut.Value, value => $"{value:F0} kg");
            Boiler("Injector flow", b => b.injectorFlowNormalizedReadOut.Value, Percent);
            Boiler("Blowdown flow", b => b.blowdownFlowNormalizedReadOut.Value, Percent);
            Boiler("Safety valve", b => b.safetyValveReadOut.Value, Percent);
            Boiler("Crown sheet heat", b => b.normalizedCrownSheetTemperatureReadOut.Value, Percent);

            Diesel("Engine RPM", e => e.engineRpm.Value, value => $"{value:F0} rpm", e => e.engineRpmReadOut.Value, e => e.engineRpmReadOut.Value);
            Diesel("Engine RPM limit", e => e.engineRpmMaxReadOut.Value, value => $"{value:F0} rpm", e => e.maxRpmReadOut.Value, e => e.engineRpmMaxReadOut.Value);
            Diesel("Engine load torque", e => NativeTelemetry.ReferenceValue(e.loadTorque), value => $"{value / 1000:F1} kNm");
            Diesel("Engine temperature", e => NativeTelemetry.ReferenceValue(e.temperature), Celsius, e => NativeTelemetry.ReferenceValue(e.temperature), e => NativeTelemetry.ReferenceValue(e.temperature));
            Diesel("Engine running", e => e.engineOnReadOut.Value, value => value > 0.5f ? "On" : "Off", e => e.engineOnReadOut.Value, e => e.engineOnReadOut.Value);
            Diesel("Engine health", e => e.engineHealthStateExtIn.Value, Percent, e => e.engineHealthStateExtIn.Value, e => e.engineHealthStateExtIn.Value);
            Diesel("Retarder effect", e => e.retarderNormalizedBrakeEffectReadOut.Value, Percent, null, e => e.retarderNormalizedBrakeEffectReadOut.Value);
            Diesel("Fuel consumption demand", e => e.fuelConsumptionNormalizedReadOut.Value, Percent, e => e.fuelConsumptionNormalizedReadOut.Value, e => e.fuelConsumptionNormalizedReadOut.Value);
            Register("Fuel level", car => NativeTelemetry.ResourceLevel(car, ResourceContainerType.FUEL), Percent);
            Register("Oil level", car => NativeTelemetry.ResourceLevel(car, ResourceContainerType.OIL), Percent);
            Register("Sand level", car => NativeTelemetry.ResourceLevel(car, ResourceContainerType.SAND), Percent);
            Register("Water tank level", car => NativeTelemetry.ResourceLevel(car, ResourceContainerType.WATER), Percent);
            Register("Coal reserve", car => NativeTelemetry.ResourceLevel(car, ResourceContainerType.COAL), Percent);
            Register("Battery charge", car => NativeTelemetry.ResourceLevel(car, ResourceContainerType.ELECTRIC_CHARGE), Percent);

            Component<Firebox>("Firebox temperature", f => f.temperatureReadOut.Value, Celsius);
            Component<Firebox>("Firebox coal level", f => f.coalLevelNormalizedReadOut.Value, Percent);
            Component<Firebox>("Firebox combustion", f => f.combustionRateNormalizedReadOut.Value, Percent);
            Component<Firebox>("Firebox heat output", f => f.heatReadOut.Value / 1000, value => $"{value:F0} kW");
            Component<ManualTransmissionInput>("Gear", t => t.gearReadOut.Value, value => value.ToString("F0"));
            Component<SmoothTransmission>("Gear ratio", t => t.gearRatioReadOut.Value, value => value.ToString("F2"));
            Component<SmoothTransmission>("Gear change", t => t.gearChangeInProgressReadOut.Value, Percent);
        }

        private static string BarGauge(float value) => $"{DrivingMath.GaugePressure(value):F1} bar(g)";
        private static string Celsius(float value) => $"{value:F0} °C";

        private static void Register(string label, Func<TrainCar, float?> read, Func<float, string> format)
            => Registry.Register(new FloatQueryDataProvider(label, car => NativeTelemetry.Finite(read(car)), format));

        private static void Control(string label, Func<BaseControlsOverrider, OverridableBaseControl?> select)
            => Register(label, car => NativeTelemetry.ControlValue(car, select), DrivingMath.Percent);

        private static void Notch(string label, Func<BaseControlsOverrider, OverridableBaseControl?> select)
        {
            Register(label, car =>
            {
                if (car.SimController?.simFlow == null) return null;
                var controls = car.SimController.controlsOverrider;
                var control = controls == null ? null : select(controls);
                return control != null && control.IsNotched ? Mathf.Round(control.Value * control.NotchCount) : (float?)null;
            }, value => value.ToString("F0"));
        }

        private static void PortControl(string label, string id) => Register(label, car => NativeTelemetry.PortValue(car, id), DrivingMath.Percent);
        private static void Component<T>(string label, Func<T, float?> read, Func<float, string> format) where T : SimComponent
            => Register(label, car => NativeTelemetry.ComponentValue(car, read), format);
        private static void Steam(string label, Func<ReciprocatingSteamEngine, float?> read, Func<float, string> format) => Component(label, read, format);
        private static void Boiler(string label, Func<Boiler, float?> read, Func<float, string> format) => Component(label, read, format);
        private static void Diesel(string label, Func<DieselEngineDirect, float?> read, Func<float, string> format,
            Func<DieselEnginePowerSource, float?>? readPowerSource = null, Func<DieselEngineDirectDrive, float?>? readDirectDrive = null)
        {
            Register(label, car => NativeTelemetry.ComponentValue(car, read)
                ?? (readPowerSource == null ? null : NativeTelemetry.ComponentValue(car, readPowerSource))
                ?? (readDirectDrive == null ? null : NativeTelemetry.ComponentValue(car, readDirectDrive)), format);
        }
    }
}
