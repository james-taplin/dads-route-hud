// Boundary doubles only. Production NativeTelemetry, Registry and pull-provider code are linked unchanged.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using QuantitiesNet;
using LocoSim.Implementations;
using DV.Simulation.Controllers;

namespace UnityEngine { public static class Time { public static float time; } }
namespace DV.Simulation.Cars
{
    public sealed class SimController
    {
        public SimulationFlow? simFlow;
        public BaseControlsOverrider? controlsOverrider = new BaseControlsOverrider();
    }
}
namespace DV.Simulation.Controllers
{
    public sealed class BaseControlsOverrider { public OverridableBaseControl? Selected; }
    public sealed class OverridableBaseControl { public float Value; }
}
namespace LocoSim.Implementations
{
    public class SimComponent { }
    public sealed class Port { public string id = "", valueType = "RAW"; public float Value; }
    public sealed class PortReference
    {
        public bool IsConnected;
        public float Value;
    }
    public sealed class SimulationFlow
    {
        public IEnumerable<Port> AllPorts = Array.Empty<Port>();
        public IEnumerable<SimComponent> OrderedSimComps = Array.Empty<SimComponent>();
    }
}
namespace LocoSim.Resources
{
    public enum ResourceContainerType { Fuel, Water, Coal }
    public sealed class ResourceContainer : SimComponent
    {
        public ResourceContainerType resourceType;
        public Port normalizedReadOutPort = new Port();
    }
}
public sealed class TrainCar
{
    public string ID = "Test car";
    public DV.Simulation.Cars.SimController? SimController = new DV.Simulation.Cars.SimController();
    public float Speed;
    public int SpeedReads;
    public float GetForwardSpeed() { SpeedReads++; return Speed; }
}
public sealed class CountingSequence<T> : IEnumerable<T>
{
    private readonly T[] items;
    public int Enumerations;
    public CountingSequence(params T[] items) { this.items = items; }
    public IEnumerator<T> GetEnumerator() { Enumerations++; return ((IEnumerable<T>)items).GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
public class ExampleComponent : SimComponent { public float Value; }
public sealed class CustomComponent : ExampleComponent { }
public sealed class MissingComponent : SimComponent { }

namespace DvMod.HeadsUpDisplay
{
    public static class Main
    {
        public static Settings settings = new Settings();
        public static Mod? mod = new Mod();
        public static int Registrations;
        public static void DebugLog(string message) { Registrations++; }
    }
    public sealed class Mod { public Logger Logger = new Logger(); }
    public sealed class Logger { public void Warning(string message) => throw new Exception(message); }
    public sealed class Settings
    {
        public DrivingInfoSettings drivingInfoSettings = new DrivingInfoSettings();
        public sealed class DrivingInfoSettings
        {
            public readonly Dictionary<string, ProviderSettings> providerSettings = new Dictionary<string, ProviderSettings>();
            public readonly HashSet<string> disabledProviders = new HashSet<string>();
            public bool HasProviderSettings(string label) => providerSettings.ContainsKey(label);
            public void EnsureProviderSettings(IDataProvider provider) => GetProviderSettings(provider);
            public ProviderSettings GetProviderSettings(IDataProvider provider)
            {
                if (!providerSettings.TryGetValue(provider.Label, out var settings))
                    providerSettings.Add(provider.Label, settings = new ProviderSettings());
                return settings;
            }
            public sealed class ProviderSettings
            {
                public int precision;
                public bool TryGetUnit(Dimension dimension, out Unit unit) { unit = Units.Bar; return true; }
            }
        }
    }
    // RegisterPush and the top-level catalogue initializer are outside these pull-telemetry tests.
    public sealed class FloatPushProvider : DataProvider<float>
    {
        public FloatPushProvider(string label, Func<float, string> formatter, IComparable? order = null) : base(label, order) { }
        public void SetValue(TrainCar car, float value) { }
        public override bool TryGetValue(TrainCar car, out float value) { value = 0; return false; }
        public override bool TryGetFormatted(TrainCar car, out string value) { value = ""; return false; }
    }
    public sealed class QuantityPushProvider<D> : DataProvider<Quantity<D>> where D : IDimension, new()
    {
        public QuantityPushProvider(string label, IComparable? order = null, bool hidden = false) : base(label, order, hidden) { }
        public void SetValue(TrainCar car, Quantity<D> value) { }
        public override bool TryGetValue(TrainCar car, out Quantity<D> value) { value = default!; return false; }
        public override bool TryGetFormatted(TrainCar car, out string value) { value = ""; return false; }
    }
    public static class GeneralProviders { public static void Register() { } }
    public static class LocoProviders { public static void Register() { } }
    public static class ControlProviders { public static void Register() { } }
}
