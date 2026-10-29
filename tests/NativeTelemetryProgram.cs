using System;
using System.Globalization;
using DvMod.HeadsUpDisplay;
using DV.Simulation.Controllers;
using LocoSim.Implementations;
using LocoSim.Resources;
using UnityEngine;
using HudMain = DvMod.HeadsUpDisplay.Main;

internal static class NativeTelemetryProgram
{
    private static int assertions;
    private static void Check(bool value, string message)
    { assertions++; if (!value) throw new Exception(message); }
    private static bool Near(float? value, float expected) => value.HasValue && Math.Abs(value.Value - expected) < 0.01f;

    public static int Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var port = new Port { id = "throttle", Value = 0.25f };
        var shadowed = new Port { id = "duplicate", Value = 1 };
        var final = new Port { id = "duplicate", Value = 2 };
        var fuel = new ResourceContainer { resourceType = ResourceContainerType.Fuel };
        fuel.normalizedReadOutPort.Value = 0.4f;
        var secondFuel = new ResourceContainer { resourceType = ResourceContainerType.Fuel };
        secondFuel.normalizedReadOutPort.Value = 0.9f;
        var custom = new CustomComponent { Value = 8 };
        var simComponents = new CountingSequence<SimComponent>(custom, fuel, secondFuel);
        var simPorts = new CountingSequence<Port>(port, shadowed, final);
        var flow = new SimulationFlow { AllPorts = simPorts, OrderedSimComps = simComponents };
        var car = new TrainCar { Speed = 10 };
        car.SimController!.simFlow = flow;
        Time.time = 100;
        NativeTelemetry.Update(car);
        Check(NativeTelemetry.SpeedKph == 36, "Shared HUD speed converts the existing native sample to km/h");
        for (var i = 0; i < 100; i++) { var ignored = NativeTelemetry.SpeedKph; }
        Check(car.SpeedReads == 1, "Map and list colour reads do not perform additional native speed sampling");
        Check(NativeTelemetry.Acceleration(car) == null, "First sample has no fabricated acceleration");
        Check(Near(NativeTelemetry.PortValue(car, "throttle"), 0.25f), "Port values use the current native port");
        Check(Near(NativeTelemetry.PortValue(car, "duplicate"), 2), "Duplicate port IDs retain the final indexed port");
        Check(NativeTelemetry.PortValue(car, "missing") == null, "Missing ports are unavailable");
        Check(!NativeTelemetry.TryGetPort(car, "missing", out var absent) && absent == 0, "TryGetPort communicates absence explicitly");
        Check(NativeTelemetry.TryGetPort(car, "duplicate", out var present) && present == 2, "TryGetPort returns observed values");
        Check(HudMain.Registrations == 2 && Registry.providers.Count == 2, "Raw duplicate IDs register only once");
        Check(HudMain.settings.drivingInfoSettings.disabledProviders.Contains("Sim: throttle [RAW]"), "New raw diagnostics remain disabled by default through the production registry");
        var raw = Registry.GetProvider("Sim: throttle [RAW]")!;
        Check(raw.TryGetFormatted(car, out var formatted) && formatted == "0.25", "Registered raw providers read and format the telemetry value");
        var other = new TrainCar();
        Check(NativeTelemetry.PortValue(other, "throttle") == null && NativeTelemetry.Acceleration(other) == null, "A different car cannot read the selected cache");
        port.Value = float.NaN;
        Check(NativeTelemetry.PortValue(car, "throttle") == null && !raw.TryGetFormatted(car, out _), "Nonfinite native ports do not reach the HUD");
        port.Value = float.PositiveInfinity;
        Check(NativeTelemetry.PortValue(car, "throttle") == null, "Infinite ports are unavailable");
        port.Value = 0.75f;
        Check(Near(NativeTelemetry.PortValue(car, "throttle"), 0.75f), "Cached port identities read live values");

        var initialEnumerations = simComponents.Enumerations;
        Check(ReferenceEquals(NativeTelemetry.Component<CustomComponent>(car), custom)
            && simComponents.Enumerations == initialEnumerations, "Exact component lookups use the index");
        Check(ReferenceEquals(NativeTelemetry.Component<ExampleComponent>(car), custom)
            && simComponents.Enumerations == initialEnumerations + 1, "Native subclass fallback resolves the base component");
        NativeTelemetry.Component<ExampleComponent>(car);
        Check(simComponents.Enumerations == initialEnumerations + 1, "Subclass fallback is cached per flow");
        Check(NativeTelemetry.Component<MissingComponent>(car) == null, "Missing components are unavailable");
        var afterMiss = simComponents.Enumerations;
        NativeTelemetry.Component<MissingComponent>(car);
        Check(simComponents.Enumerations == afterMiss, "Negative component lookups do not rescan every read");
        Check(Near(NativeTelemetry.ComponentValue<ExampleComponent>(car, component => component.Value), 8), "Component readers receive the native subclass");
        Check(NativeTelemetry.ComponentValue<ExampleComponent>(car, _ => float.NaN) == null
            && NativeTelemetry.ComponentValue<ExampleComponent>(car, _ => null) == null, "Component readers reject missing and nonfinite values");
        Check(NativeTelemetry.Component<ExampleComponent>(other) == null, "Components cannot leak to another car");
        Check(Near(NativeTelemetry.ResourceLevel(car, ResourceContainerType.Fuel), 0.4f), "Resource indexing preserves the first container for its native type");
        Check(NativeTelemetry.ResourceLevel(car, ResourceContainerType.Water) == null
            && NativeTelemetry.ResourceLevel(other, ResourceContainerType.Fuel) == null, "Missing and other-car resources remain unavailable");
        fuel.normalizedReadOutPort.Value = float.NegativeInfinity;
        Check(NativeTelemetry.ResourceLevel(car, ResourceContainerType.Fuel) == null, "Nonfinite resource readings are unavailable");

        var lever = new OverridableBaseControl { Value = 0.6f };
        car.SimController.controlsOverrider!.Selected = lever;
        Check(Near(NativeTelemetry.ControlValue(car, controls => controls.Selected), 0.6f), "Controls return their observed native value");
        lever.Value = float.NaN;
        Check(NativeTelemetry.ControlValue(car, controls => controls.Selected) == null, "Nonfinite controls are unavailable");
        car.SimController.controlsOverrider = null;
        Check(NativeTelemetry.ControlValue(car, controls => controls.Selected) == null, "Missing overriders are safe");
        var reference = new PortReference { Value = 3 };
        Check(NativeTelemetry.ReferenceValue(reference) == null, "Disconnected port references do not report raw defaults");
        reference.IsConnected = true;
        Check(Near(NativeTelemetry.ReferenceValue(reference), 3), "Connected port references report their value");
        reference.Value = float.PositiveInfinity;
        Check(NativeTelemetry.ReferenceValue(reference) == null, "References also reject nonfinite values");

        Time.time = 100.05f; car.Speed = 11;
        NativeTelemetry.Update(car);
        Check(car.SpeedReads == 1 && NativeTelemetry.Acceleration(car) == null, "Acceleration waits for at least 0.1 seconds without resampling speed");
        Time.time = 100.2f; car.Speed = 12;
        NativeTelemetry.Update(car);
        Check(Near(NativeTelemetry.Acceleration(car), 10), "Acceleration uses metres-per-second delta over sample seconds");
        Check(simPorts.Enumerations == 1 && HudMain.Registrations == 2, "Stable Updates neither rediscover ports nor reregister diagnostics");
        Time.time = 102; car.Speed = 50;
        NativeTelemetry.Update(car);
        Check(NativeTelemetry.Acceleration(car) == null, "A long sampling gap resets acceleration");
        Time.time = 101;
        NativeTelemetry.Update(car);
        Check(NativeTelemetry.Acceleration(car) == null, "Time rewind resets acceleration");
        Time.time = 101.2f; car.Speed = 52;
        NativeTelemetry.Update(car);
        Check(Near(NativeTelemetry.Acceleration(car), 10), "Acceleration resumes from the rewind baseline");

        var newlyAvailable = new MissingComponent();
        var replacement = new SimulationFlow { OrderedSimComps = new[] { newlyAvailable } };
        car.SimController.simFlow = replacement;
        NativeTelemetry.Update(car);
        Check(NativeTelemetry.PortValue(car, "throttle") == null && NativeTelemetry.Component<ExampleComponent>(car) == null
            && NativeTelemetry.ResourceLevel(car, ResourceContainerType.Fuel) == null, "A replacement flow invalidates ports, components and resources");
        Check(NativeTelemetry.Acceleration(car) == null, "A replacement flow also resets acceleration");
        Check(ReferenceEquals(NativeTelemetry.Component<MissingComponent>(car), newlyAvailable), "A replacement flow invalidates prior negative component lookups");
        car.SimController.simFlow = flow;
        HudMain.settings.drivingInfoSettings.disabledProviders.Remove("Sim: throttle [RAW]");
        NativeTelemetry.Update(car);
        Check(HudMain.Registrations == 2 && !HudMain.settings.drivingInfoSettings.disabledProviders.Contains("Sim: throttle [RAW]"), "Returning to a known flow preserves the selected raw provider and registers it once");
        other.SimController!.simFlow = flow;
        NativeTelemetry.Update(other);
        Check(NativeTelemetry.PortValue(car, "throttle") == null && Near(NativeTelemetry.PortValue(other, "throttle"), 0.75f)
            && NativeTelemetry.Acceleration(other) == null, "Changing car resets ownership and acceleration even with a shared flow");
        const string savedRawLabel = "Sim: saved [RAW]";
        HudMain.settings.drivingInfoSettings.providerSettings.Add(savedRawLabel, new Settings.DrivingInfoSettings.ProviderSettings());
        other.SimController.simFlow = new SimulationFlow { AllPorts = new[] { new Port { id = "saved", Value = 4 } } };
        NativeTelemetry.Update(other);
        Check(Registry.providers.ContainsKey(savedRawLabel) && !HudMain.settings.drivingInfoSettings.disabledProviders.Contains(savedRawLabel), "A raw provider with saved enabled settings keeps its selection on first discovery");
        NativeTelemetry.Update(null);
        Check(NativeTelemetry.PortValue(other, "throttle") == null && NativeTelemetry.Component<ExampleComponent>(other) == null, "No selected car clears retained simulation references");
        NativeTelemetry.Update(new TrainCar { SimController = null });
        Check(NativeTelemetry.ControlValue(other, controls => controls.Selected) == null, "An uninitialized car is safe");
        Check(NativeTelemetry.Finite(null) == null && NativeTelemetry.Finite(float.NegativeInfinity) == null && NativeTelemetry.Finite(0) == 0, "Finite filtering preserves legitimate zero and rejects missing/nonfinite values");
        NativeTelemetry.Update(new TrainCar { Speed = -10, SimController = null });
        Check(NativeTelemetry.SpeedKph == 36, "Reverse motion uses absolute speed for list overspeed colours without requiring a graph");
        NativeTelemetry.Update(new TrainCar { Speed = float.NaN, SimController = null });
        Check(NativeTelemetry.SpeedKph == null, "Nonfinite speed cannot create a comparison colour");
        NativeTelemetry.Update(null);
        Check(NativeTelemetry.SpeedKph == null, "No car clears the shared speed comparison");
        NativeTelemetry.Update(new TrainCar { Speed = 0, SimController = null });
        Check(NativeTelemetry.SpeedKph == 0, "A stationary car remains a known zero for green speed comparisons");
        Console.WriteLine($"{assertions} production NativeTelemetry/Registry regression checks passed.");
        return 0;
    }
}
