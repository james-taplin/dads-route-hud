// Test doubles for native boundaries only. The actual RouteMonitorService is compiled unchanged.
using System;
using System.Collections.Generic;
using DvMod.HeadsUpDisplay;

namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
namespace DV.Simulation.Controllers { public static class ReverserControl { public const float NEUTRAL_VALUE = 0.5f; } }

public sealed class RailTrack
{
    public string name = "Track";
    public double Length = 1000;
    public RailTrack? Next;
    public Junction? inJunction, outJunction;
    public event Action<RailTrack>? TrackPointsUpdated;
    public void Regenerate() => TrackPointsUpdated?.Invoke(this);
    public int GetInstanceID() => GetHashCode();
    public PointSet GetKinkedPointSet() => new PointSet { span = Length };
}
public sealed class PointSet { public double span; }
public sealed class Junction
{
    public int GetInstanceID() => GetHashCode();
    public sealed class Branch { public RailTrack? track; }
    public string name = "Switch";
    public int selectedBranch;
    public Branch? inBranch;
    public List<Branch>? outBranches;
}
public sealed class TrainCar
{
    public Bogie[] Bogies = { new Bogie(), new Bogie() };
    public SimController SimController = new SimController();
    public float Speed = 1;
    public float GetForwardSpeed() => Speed;
}
public sealed class Bogie
{
    public RailTrack? track;
    public Traveller traveller = new Traveller();
    public int TrackDirectionSign = 1;
}
public sealed class Traveller { public double Span; }
public sealed class SimController { public Controls controlsOverrider = new Controls(); }
public sealed class Controls { public Lever Reverser = new Lever(); }
public sealed class Lever { public float Value = 1; }
public static class PlayerManager { public static TrainCar? LastLoco; }

namespace DvMod.HeadsUpDisplay
{
    public static class Main
    {
        public static Settings settings = new Settings();
        public static Mod? mod = new Mod();
    }
    public sealed class Mod { public Logger Logger = new Logger(); }
    public sealed class Logger
    {
        public readonly List<string> Messages = new List<string>();
        public void Log(string message) => Messages.Add(message);
        public void Warning(string message) => throw new Exception(message);
    }
    public sealed class Settings
    {
        public TrackSettings trackInfoSettings = new TrackSettings();
        public bool enableLogging = true;
    }
    public sealed class TrackSettings { public double maxEventSpan = 500; }
    public sealed class NativeRouteGraph : IRouteGraph<RailTrack>
    {
        public bool TryGetLength(RailTrack track, out double length) { length = track.Length; return true; }
        public bool TryGetNext(RailTrack track, bool forward, out RailTrack next, out bool nextForward)
        { next = track.Next!; nextForward = forward; return next != null; }
    }
    public static class NativeRouteGeometry
    {
        public static bool TryJunctionHeading(RailTrack track, bool forward, out double x, out double z)
            => TryHeading(track, forward, false, out x, out z);
        public static bool TrySample(RailTrack track, double span, bool forward, out double elevation, out double grade)
        { elevation = grade = 0; return true; }
        public static bool TryHeading(RailTrack track, bool forward, bool end, out double x, out double z)
        { x = 0; z = 1; return true; }
    }
    public static class NativeTrackNames
    {
        public static void Reset() { }
        public static void Describe(RailTrack track, out string label, out string code) { label = code = track.name; }
        public static string StationNameForTrack(RailTrack track) => "";
    }
    public static class NativeJunctionNames
    {
        public static void Reset() { }
        public static void Update() { }
        public static string Describe(Junction junction, RailTrack approachTrack, bool trailing,
            bool setForRoute, out RouteTurnDirection turn, bool approachForward = true)
        {
            turn = RouteTurnDirection.Unknown;
            return RouteNaming.SwitchText("", junction.name,
                junction.selectedBranch, junction.outBranches?.Count ?? 0, trailing, setForRoute, turn);
        }
    }
    public sealed class NativeRouteSign
    {
        public double Span;
        public string SourceId = "", Label = "", Fact = "";
        public RouteEventKind Kind;
        public bool Forward;
        public double? SpeedLimitKph, AlternateSpeedLimitKph;
    }
    public static class NativeRouteSignIndex
    {
        public static Action<RailTrack>? OnEnsureTrack;
        public static long SnapshotRevision => 0;
        public static string Coverage => "Test native observations";
        public static IReadOnlyList<string> Diagnostics => Array.Empty<string>();
        public static IEnumerable<NativeRouteSign> EnsureTrack(RailTrack track)
        { OnEnsureTrack?.Invoke(track); return Array.Empty<NativeRouteSign>(); }
    }
    public sealed class SignalTrackEvent
    {
        public double span = 100;
        public string SourceId = "signal", Name = "Signal", Aspect = "Green", ColorHex = "00FF00";
        public string Reservation = "", Freshness = "Test observation";
        public bool FacingTravel = true;
    }
    public static class OptionalSignalTelemetry
    {
        private static long sampledRevision = -1;
        private static int remaining;
        private static object? sampledSource;
        public static int ResetCount;
        public static int PassFrames = 3;
        public static bool PartialSample = true;
        public static object? ActiveSignalSource { get; set; } = new object();
        public static object? ActiveTrafficSource => null;
        public static bool IsAvailable => ActiveSignalSource != null;
        public static bool IsReading => remaining > 0;
        public static bool IsComplete => !IsReading && !PartialSample;
        public static string Status => IsReading ? "Reading optional signals; observations partial"
            : PartialSample ? "Optional signals partial (1 diagnostics)" : "Test sample ready";
        public static IReadOnlyList<string> Diagnostics => PartialSample ? new[] { "Test unassociated controller" } : Array.Empty<string>();
        public static IReadOnlyList<SignalTrackEvent> UpcomingSignals { get; private set; } = Array.Empty<SignalTrackEvent>();
        public static string GetTrackReservation(RailTrack track) => "";
        public static void Reset()
        { ResetCount++; sampledRevision = -1; remaining = 0; sampledSource = null; UpcomingSignals = Array.Empty<SignalTrackEvent>(); }
        public static void Update(TrainCar? car, IReadOnlyList<RouteInterval<RailTrack>> intervals, long revision)
        {
            if (!IsAvailable) { Reset(); return; }
            if (sampledRevision != revision || !ReferenceEquals(sampledSource, ActiveSignalSource))
            { sampledRevision = revision; sampledSource = ActiveSignalSource; remaining = PassFrames; UpcomingSignals = Array.Empty<SignalTrackEvent>(); }
            if (remaining > 0 && --remaining == 0) UpcomingSignals = new[] { new SignalTrackEvent() };
        }
    }
}
