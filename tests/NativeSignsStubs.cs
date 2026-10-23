// Native/Harmony boundary doubles. The production association/index code is compiled unchanged.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class HarmonyPatch : Attribute { public HarmonyPatch() { } public HarmonyPatch(Type type, string method) { } }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPostfix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPriority : Attribute { public HarmonyPriority(int priority) { } }
    public static class Priority { public const int Last = 0; }
    public sealed class HarmonyMethod
    {
        public readonly System.Reflection.MethodInfo method;
        public int priority;
        public HarmonyMethod(System.Reflection.MethodInfo method) => this.method = method;
    }
    public sealed class Patch
    {
        public readonly string owner;
        public readonly System.Reflection.MethodInfo PatchMethod;
        public Patch(string owner, System.Reflection.MethodInfo method) { this.owner = owner; PatchMethod = method; }
    }
    public sealed class Patches { public readonly List<Patch> Postfixes = new List<Patch>(); }
    public sealed class Harmony
    {
        private static readonly Dictionary<System.Reflection.MethodBase, Patches> patches = new Dictionary<System.Reflection.MethodBase, Patches>();
        private readonly string owner;
        public static int PatchCalls;
        public Harmony(string owner) => this.owner = owner;
        public static Patches? GetPatchInfo(System.Reflection.MethodBase method) => patches.TryGetValue(method, out var value) ? value : null;
        public void Patch(System.Reflection.MethodBase method, HarmonyMethod? postfix = null)
        {
            PatchCalls++;
            if (!patches.TryGetValue(method, out var value)) patches[method] = value = new Patches();
            if (postfix != null) value.Postfixes.Add(new Patch(owner, postfix.method));
        }
        public static void InvokePostfixes(System.Reflection.MethodBase method, object instance, object data, UnityEngine.GameObject root)
        { if (patches.TryGetValue(method, out var value)) foreach (var patch in value.Postfixes) patch.PatchMethod.Invoke(null, new[] { instance, data, root }); }
        public static void Reset() { patches.Clear(); PatchCalls = 0; }
    }
}
namespace UnityEngine
{
    public class Object
    {
        private static int nextId;
        private static readonly List<Object> scene = new List<Object>();
        private readonly int id = ++nextId;
        public static int FindCalls, ComponentReads;
        public Object() => scene.Add(this);
        public bool Destroyed;
        public string name = "Test object";
        public int GetInstanceID() => id;
        public static T[] FindObjectsOfType<T>() where T : Object
        { FindCalls++; return scene.OfType<T>().Where(value => value != null).ToArray(); }
        public static Object[] FindObjectsOfType(Type type)
        { FindCalls++; return scene.Where(value => type.IsInstanceOfType(value) && value != null).ToArray(); }
        public static void ClearScene() { scene.Clear(); FindCalls = ComponentReads = 0; }
        public static bool operator ==(Object? a, Object? b)
        {
            var absentA = a is null || a.Destroyed;
            var absentB = b is null || b.Destroyed;
            return absentA || absentB ? absentA == absentB : ReferenceEquals(a, b);
        }
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => id;
    }
    public class Component : Object
    {
        public GameObject gameObject = null!;
        public Transform transform => gameObject.transform;
        public T? GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
    }
    public class MonoBehaviour : Component { }
    public sealed class GameObject : Object
    {
        private readonly Dictionary<Type, Component> components = new Dictionary<Type, Component>();
        public readonly Transform transform = new Transform();
        public GameObject(string objectName = "Test root") => name = objectName;
        public T Add<T>(T component) where T : Component
        { component.gameObject = this; components[typeof(T)] = component; return component; }
        public T? GetComponent<T>() where T : Component
        { Object.ComponentReads++; return components.TryGetValue(typeof(T), out var value) && value != null ? (T)value : null; }
    }
    public sealed class Transform { public Vector3 position; public Quaternion rotation; }
    public struct Vector3 : IEquatable<Vector3>
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 left => new Vector3(-1, 0, 0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float value) => new Vector3(a.x * value, a.y * value, a.z * value);
        public static bool operator ==(Vector3 a, Vector3 b) => a.Equals(b);
        public static bool operator !=(Vector3 a, Vector3 b) => !a.Equals(b);
        public static float SqrMagnitude(Vector3 value) => value.x * value.x + value.y * value.y + value.z * value.z;
        public bool Equals(Vector3 value) => x == value.x && y == value.y && z == value.z;
        public override bool Equals(object? value) => value is Vector3 vector && Equals(vector);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
        public string ToString(string format) => "(" + x.ToString(format, CultureInfo.InvariantCulture) + ","
            + y.ToString(format, CultureInfo.InvariantCulture) + "," + z.ToString(format, CultureInfo.InvariantCulture) + ")";
    }
    public struct Quaternion
    {
        public Vector3 eulerAngles;
        public static Quaternion Euler(Vector3 value) => new Quaternion { eulerAngles = value };
        public static Vector3 operator *(Quaternion value, Vector3 vector)
        {
            var radians = value.eulerAngles.y * Math.PI / 180;
            return new Vector3((float)(vector.x * Math.Cos(radians) + vector.z * Math.Sin(radians)), vector.y,
                (float)(-vector.x * Math.Sin(radians) + vector.z * Math.Cos(radians)));
        }
        public static float Angle(Quaternion a, Quaternion b)
        { var delta = Math.Abs(a.eulerAngles.y - b.eulerAngles.y) % 360; return Math.Min(delta, 360 - delta); }
    }
    public static class Mathf { public static int FloorToInt(float value) => (int)Math.Floor(value); }
    public static class Time { public static float realtimeSinceStartup; }
}
namespace UnityModManagerNet
{
    public static class UnityModManager
    {
        public sealed class ModEntry
        {
            public bool Loaded, Active;
            public System.Reflection.Assembly? Assembly;
        }
        public static ModEntry? DoubleTrack;
        public static ModEntry? FindMod(string id) => id == "DoubleTrack" ? DoubleTrack : null;
    }
}
public static class WorldMover { public static UnityEngine.Vector3 currentMove; }
public sealed class BaseSign : UnityEngine.MonoBehaviour { }
public sealed class RailTrack : UnityEngine.Component
{
    private Action<RailTrack>? updated;
    public bool ProjectionAvailable = true;
    public int ProjectionReads;
    public float OriginX, ExpectedLateralZ;
    public Junction? inJunction, outJunction;
    public int Subscribers => updated?.GetInvocationList().Length ?? 0;
    public event Action<RailTrack> TrackPointsUpdated { add => updated += value; remove => updated -= value; }
    public void Regenerate() => updated?.Invoke(this);
}
public sealed class Junction : UnityEngine.Component
{
    public sealed class Branch { public RailTrack track = null!; }
    public Branch inBranch = new Branch();
}
namespace DvMod.HeadsUpDisplay
{
    public static class NativeRouteGeometry
    {
        public static bool TryProjectKnownTrack(RailTrack track, UnityEngine.Vector3 scenePosition, out double span)
        {
            track.ProjectionReads++;
            span = scenePosition.x - WorldMover.currentMove.x - track.OriginX;
            return track.ProjectionAvailable && Math.Abs(scenePosition.z - WorldMover.currentMove.z - track.ExpectedLateralZ) < 0.001;
        }
    }
}
namespace DV.Signs
{
    public enum SignType { SpeedLimit, Grade, WarningBoard }
    public struct SignParameters
    {
        public SignType type;
        public string signText;
        public BaseSign? sign;
        public UnityEngine.GameObject[]? accessories;
    }
    public sealed class Sign : UnityEngine.Object
    {
        private readonly SignParameters[] signParameters;
        public Sign(SignParameters[] parts) => signParameters = parts;
        public SignParameters[] PartsForTest => signParameters;
    }
    public sealed class SignGeneratorData : UnityEngine.Component { public SignParameters[] signParameters = Array.Empty<SignParameters>(); }
    public sealed class SignDebug : UnityEngine.Component { public string text = ""; }
    public sealed class SignPlacer : UnityEngine.Component
    {
        public bool placeTrackSigns, placeJunctionSigns;
        public float sideways;
        public string[]? noSignsTrackNameMarks;
        private readonly List<SignData> plans = new List<SignData>();
        public int RecipeCalls;
        public object AddPlan(RailTrack track, UnityEngine.Vector3 position, bool flipped, SignParameters[] parts)
        { var result = new SignData(track, position, flipped, parts); plans.Add(result); return result; }
        public static object Data(RailTrack track, UnityEngine.Vector3 position, bool flipped, SignParameters[] parts)
            => new SignData(track, position, flipped, parts);
        private static bool ShouldIncludeTrack(RailTrack track) => track != null;
        private IEnumerable GetTrackSigns(RailTrack track, bool reverse)
        { RecipeCalls++; return plans.Where(value => value.GetTrack() == track && value.FlippedForTest == reverse).ToArray(); }
        private IEnumerable GetJunctionSigns(Junction junction) => Array.Empty<object>();
        private sealed class SignData
        {
            private readonly RailTrack track;
            private readonly UnityEngine.Vector3 position;
            private readonly bool flip;
            private readonly Sign sign;
            public SignData(RailTrack track, UnityEngine.Vector3 position, bool flipped, SignParameters[] parts)
            { this.track = track; this.position = position - WorldMover.currentMove; flip = flipped; sign = new Sign(parts); }
            public bool FlippedForTest => flip;
            public RailTrack GetTrack() => track;
            public UnityEngine.Vector3 GetPosition() => position + WorldMover.currentMove;
            public UnityEngine.Quaternion GetRotation() => default;
            public Sign GetSign() => sign;
        }
    }

    // DoubleTrack 2.1.1 is a separate placer, not a SignPlacer subtype. Its private
    // recipe uses native Sign/SignParameters and native SignGeneratorData roots.
    public sealed class CustomSignPlacer : UnityEngine.Component
    {
        public bool placeTrackSigns = true, placeJunctionSigns;
        public float sideways;
        public List<string> noSignsTrackNameMarks = new List<string>();
        private readonly List<SignData> plans = new List<SignData>();
        public int RecipeCalls, PlacementCalls;
        public object AddPlan(RailTrack track, UnityEngine.Vector3 anchor, bool flip, SignParameters[] parts)
        { var data = new SignData(track, anchor, flip, parts); plans.Add(data); return data; }
        public void ObservePlacementForTest(object data, UnityEngine.GameObject root)
            => HarmonyLib.Harmony.InvokePostfixes(typeof(CustomSignPlacer).GetMethod("PlaceSign", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!, this, data, root);
        private static bool ShouldIncludeTrack(RailTrack track) => track != null;
        private IEnumerable GetTrackSigns(RailTrack track, bool reverse)
        { RecipeCalls++; return plans.Where(data => data.GetTrack() == track && data.FlippedForTest == reverse).ToArray(); }
        private IEnumerable GetJunctionSigns(Junction junction) => Array.Empty<object>();
        private UnityEngine.GameObject PlaceSign(SignData data)
        { PlacementCalls++; throw new InvalidOperationException("Read-only bootstrap must never create a sign"); }
        private sealed class SignData
        {
            private readonly RailTrack track;
            private readonly UnityEngine.Vector3 anchor;
            private readonly bool flip;
            private readonly Sign sign;
            public SignData(RailTrack track, UnityEngine.Vector3 anchor, bool flip, SignParameters[] parts)
            { this.track = track; this.anchor = anchor - WorldMover.currentMove; this.flip = flip; sign = new Sign(parts); }
            public bool FlippedForTest => flip;
            public RailTrack GetTrack() => track;
            public UnityEngine.Vector3 GetPosition() => anchor + WorldMover.currentMove;
            public UnityEngine.Quaternion GetRotation() => default;
            public Sign GetSign() => sign;
        }
    }
}
