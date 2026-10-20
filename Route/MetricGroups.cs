using System;
using System.Collections.Generic;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>
    /// What a metric is about, in the order a driver reads them: what the train is doing, what is
    /// being commanded, the brakes, what is coming out of the engine, the engine itself, and what
    /// it is consuming. Native simulation ports keep to themselves at the end.
    /// </summary>
    public enum MetricGroup { Train, Controls, Brakes, Power, Engine, Supplies, NativePort }

    /// <summary>
    /// Classifies a metric by its label. Pure, so the grouping can be tested without the game.
    /// Named metrics are matched exactly; anything unrecognised falls back to a keyword rule and
    /// finally to Train, so a metric added by the game or another mod still lands somewhere.
    /// </summary>
    public static class MetricGroups
    {
        public const int Count = 7;

        private static readonly HashSet<string> Train = Set(
            "Speed", "Acceleration", "Grade", "Grade resistance", "Altitude", "Vehicle mass",
            "Kinetic energy", "Direction");

        private static readonly HashSet<string> Controls = Set(
            "Throttle", "Throttle notch", "Regulator", "Reverser", "Cutoff", "Gear", "Gear ratio",
            "Gear change", "Sanders", "Horn / whistle", "Dynamo", "Air pump", "Firedoor", "Damper",
            "Blower", "Injector", "Cylinder cocks", "Blowdown", "Coal dump", "Lubricator");

        // Brake controls and brake pressures read together, so they share one group.
        private static readonly HashSet<string> Brakes = Set(
            "Train brake", "Train brake notch", "Independent brake", "Independent brake notch",
            "Dynamic brake", "Dynamic brake notch", "Handbrake", "Brake cutout", "Brake pipe",
            "Brake cylinder", "Main reservoir", "Aux reservoir", "Brake application",
            "Cylinder application", "Brake force", "Brake temperature", "Brake overheating",
            "Retarder effect");

        private static readonly HashSet<string> Power = Set(
            "Power", "Tractive effort", "Engine load torque", "Adhesion", "Adhesion demand", "Slip");

        private static readonly HashSet<string> Engine = Set(
            "Engine RPM", "Engine RPM limit", "Engine temperature", "Engine health", "Engine running",
            "Fuel consumption demand", "Boiler pressure", "Boiler temperature", "Boiler water level",
            "Boiler water mass", "Firebox coal level", "Firebox combustion", "Firebox heat output",
            "Firebox temperature", "Crown sheet heat", "Cylinder temperature", "Water in cylinders",
            "Safety valve", "Injector flow", "Blowdown flow", "Steam chest pressure",
            "Steam engine RPM", "Steam flow", "Steam flow demand");

        private static readonly HashSet<string> Supplies = Set(
            "Fuel level", "Coal reserve", "Water tank level", "Sand level", "Oil level", "Battery charge");

        public static MetricGroup Of(string? label)
        {
            var text = (label ?? "").Trim();
            if (text.Length == 0) return MetricGroup.Train;
            if (text.StartsWith("Sim: ", StringComparison.Ordinal)) return MetricGroup.NativePort;
            // Exact names first: "Cylinder application" is a brake reading despite its wording.
            if (Brakes.Contains(text)) return MetricGroup.Brakes;
            if (Controls.Contains(text)) return MetricGroup.Controls;
            if (Power.Contains(text)) return MetricGroup.Power;
            if (Engine.Contains(text)) return MetricGroup.Engine;
            if (Supplies.Contains(text)) return MetricGroup.Supplies;
            if (Train.Contains(text)) return MetricGroup.Train;
            // An unrecognised metric still groups by what its name is plainly about.
            if (Mentions(text, "brake")) return MetricGroup.Brakes;
            if (Mentions(text, "boiler") || Mentions(text, "firebox") || Mentions(text, "engine")
                || Mentions(text, "steam") || Mentions(text, "cylinder")) return MetricGroup.Engine;
            if (Mentions(text, "level") || Mentions(text, "reserve") || Mentions(text, "charge"))
                return MetricGroup.Supplies;
            if (Mentions(text, "notch") || Mentions(text, "valve")) return MetricGroup.Controls;
            return MetricGroup.Train;
        }

        private static bool Mentions(string text, string word) =>
            text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;

        private static HashSet<string> Set(params string[] labels) =>
            new HashSet<string>(labels, StringComparer.OrdinalIgnoreCase);
    }
}
