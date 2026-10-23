// Native/optional boundaries only: production OptionalSignalTelemetry executes reflection.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static bool operator ==(Object? a, Object? b)
        {
            var absentA = a is null || a.Destroyed;
            var absentB = b is null || b.Destroyed;
            return absentA || absentB ? absentA == absentB : ReferenceEquals(a, b);
        }
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public static class Time { public static float realtimeSinceStartup; }
    public struct Color { public float r, g, b; }
    public static class ColorUtility
    {
        public static string ToHtmlStringRGB(Color c) => ((byte)(c.r * 255)).ToString("X2")
            + ((byte)(c.g * 255)).ToString("X2") + ((byte)(c.b * 255)).ToString("X2");
    }
}
namespace DV.Utils
{
    public class SingletonBehaviour<T> : UnityEngine.Object where T : UnityEngine.Object, new()
    {
        private static T? _instance;
        public static int InstanceGetterCalls;
        public static T Instance { get { InstanceGetterCalls++; return _instance ??= new T(); } }
        public static void SetExisting(T? value) => _instance = value;
    }
}
namespace UnityModManagerNet
{
    public static class UnityModManager
    {
        public sealed class ModEntry { public bool Active = true; public Assembly Assembly = typeof(ModEntry).Assembly; }
        public static readonly Dictionary<string, ModEntry> Mods = new Dictionary<string, ModEntry>();
        public static ModEntry? FindMod(string name) => Mods.TryGetValue(name, out var result) ? result : null;
    }
}
public sealed class TrainCar { }
public sealed class RailTrack
{
    public string name = "Test track";
    public bool Occupied, Reserved;
    public object? ReservationToken;
}
namespace DvMod.HeadsUpDisplay
{
    public static class Main
    {
        public static bool enabled = true;
        public static Settings settings = new Settings();
        public static Mod mod = new Mod();
    }
    public sealed class Settings { public TrackSettings trackInfoSettings = new TrackSettings(); public bool enableLogging = true; }
    public sealed class TrackSettings { public bool enabled = true; }
    public sealed class Mod { public Logger Logger = new Logger(); }
    public sealed class Logger
    {
        public readonly List<string> Messages = new List<string>();
        public void Log(string message) => Messages.Add(message);
        public void Warning(string message) => Messages.Add(message);
    }
}
namespace Signals.Game
{
    public sealed class SignalManager : DV.Utils.SingletonBehaviour<SignalManager>
    {
        public static int Constructions;
        public SignalManager() => Constructions++;
        public IEnumerable AllControllers { get; set; } = Array.Empty<object>();
    }
    public sealed class Placement
    {
        public RailTrack? Track { get; set; }
        public double Span { get; set; }
        public double Direction { get; set; } = 1;
    }
    public sealed class Controller
    {
        public string Id { get; set; } = "controller";
        public bool Exists { get; set; } = true;
        public Placement? PlacementInfo { get; set; }
        public IEnumerable? AllSignals { get; set; }
        public double CameraDistanceSqr;
        public double GetCameraDistanceSqr() => CameraDistanceSqr;
    }
    public sealed class Block { public IEnumerable AllTracks { get; set; } = Array.Empty<RailTrack>(); }
    public sealed class Signal
    {
        public string Id { get; set; } = "signal";
        public string Name { get; set; } = "Native signal";
        public Aspect CurrentAspect { get; set; } = new Aspect();
        public Signal? DistantSignal { get; set; }
        public Block? Block { get; set; }
        public bool IsShunting { get; set; }
    }
    public sealed class Aspect
    {
        public string Id { get; set; } = "Green";
        public bool DisallowPassing { get; set; }
        public Definition GetDefinition() => new Definition();
    }
    public sealed class Definition { public IEnumerable OnLights { get; set; } = new[] { new Lamp() }; }
    public sealed class Lamp { public UnityEngine.Color Colour { get; set; } = new UnityEngine.Color { g = 1 }; }
}
namespace Signals.Game.Railway
{
    public enum OccupancyMode { All = 0 }
    public static class TrackChecker
    { public static bool IsOccupied(RailTrack track, OccupancyMode mode) => track.Occupied; }
    public static class TrackReserver
    {
        public static bool IsTrackReserved(RailTrack track, out object? signal)
        { signal = track.ReservationToken; return track.Reserved; }
    }
}
namespace AITraffic.Core
{
    public sealed class TrafficManager : UnityEngine.Object
    {
        private static TrafficManager? s_instance;
        public static int InstanceGetterCalls;
        public static TrafficManager Instance { get { InstanceGetterCalls++; return s_instance ??= new TrafficManager(); } }
        public static void SetExisting(TrafficManager? value) => s_instance = value;
    }
}
namespace AITraffic.Driver { public sealed class AIEngineer { } }
namespace AITraffic.Navigation
{
    public sealed class RailGraph
    {
        private static RailGraph? s_instance;
        public static RailGraph? ExistingForTest => s_instance;
        public readonly Dictionary<RailTrack, object> Owners = new Dictionary<RailTrack, object>();
        public static void SetExisting(RailGraph? value) => s_instance = value;
        public bool TryGetTrackReservationHolder(RailTrack track, out object? reservation, out object? owner)
        { reservation = null; return Owners.TryGetValue(track, out owner); }
    }
    public static class SignalRegistry
    {
        public static bool FailEnrichment;
        public static int ReservationHelperCalls;
        public static string GetAspectDisplayName(Signals.Game.Signal signal)
            => FailEnrichment ? throw new InvalidOperationException("Test enrichment unavailable") : "Enriched aspect";
        public static string GetAspectColorHex(Signals.Game.Signal signal) => "#123456";
        public static string GetSignalName(Signals.Game.Signal signal)
            => FailEnrichment ? throw new InvalidOperationException("Test enrichment unavailable") : "Enriched " + signal.Name;
        public static bool IsSignalReservedByAI(object token)
        { ReservationHelperCalls++; _ = AITraffic.Core.TrafficManager.Instance; return token is bool value && value; }
    }
}
// The deliberate native-boundary delay ensures a read spans Update calls. It is
// scheduling control for cancellation tests, never a performance measurement.
public sealed class SlowControllers : IEnumerable
{
    public readonly object Controller;
    public bool Disposed;
    public SlowControllers(object controller) => Controller = controller;
    public IEnumerator GetEnumerator() => new Enumerator(this);
    private sealed class Enumerator : IEnumerator, IDisposable
    {
        private readonly SlowControllers owner;
        private bool yielded;
        public Enumerator(SlowControllers owner) => this.owner = owner;
        public object Current => owner.Controller;
        public bool MoveNext()
        {
            if (yielded) return false;
            System.Threading.Thread.Sleep(5);
            yielded = true;
            return true;
        }
        public void Reset() => throw new NotSupportedException();
        public void Dispose() => owner.Disposed = true;
    }
}
