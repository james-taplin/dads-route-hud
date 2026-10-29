using System;
using System.Collections.Generic;
using DV.Simulation.Cars;
using DV.Simulation.Controllers;
using LocoSim.Implementations;
using LocoSim.Resources;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>Read-only native simulation cache for the currently displayed car.</summary>
    public static class NativeTelemetry
    {
        private static TrainCar? currentCar;
        private static SimulationFlow? currentFlow;
        private static readonly Dictionary<string, Port> ports = new Dictionary<string, Port>();
        private static readonly Dictionary<Type, SimComponent?> components = new Dictionary<Type, SimComponent?>();
        private static readonly Dictionary<ResourceContainerType, ResourceContainer> resources = new Dictionary<ResourceContainerType, ResourceContainer>();
        private static float previousTime;
        private static float previousSpeed;
        private static float? acceleration;

        // Reuse the existing native speed sample for maps and event-list colours,
        // including event-list-only mode. Presentation never reads game state itself.
        public static double? SpeedKph => currentCar != null && !float.IsNaN(previousSpeed) && !float.IsInfinity(previousSpeed)
            ? Math.Abs((double)previousSpeed) * 3.6 : (double?)null;

        public static void Reset()
        {
            currentCar = null;
            currentFlow = null;
            ports.Clear();
            components.Clear();
            resources.Clear();
            acceleration = null;
            previousTime = 0;
            previousSpeed = 0;
        }

        // Call from Update, never OnGUI: Unity may render several GUI passes per frame.
        public static void Update(TrainCar? car)
        {
            if (car == null)
            {
                if (currentCar != null || currentFlow != null) Reset();
                return;
            }

            var flow = car.SimController != null ? car.SimController.simFlow : null;
            if (car != currentCar || !ReferenceEquals(flow, currentFlow))
            {
                Reset();
                currentCar = car;
                currentFlow = flow;
                previousTime = Time.time;
                previousSpeed = car.GetForwardSpeed(); // Native game API is metres per second.
                if (flow != null)
                {
                    foreach (var port in flow.AllPorts)
                    {
                        ports[port.id] = port;
                        RegisterRawPort(port);
                    }
                    foreach (var component in flow.OrderedSimComps)
                    {
                        var type = component.GetType();
                        if (!components.ContainsKey(type)) components.Add(type, component);
                        if (component is ResourceContainer container && !resources.ContainsKey(container.resourceType))
                            resources.Add(container.resourceType, container);
                    }
                }
                return;
            }

            var time = Time.time;
            var elapsed = time - previousTime;
            if (elapsed < 0 || elapsed > 1f)
            {
                previousTime = time;
                previousSpeed = car.GetForwardSpeed();
                acceleration = null;
            }
            else if (elapsed >= 0.1f)
            {
                var speed = car.GetForwardSpeed();
                acceleration = Finite((speed - previousSpeed) / elapsed);
                previousTime = time;
                previousSpeed = speed;
            }
        }

        public static float? Acceleration(TrainCar car) => car == currentCar ? acceleration : null;

        public static float? PortValue(TrainCar car, string id)
        {
            // SimulationFlow.TryGetPort logs missing optional ports; the local lookup does not.
            return car == currentCar && ports.TryGetValue(id, out var port) ? Finite(port.Value) : null;
        }

        public static bool TryGetPort(TrainCar car, string id, out float value)
        {
            var result = PortValue(car, id);
            value = result ?? 0;
            return result.HasValue;
        }

        public static T? Component<T>(TrainCar car) where T : SimComponent
        {
            if (car != currentCar || currentFlow == null) return null;
            if (components.TryGetValue(typeof(T), out var component)) return (T?)component;
            // Native simulation subclasses supplied by custom-car mods preserve this API.
            // Resolve once per flow, then retain the same lifetime as the native cache.
            foreach (var candidate in currentFlow.OrderedSimComps)
                if (candidate is T match)
                {
                    components[typeof(T)] = match;
                    return match;
                }
            components[typeof(T)] = null;
            return null;
        }

        public static float? ComponentValue<T>(TrainCar car, Func<T, float?> read) where T : SimComponent
        {
            var component = Component<T>(car);
            return component == null ? null : Finite(read(component));
        }

        public static float? ControlValue(TrainCar car, Func<BaseControlsOverrider, OverridableBaseControl?> select)
        {
            if (car != currentCar || currentFlow == null) return null;
            var controls = car.SimController?.controlsOverrider;
            var control = controls == null ? null : select(controls);
            return control == null ? null : Finite(control.Value);
        }

        public static float? ReferenceValue(PortReference port) => port.IsConnected ? Finite(port.Value) : null;
        public static float? ResourceLevel(TrainCar car, ResourceContainerType resource)
            => car == currentCar && resources.TryGetValue(resource, out var container) ? Finite(container.normalizedReadOutPort.Value) : null;
        public static float? Finite(float? value) => !value.HasValue || float.IsNaN(value.Value) || float.IsInfinity(value.Value) ? null : value;

        private static void RegisterRawPort(Port port)
        {
            var id = port.id;
            var label = $"Sim: {id} [{port.valueType}]";
            if (Registry.providers.ContainsKey(label)) return;
            Registry.Register(new FloatQueryDataProvider(label, car => PortValue(car, id), value => value.ToString("G6")));
            // Raw values retain their native representation. PortValueType alone does not
            // specify physical units: WATER and TEMPERATURE also describe normalized ports.
        }
    }
}
