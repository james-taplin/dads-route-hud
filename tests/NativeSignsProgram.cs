using System;
using System.Linq;
using System.Reflection;
using DV.Signs;
using DvMod.HeadsUpDisplay;
using UnityEngine;

internal static class NativeSignsProgram
{
    private static int checks;
    private static readonly MethodInfo observe = typeof(NativeRouteSignIndex).GetMethod("ObservePlacement", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo register = typeof(NativeRouteSignIndex).GetMethod("Register", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly Type sourceType = typeof(NativeRouteSignIndex).GetNestedType("Source", BindingFlags.NonPublic)!;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; }
    private static RailTrack Track(string name) => new GameObject(name).Add(new RailTrack { name = name });
    private static SignPlacer Placer(bool plans = false) => new GameObject("Placer").Add(new SignPlacer { placeTrackSigns = plans });
    private static SignParameters Part(SignType type, string text, GameObject? prefab = null, GameObject[]? accessories = null)
        => new SignParameters { type = type, signText = text, sign = prefab, accessories = accessories };
    private static GameObject Root(Vector3 position, SignParameters[] parts)
    {
        var root = new GameObject("Native sign root");
        root.transform.position = position;
        root.Add(new SignGeneratorData { signParameters = parts });
        root.Add(new SignDebug { text = string.Join("\n", parts.Select(part => part.signText)) });
        return root;
    }
    private static void Observe(SignPlacer placer, RailTrack track, Vector3 anchor, GameObject root, bool flip = false)
        => observe.Invoke(null, new[] { (object)placer, SignPlacer.Data(track, anchor, flip, root.GetComponent<SignGeneratorData>()!.signParameters), root });
    private static void Register(GameObject root, RailTrack track, bool captured, SignParameters[] parts)
    {
        var source = Activator.CreateInstance(sourceType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { root, track, true, default(Vector3), parts, captured }, null)!;
        register.Invoke(null, new[] { source });
    }
    private static void Prepare()
    {
        NativeRouteSignIndex.Reset();
        UnityEngine.Object.ClearScene();
        Time.realtimeSinceStartup = 1;
        WorldMover.currentMove = default;
    }
    private static void Main()
    {
        CapturedMembershipAndParts();
        PrecedenceAndAmbiguity();
        RecipeSignatureAndCache();
        RejectedRecipeAndProjection();
        NativeRouteSignIndex.Reset();
        Console.WriteLine(checks + " actual native-sign index checks passed (reflection/Unity/native boundary doubles; not game acceptance).");
    }

    private static void CapturedMembershipAndParts()
    {
        Prepare();
        var a = Track("A");
        var b = Track("B");
        var placer = Placer();
        var speedPrefab = new GameObject("SpeedPrefab");
        var accessory = new GameObject("ExtraArrow");
        var parts = new[]
        {
            Part(SignType.SpeedLimit, "8", speedPrefab, new[] { accessory, null! }),
            Part(SignType.Grade, "-1.5", new GameObject("GradePrefab"))
        };
        var root = Root(new Vector3(100, 0, 3), parts);
        Observe(placer, a, new Vector3(100, 0, 0), root);
        var entries = NativeRouteSignIndex.EnsureTrack(a);
        Check(entries.Count == 4, "Captured root retains every sign part and accessory including unavailable accessory");
        Check(entries.Any(s => s.Kind == RouteEventKind.Speed && s.Label == "80 km/h"), "Native speed text is converted using existing native convention");
        Check(entries.Any(s => s.Kind == RouteEventKind.Grade && s.Label == "-1.5 % falling"), "Native grade uses signed readable description");
        Check(entries.All(s => s.Span == 100 && s.Forward), "Captured anchor cancels native pole displacement before projection");
        Check(entries.All(s => s.Fact.Contains("Captured native placement")), "Captured placement evidence remains explicit");
        Check(entries.First().Fact.Contains("raw text: 8") && entries.First().Fact.Contains("prefab: SpeedPrefab"), "Raw text and prefab evidence are retained");
        var prefix = "native-sign:" + root.GetInstanceID();
        Check(entries.Select(s => s.SourceId).Distinct().Count() == 4 && entries.All(s => s.SourceId.StartsWith(prefix)), "Part and accessory source identities remain distinct under exact root identity");
        Check(entries.Any(s => s.Label == "Accessory · unavailable"), "Missing accessory is represented instead of dropped");
        Check(a.Subscribers == 1, "First track subscribes to native geometry invalidation once");
        Check(ReferenceEquals(entries, NativeRouteSignIndex.EnsureTrack(a)) && a.Subscribers == 1, "Repeated reads reuse immutable cache without duplicate subscription");
        var aProjectionReads = a.ProjectionReads;
        Check(NativeRouteSignIndex.EnsureTrack(b).Count == 0, "Parallel track never receives captured signs from another track");
        Check(a.ProjectionReads == aProjectionReads && b.ProjectionReads == 0, "Track membership index avoids projecting unrelated sources");
        var revision = NativeRouteSignIndex.SnapshotRevision;
        Observe(placer, b, new Vector3(100, 0, 0), root, flip: true);
        Check(NativeRouteSignIndex.SnapshotRevision > revision, "Native recapture advances revision");
        Check(NativeRouteSignIndex.EnsureTrack(a).Count == 0, "Captured reassignment removes old track membership and cached observations");
        Check(NativeRouteSignIndex.EnsureTrack(b).Count == 4 && NativeRouteSignIndex.EnsureTrack(b).All(s => !s.Forward), "Captured reassignment uses new exact track and direction");
        WorldMover.currentMove = new Vector3(1000, 0, 500);
        root.transform.position += WorldMover.currentMove;
        b.Regenerate();
        Check(NativeRouteSignIndex.EnsureTrack(b).All(s => s.Span == 100), "Anchor offset preserves exact identity across world shift");
        root.Destroyed = true;
        Time.realtimeSinceStartup += 6;
        revision = NativeRouteSignIndex.SnapshotRevision;
        NativeRouteSignIndex.Prune();
        Check(NativeRouteSignIndex.SnapshotRevision > revision && NativeRouteSignIndex.EnsureTrack(b).Count == 0, "Prune removes destroyed root membership and invalidates observations");
        NativeRouteSignIndex.Reset();
        Check(a.Subscribers == 0 && b.Subscribers == 0, "Reset unsubscribes every live tracked native geometry event");
    }

    private static void PrecedenceAndAmbiguity()
    {
        Prepare();
        var a = Track("A");
        var b = Track("B");
        Placer();
        var parts = new[] { Part(SignType.WarningBoard, "Native text") };
        var root = Root(new Vector3(10, 0, 0), parts);
        Register(root, a, true, parts);
        Register(root, b, false, parts);
        Check(NativeRouteSignIndex.EnsureTrack(a).Count == 1 && NativeRouteSignIndex.EnsureTrack(b).Count == 0,
            "Late recipe cannot override conflicting captured track identity");
        Register(root, b, true, parts);
        Check(NativeRouteSignIndex.EnsureTrack(a).Count == 0 && NativeRouteSignIndex.EnsureTrack(b).Count == 1,
            "New captured identity supersedes previous captured membership atomically");

        Prepare();
        a = Track("A"); b = Track("B"); Placer();
        root = Root(new Vector3(10, 0, 0), parts);
        Register(root, a, false, parts);
        Register(root, b, false, parts);
        Check(NativeRouteSignIndex.EnsureTrack(a).Count == 0 && NativeRouteSignIndex.EnsureTrack(b).Count == 0,
            "Conflicting uncaptured plans quarantine root instead of assigning nearby track");
        Check(NativeRouteSignIndex.Diagnostics.Any(p => p.Contains("Ambiguous native-plan association rejected")), "Conflicting plan identity is diagnosed");
        Check(NativeRouteSignIndex.Coverage.Contains("coverage partial"), "Ambiguous identity cannot claim complete native coverage");
        var empty = Root(new Vector3(20, 0, 0), Array.Empty<SignParameters>());
        Register(empty, a, true, Array.Empty<SignParameters>());
        var pole = NativeRouteSignIndex.EnsureTrack(a).Single();
        Check(pole.SourceId.EndsWith(":pole") && pole.Label.Contains("no parameters retained"), "Native pole without parameters remains an explicit observation");
    }

    private static void RecipeSignatureAndCache()
    {
        Prepare();
        var track = Track("Late track");
        var placer = Placer(plans: true);
        var prefab = new GameObject("Recipe prefab");
        var parts = new[] { Part(SignType.SpeedLimit, "8", prefab) };
        var root = Root(new Vector3(50, 0, 0), parts);
        placer.AddPlan(track, new Vector3(50, 0, 0), false, parts);
        var entries = NativeRouteSignIndex.EnsureTrack(track);
        Check(entries.Count == 1 && entries[0].Label == "80 km/h", "Actual native recipe validates observed root before late association");
        Check(entries[0].Fact.Contains("Native recipe matched to observed sign"), "Late recipe evidence differs from captured placement evidence");
        Check(NativeRouteSignIndex.Coverage.Contains("1 validated plan matches") && NativeRouteSignIndex.Coverage.Contains("0 unmatched plans"), "Matched plan coverage is reported from actual bootstrap");
        var calls = placer.RecipeCalls;
        Time.realtimeSinceStartup += 6;
        Check(NativeRouteSignIndex.EnsureTrack(track).Count == 1 && placer.RecipeCalls == calls, "Unchanged cached signature skips repeating native recipe bootstrap after prune");
        parts[0] = Part(SignType.SpeedLimit, "9", prefab);
        Time.realtimeSinceStartup += 6;
        var refreshed = NativeRouteSignIndex.EnsureTrack(track);
        Check(refreshed.Count == 1 && refreshed[0].Label == "90 km/h", "Changed raw native sign content refreshes published observations");
        Check(refreshed[0].SourceId == entries[0].SourceId, "Content edits preserve exact retained source identity");
        Check(placer.RecipeCalls == calls, "Changed content keeps already established exact root association without redundant recipe enumeration");
        var extraParts = new[] { Part(SignType.WarningBoard, "Late root", prefab) };
        var extraRoot = Root(new Vector3(75, 0, 0), extraParts);
        placer.AddPlan(track, new Vector3(75, 0, 0), false, extraParts);
        Time.realtimeSinceStartup += 6;
        refreshed = NativeRouteSignIndex.EnsureTrack(track);
        Check(refreshed.Count == 2 && refreshed.Any(s => s.SourceId.StartsWith("native-sign:" + extraRoot.GetInstanceID() + ":")),
            "Newly observed native root invalidates membership bootstrap and becomes associated by exact recipe");
        Check(placer.RecipeCalls > calls, "New unassociated root triggers fresh native recipe validation");
        var revision = NativeRouteSignIndex.SnapshotRevision;
        calls = placer.RecipeCalls;
        track.Regenerate();
        Check(NativeRouteSignIndex.SnapshotRevision > revision, "Native geometry event advances sign snapshot revision");
        Check(!ReferenceEquals(refreshed, NativeRouteSignIndex.EnsureTrack(track)) && placer.RecipeCalls == calls,
            "Geometry invalidation refreshes track cache while established root associations avoid redundant native recipes");
        NativeRouteSignIndex.Reset();
        Check(track.Subscribers == 0, "Late-enable index reset releases geometry subscription");
    }

    private static void RejectedRecipeAndProjection()
    {
        Prepare();
        var track = Track("Recipe track");
        var placer = Placer(plans: true);
        var firstPrefab = new GameObject("First prefab");
        var secondPrefab = new GameObject("Second prefab");
        var parts = new[] { Part(SignType.SpeedLimit, "8", firstPrefab) };
        Root(new Vector3(50, 0, 0), parts);
        placer.AddPlan(track, new Vector3(50, 0, 0), false, new[] { Part(SignType.SpeedLimit, "8", secondPrefab) });
        Check(NativeRouteSignIndex.EnsureTrack(track).Count == 0, "Matching visible text cannot override different native prefab signature");
        Check(NativeRouteSignIndex.Diagnostics.Any(p => p.Contains("Unmatched native sign recipe")), "Signature mismatch records raw uncertainty instead of inventing event");

        Prepare();
        track = Track("Accessory identity"); placer = Placer(plans: true);
        parts = new[] { Part(SignType.SpeedLimit, "8", firstPrefab, new[] { new GameObject("Observed accessory") }) };
        Root(new Vector3(50, 0, 0), parts);
        placer.AddPlan(track, new Vector3(50, 0, 0), false,
            new[] { Part(SignType.SpeedLimit, "8", firstPrefab, new[] { new GameObject("Different recipe accessory") }) });
        Check(NativeRouteSignIndex.EnsureTrack(track).Count == 0, "Cached native signature includes accessory identity as well as raw text and primary prefab");

        Prepare();
        track = Track("Text-only observed root"); placer = Placer(plans: true);
        var textRoot = new GameObject("Debug-only sign");
        textRoot.transform.position = new Vector3(50, 0, 0);
        textRoot.Add(new SignDebug { text = "8" });
        placer.AddPlan(track, new Vector3(50, 0, 0), false, new[] { Part(SignType.SpeedLimit, "8", firstPrefab) });
        var textOnly = NativeRouteSignIndex.EnsureTrack(track);
        Check(textOnly.Count == 1 && textOnly[0].Label == "80 km/h", "Observed debug-only root can validate unique native recipe text without generated parameter component");
        Check(textOnly[0].Fact.Contains("raw text: 8") && textOnly[0].Fact.Contains("First prefab"), "Debug-only fallback retains recipe parameter evidence after association");

        Prepare();
        track = Track("Ambiguous scene"); placer = Placer(plans: true);
        parts = new[] { Part(SignType.WarningBoard, "8", firstPrefab) };
        Root(new Vector3(50, 0, 0), parts); Root(new Vector3(50, 0, 0), parts);
        placer.AddPlan(track, new Vector3(50, 0, 0), false, parts);
        Check(NativeRouteSignIndex.EnsureTrack(track).Count == 0, "Two matching observed roots cannot establish unique recipe association");
        Check(NativeRouteSignIndex.Diagnostics.Any(p => p.Contains("Ambiguous native sign recipe")), "Multiple observed candidates remain explicit ambiguity");

        Prepare();
        track = Track("Known identity"); placer = Placer();
        var root = Root(new Vector3(50, 0, 0), parts);
        Observe(placer, track, root.transform.position, root);
        track.ProjectionAvailable = false;
        Check(NativeRouteSignIndex.EnsureTrack(track).Count == 0, "Unavailable known-track projection is never assigned to guessed distance");
        Check(NativeRouteSignIndex.Diagnostics.Any(p => p.Contains("Known-track sign projection unavailable")), "Unavailable known-track projection records explicit source diagnostic");
        track.ProjectionAvailable = true;
        track.Regenerate();
        Check(NativeRouteSignIndex.EnsureTrack(track).Count == 1, "Known source identity survives unavailable projection and native retry");
        track.Destroyed = true;
        Time.realtimeSinceStartup += 6;
        NativeRouteSignIndex.Prune();
        Check(NativeRouteSignIndex.EnsureTrack(track).Count == 0, "Destroyed native track never yields retained sign observations");
    }
}
