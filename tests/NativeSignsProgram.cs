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
    private static BaseSign Prefab(string name) => new GameObject(name).Add(new BaseSign { name = name });
    private static SignParameters Part(SignType type, string text, BaseSign? prefab = null, GameObject[]? accessories = null)
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
        HarmonyLib.Harmony.Reset();
        UnityEngine.Object.ClearScene();
        UnityModManagerNet.UnityModManager.DoubleTrack = null;
        Time.realtimeSinceStartup = 1;
        WorldMover.currentMove = default;
        HudTimings.Enabled = false;
    }
    private static void Main()
    {
        ColdDiscoveryDoesNotSearchTheWorld();
        BoundedColdHierarchyAndSceneLifecycle();
        CapturedMembershipAndParts();
        PrecedenceAndAmbiguity();
        RecipeSignatureAndCache();
        RejectedRecipeAndProjection();
        DoubleTrackPlacementAndLateEnable();
        IncrementalMaintenanceAndOriginShift();
        OptionalDiscoveryAndLateRecipeShift();
        TypedSpeedBoards();
        EmptyGeneratorBeforeDebugText();
        NativeRouteSignIndex.Reset();
        Console.WriteLine(checks + " actual native-sign index checks passed (reflection/Unity/native boundary doubles; not game acceptance).");
    }

    private static void ColdDiscoveryDoesNotSearchTheWorld()
    {
        Prepare();
        var track = Track("Cold discovery");
        Placer(plans: true);
        Root(new Vector3(50, 0, 0), new[] { Part(SignType.SpeedLimit, "8") });
        NativeRouteSignIndex.EnsureTrack(track);
        Check(UnityEngine.Object.FindCalls == 0,
            "Cold route collection must not issue synchronous whole-world type searches");
    }

    private static void EmptyGeneratorBeforeDebugText()
    {
        Prepare(); var track = Track("Empty generator before debug"); var placer = Placer(plans: true);
        var root = new GameObject("Text-only evidence with empty generator"); root.transform.position = new Vector3(50, 0, 0);
        root.Add(new SignGeneratorData { signParameters = null! }); root.Add(new SignDebug { text = "8" });
        placer.AddPlan(track, root.transform.position, false, new[] { Part(SignType.SpeedLimit, "8") });
        Check(ReadTrack(track).Single().SpeedLimitKph == 80,
            "Component enumeration order preserves debug-text recipe evidence when generator parameters are absent");
    }

    private static void BoundedColdHierarchyAndSceneLifecycle()
    {
        Prepare();
        var track = Track("Cold wide hierarchy");
        var parent = new GameObject("Wide parent");
        var parts = new[] { Part(SignType.SpeedLimitDual, "4/6"), Part(SignType.Grade, "-2") };
        var roots = Enumerable.Range(0, 600).Select(i => Root(new Vector3(i + 10, 0, 0), parts)).ToArray();
        foreach (var root in roots) root.transform.SetParent(parent.transform);
        for (var i = 0; i < 1600; i++) new GameObject("Unrelated child").transform.SetParent(parent.transform);
        var deep = new GameObject("Deep parent").transform;
        for (var i = 0; i < 300; i++)
        {
            var child = new GameObject("Deep child"); child.transform.SetParent(deep); deep = child.transform;
        }
        // The placer deliberately follows every root in traversal order.
        var placer = Placer(plans: true);
        for (var i = 0; i < roots.Length; i++) placer.AddPlan(track, roots[i].transform.position, i % 2 == 0, parts);
        var inactive = Root(new Vector3(800, 0, 0), parts); inactive.activeSelf = false;
        placer.AddPlan(track, inactive.transform.position, false, parts);

        NativeRouteSignIndex.EnsureTrack(track);
        Check(!NativeRouteSignIndex.DiscoveryComplete && NativeRouteSignIndex.Coverage.Contains("discovery pending"),
            "Cold census is explicit pending coverage, never a synchronous completeness claim");
        var reads = UnityEngine.Object.ComponentReads;
        for (var i = 0; i < 1000; i++) NativeRouteSignIndex.EnsureTrack(track);
        Check(UnityEngine.Object.ComponentReads == reads && UnityEngine.SceneManagement.SceneManager.RootReads == 0,
            "Repeated per-track reads cannot advance discovery or enumerate roots within a collection frame");
        NativeRouteSignIndex.Prune();
        Check(!NativeRouteSignIndex.DiscoveryComplete && UnityEngine.Object.ComponentReads - reads <= 320,
            "First discovery update obeys its work bound instead of expanding a wide or deep hierarchy");

        // New-scene sign generation still arrives through the native observer.
        var extraScene = UnityEngine.SceneManagement.SceneManager.AddScene();
        var late = Root(new Vector3(900, 0, 0), parts);
        late.scene.Roots.Remove(late); late.scene = extraScene; extraScene.Roots.Add(late);
        placer.AddPlan(track, late.transform.position, false, parts);
        Observe(placer, track, late.transform.position, late);
        // Remove a visited/queued child so sibling indices move during the walk.
        roots[0].transform.SetParent(null);
        roots[0].Destroyed = true;
        var steps = 1;
        var bounded = true;
        while (!NativeRouteSignIndex.DiscoveryComplete && steps < 20000)
        {
            reads = UnityEngine.Object.ComponentReads;
            NativeRouteSignIndex.Prune();
            bounded &= UnityEngine.Object.ComponentReads - reads <= 320;
            steps++;
        }
        Check(NativeRouteSignIndex.DiscoveryComplete && steps > 1 && bounded,
            "Wide, deep and changed hierarchies finish across bounded updates without a total observation cap");
        var entries = NativeRouteSignIndex.EnsureTrack(track);
        Check(entries.Count == 1200 && entries.Select(e => e.SourceId).Distinct().Count() == 1200,
            "All live census-only roots in initial/newly generated scenes retain every source part once");
        Check(entries.Count(e => e.Kind == RouteEventKind.Speed && e.SpeedLimitKph == 40 && e.AlternateSpeedLimitKph == 60) == 600
            && entries.Any(e => e.Forward) && entries.Any(e => !e.Forward),
            "Cold discovery preserves dual speeds, opposing observations and grade parts");
        Check(entries.All(e => !e.SourceId.StartsWith("native-sign:" + inactive.GetInstanceID() + ":")),
            "Census preserves native active-hierarchy search semantics");
        Check(UnityEngine.Object.FindCalls == 0 && UnityEngine.SceneManagement.SceneManager.Subscribers == 0,
            "Discovery uses no whole-world searches or recurring scene-load census");
        var cache = NativeRouteSignIndex.EnsureTrack(track);
        NativeRouteSignIndex.Prune();
        Check(ReferenceEquals(cache, NativeRouteSignIndex.EnsureTrack(track)), "Settled census retains unchanged per-track caches");
        NativeRouteSignIndex.Reset();
        Check(UnityEngine.SceneManagement.SceneManager.Subscribers == 0 && !NativeRouteSignIndex.DiscoveryComplete,
            "Reset cancels discovery without a native scene subscription");

        Prepare(); track = Track("Known root during a long census"); placer = Placer();
        var known = Root(new Vector3(20, 0, 0), new[] { Part(SignType.SpeedLimit, "8") });
        Observe(placer, track, known.transform.position, known);
        var before = NativeRouteSignIndex.EnsureTrack(track);
        for (var i = 0; i < 1000; i++) new GameObject("Unrelated pending root");
        known.GetComponent<SignGeneratorData>()!.signParameters[0] = Part(SignType.SpeedLimit, "9");
        Time.realtimeSinceStartup += 6;
        NativeRouteSignIndex.Prune();
        var during = NativeRouteSignIndex.EnsureTrack(track);
        Check(!NativeRouteSignIndex.DiscoveryComplete && before.Single().SpeedLimitKph == 80 && during.Single().SpeedLimitKph == 90,
            "Bounded maintenance refreshes known sign edits while unrelated cold discovery is still pending");
        known.Destroyed = true; Time.realtimeSinceStartup += 6; NativeRouteSignIndex.Prune();
        Check(!NativeRouteSignIndex.DiscoveryComplete && NativeRouteSignIndex.EnsureTrack(track).Count == 0,
            "Destroyed captured signs clear during pending discovery rather than waiting for its completion");

        // The existing origin parent may belong to Unity's unlisted persistent scene.
        Prepare(); track = Track("Persistent origin signs"); placer = Placer(plans: true);
        var persistent = UnityEngine.SceneManagement.SceneManager.AddScene(listed: false);
        var origin = new GameObject("Existing origin parent");
        origin.scene.Roots.Remove(origin); origin.scene = persistent; persistent.Roots.Add(origin);
        DV.OriginShift.OriginShift.parentContainer = origin.transform;
        var sign = Root(new Vector3(60, 0, 0), parts); sign.transform.SetParent(origin.transform);
        placer.AddPlan(track, sign.transform.position, false, parts);
        Check(ReadTrack(track).Count == 2 && UnityEngine.Object.FindCalls == 0,
            "Existing native origin parent seeds persistent-scene signs without creating objects or scene searches");

        // Abandon a cold pass, then start another one; no old queued scene survives.
        Prepare(); track = Track("Cancelled discovery");
        NativeRouteSignIndex.EnsureTrack(track); NativeRouteSignIndex.Reset();
        Check(UnityEngine.SceneManagement.SceneManager.Subscribers == 0,
            "Reset also releases a pending census before its first update");
        NativeRouteSignIndex.EnsureTrack(track);
        FinishDiscovery();
        Check(UnityEngine.SceneManagement.SceneManager.Subscribers == 0 && UnityEngine.Object.FindCalls == 0,
            "Re-enablement needs no scene-load census subscription");
    }

    private static void FinishDiscovery()
    {
        for (var i = 0; i < 20000 && !NativeRouteSignIndex.DiscoveryComplete; i++) NativeRouteSignIndex.Prune();
        if (!NativeRouteSignIndex.DiscoveryComplete) throw new Exception("Incremental census failed to settle");
    }

    private static System.Collections.Generic.IReadOnlyList<NativeRouteSign> ReadTrack(RailTrack track)
    {
        NativeRouteSignIndex.EnsureTrack(track);
        NativeRouteSignIndex.Prune();
        FinishDiscovery();
        return NativeRouteSignIndex.EnsureTrack(track);
    }

    private static void FinishMaintenance(int roots)
    {
        for (var i = 0; i <= roots / 32 + 2; i++) NativeRouteSignIndex.Prune();
    }

    private static void TypedSpeedBoards()
    {
        Prepare(); var track = Track("Typed speed boards"); var placer = Placer();
        var root = Root(new Vector3(10, 0, 0), new[] { Part(SignType.SpeedLimit, "8"), Part(SignType.SpeedLimitDual, "4/6"), Part(SignType.SpeedLimitDual, "4/bad") });
        Observe(placer, track, root.transform.position, root);
        var signs = ReadTrack(track);
        Check(signs[0].SpeedLimitKph == 80 && signs[0].AlternateSpeedLimitKph == null, "Actual sign adapter retains typed single-board speed in km/h");
        Check(signs[1].SpeedLimitKph == 40 && signs[1].AlternateSpeedLimitKph == 60 && signs[1].Label == "40/60 km/h", "Actual native dual board retains both readings and readable units");
        Check(signs[2].SpeedLimitKph == null && signs[2].Label.Contains("4/bad"), "Malformed dual-board text remains a retained source observation without guessed limits");
        Check(signs[1].Fact.Contains("raw text: 4/6"), "Native raw board text remains available for source inspection");
    }

    private static void IncrementalMaintenanceAndOriginShift()
    {
        Prepare();
        var track = Track("Thousands of known roots");
        var placer = Placer();
        const int count = 2048;
        var roots = Enumerable.Range(0, count).Select(i => Root(new Vector3(i, 0, 0),
            new[] { Part(SignType.SpeedLimit, "8") })).ToArray();
        foreach (var root in roots) Observe(placer, track, root.transform.position, root);
        var entries = ReadTrack(track);
        Check(entries.Count == count, "Thousands of observed roots remain retained without an importance or count cap");
        var searches = UnityEngine.Object.FindCalls;
        var projections = track.ProjectionReads;
        Check(searches == 0, "Cold census performs no whole-world type searches");
        for (var cycle = 0; cycle < 3; cycle++)
        {
            FinishMaintenance(count);
            Time.realtimeSinceStartup += 6;
            var reads = UnityEngine.Object.ComponentReads;
            NativeRouteSignIndex.Prune();
            Check(UnityEngine.Object.ComponentReads - reads <= 64,
                "A recurring maintenance step reads no more than 32 known roots");
            FinishMaintenance(count);
            Check(ReferenceEquals(entries, ReadTrack(track)),
                "Unchanged time refresh preserves the immutable per-track cache");
        }
        Check(UnityEngine.Object.FindCalls == searches && track.ProjectionReads == projections,
            "Recurring maintenance never repeats scene discovery or known-track projection on unchanged data");

        var move = new Vector3(1200, 0, 350);
        WorldMover.currentMove = move;
        foreach (var root in roots) root.transform.position += move;
        Time.realtimeSinceStartup += 6;
        FinishMaintenance(count);
        Check(ReferenceEquals(entries, ReadTrack(track))
            && UnityEngine.Object.FindCalls == searches && track.ProjectionReads == projections,
            "World-origin movement alone preserves relative spatial cells and cached exact spans without a scene census");

        var revision = NativeRouteSignIndex.SnapshotRevision;
        roots[count - 1].GetComponent<SignGeneratorData>()!.signParameters[0] = Part(SignType.SpeedLimit, "9");
        Time.realtimeSinceStartup += 6;
        NativeRouteSignIndex.Prune();
        Check(ReferenceEquals(entries, ReadTrack(track)),
            "Changed content beyond the first batch is deferred rather than synchronously scanning all roots");
        FinishMaintenance(count);
        var changed = ReadTrack(track);
        Check(changed.Count == count && changed.Count(entry => entry.Label == "90 km/h") == 1
            && NativeRouteSignIndex.SnapshotRevision > revision,
            "Incremental maintenance reaches a late raw-content edit and retains every other observation");
        Check(changed.Last().SourceId == entries.Last().SourceId && UnityEngine.Object.FindCalls == searches,
            "Raw-content updates preserve source identity without global rediscovery");

        roots[0].Destroyed = true;
        roots[count - 1].Destroyed = true;
        Time.realtimeSinceStartup += 6;
        FinishMaintenance(count);
        Check(ReadTrack(track).Count == count - 2,
            "Incremental removal handles destroyed first and swapped last roots without losing live observations");
        Check(NativeRouteSignIndex.Coverage.Contains("0 scene roots unassociated")
            && UnityEngine.Object.FindCalls == searches,
            "Destroyed roots leave neither stale membership nor incomplete-coverage counts and trigger no scene scan");

        var late = Root(new Vector3(3000, 0, 350), new[] { Part(SignType.Grade, "+2") });
        Observe(placer, track, new Vector3(3000, 0, 350), late);
        var lateEntries = ReadTrack(track);
        Check(lateEntries.Count == count - 1 && lateEntries.Any(entry => entry.Kind == RouteEventKind.Grade && entry.Span == 1800),
            "Existing native placement observer captures new generation after an origin shift");
        Check(UnityEngine.Object.FindCalls == searches, "Late native placement indexes directly without rebuilding the scene census");
    }

    private static void OptionalDiscoveryAndLateRecipeShift()
    {
        Prepare();
        var track = Track("Optional activation after HUD discovery");
        var placer = new GameObject("Late active optional placer").Add(new CustomSignPlacer());
        var parts = new[] { Part(SignType.SpeedLimit, "8") };
        var first = Root(new Vector3(50, 0, 0), parts);
        placer.AddPlan(track, first.transform.position, false, parts);
        Check(ReadTrack(track).Count == 0,
            "Inactive optional implementation cannot invent an exact track association");
        var searches = UnityEngine.Object.FindCalls;
        UnityModManagerNet.UnityModManager.DoubleTrack = new UnityModManagerNet.UnityModManager.ModEntry
        { Loaded = true, Active = true, Assembly = typeof(CustomSignPlacer).Assembly };
        Time.realtimeSinceStartup += 6;
        NativeRouteSignIndex.Prune();
        Check(ReadTrack(track).Count == 1 && HarmonyLib.Harmony.PatchCalls == 1,
            "Optional activation discovers existing roots and installs the same passive placement observer once");
        Check(UnityEngine.Object.FindCalls == searches,
            "Optional activation discovers existing roots incrementally without whole-world type searches");
        searches = UnityEngine.Object.FindCalls;
        var second = Root(new Vector3(100, 0, 0), new[] { Part(SignType.Grade, "-1") });
        var data = placer.AddPlan(track, second.transform.position, false, second.GetComponent<SignGeneratorData>()!.signParameters);
        placer.ObservePlacementForTest(data, second);
        Check(ReadTrack(track).Count == 2 && UnityEngine.Object.FindCalls == searches,
            "Future optional generation after delayed activation reaches the actual deferred observer without scene discovery");
        for (var i = 0; i < 3; i++) { Time.realtimeSinceStartup += 6; NativeRouteSignIndex.Prune(); }
        Check(HarmonyLib.Harmony.PatchCalls == 1 && UnityEngine.Object.FindCalls == searches,
            "Stable optional availability does not repeat patch ownership checks, patching or scene scans");
        UnityModManagerNet.UnityModManager.DoubleTrack.Active = false;
        Time.realtimeSinceStartup += 6; NativeRouteSignIndex.Prune();
        UnityModManagerNet.UnityModManager.DoubleTrack.Active = true;
        Time.realtimeSinceStartup += 6; NativeRouteSignIndex.Prune();
        Check(HarmonyLib.Harmony.PatchCalls == 1,
            "Reactivation recognises the already attached exact observer and cannot add duplicate postfixes");

        Prepare();
        track = Track("Origin-relative late-enable candidates");
        var native = Placer(plans: true);
        var root = Root(new Vector3(80, 0, 0), parts);
        native.AddPlan(track, root.transform.position, false, parts);
        var other = Track("First census only");
        ReadTrack(other);
        searches = UnityEngine.Object.FindCalls;
        var move = new Vector3(1000, 0, 400);
        root.transform.position += move; WorldMover.currentMove = move;
        var matched = ReadTrack(track);
        Check(matched.Count == 1 && matched[0].Span == 80 && matched[0].Fact.Contains("Native recipe matched"),
            "Previously unassociated root remains an exact late-recipe candidate after a world-origin shift");
        Check(UnityEngine.Object.FindCalls == searches, "Origin-relative late-enable matching needs no replacement scene census");

        Prepare();
        track = Track("Ambiguity removed after census"); native = Placer(plans: true);
        var survivor = Root(new Vector3(50, 0, 0), parts);
        var removed = Root(new Vector3(50, 0, 0), parts);
        native.AddPlan(track, survivor.transform.position, false, parts);
        Check(ReadTrack(track).Count == 0, "Initial coincident roots reject ambiguous late recipes");
        removed.Destroyed = true;
        Time.realtimeSinceStartup += 6; FinishMaintenance(2);
        Check(ReadTrack(track).Count == 1,
            "Removing an unassociated ambiguous candidate retries the native recipe against the sole surviving root");

        Prepare();
        track = Track("Unassociated root moved after census"); native = Placer(plans: true);
        root = Root(new Vector3(50, 0, 0), parts);
        native.AddPlan(track, new Vector3(75, 0, 0), false, parts);
        Check(ReadTrack(track).Count == 0, "Root outside strict recipe position remains unassociated");
        searches = UnityEngine.Object.FindCalls;
        root.transform.position = new Vector3(75, 0, 0);
        Time.realtimeSinceStartup += 6; FinishMaintenance(1);
        Check(ReadTrack(track).Single().Span == 75 && UnityEngine.Object.FindCalls == searches,
            "Known root movement updates its relative spatial cell and retries strict recipes without scene discovery");

        Prepare();
        track = Track("In-place native accessory edits"); native = Placer();
        var accessories = new[] { new GameObject("Before") };
        parts = new[] { Part(SignType.SpeedLimit, "8", accessories: accessories) };
        root = Root(new Vector3(50, 0, 0), parts);
        Observe(native, track, root.transform.position, root);
        var before = ReadTrack(track);
        accessories[0] = new GameObject("After");
        Time.realtimeSinceStartup += 6; FinishMaintenance(1);
        var after = ReadTrack(track);
        Check(before.Count == 2 && after.Count == 2 && after.Any(entry => entry.Label == "Accessory · After"),
            "Content snapshots detect edits inside an existing accessory array instead of comparing the same mutable reference");
        Check(before.Select(entry => entry.SourceId).SequenceEqual(after.Select(entry => entry.SourceId)),
            "Native accessory edits preserve every part and accessory source identifier");
    }

    private static void DoubleTrackPlacementAndLateEnable()
    {
        Prepare();
        var patch = typeof(NativeRouteSignIndex).GetNestedType("ObserveDoubleTrackPlacement", BindingFlags.NonPublic)!;
        var prepare = patch.GetMethod("Prepare", BindingFlags.NonPublic | BindingFlags.Static)!;
        var target = patch.GetMethod("TargetMethod", BindingFlags.NonPublic | BindingFlags.Static)!;
        var postfix = patch.GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static)!;
        Check(!(bool)prepare.Invoke(null, null)!, "Absent optional DoubleTrack does not request a Harmony target");
        UnityModManagerNet.UnityModManager.DoubleTrack = new UnityModManagerNet.UnityModManager.ModEntry
        { Loaded = true, Active = false, Assembly = typeof(CustomSignPlacer).Assembly };
        Check(!(bool)prepare.Invoke(null, null)!, "Inactive optional DoubleTrack is not bound");
        UnityModManagerNet.UnityModManager.DoubleTrack.Active = true;
        Check((bool)prepare.Invoke(null, null)!, "Active DoubleTrack's separate native-shaped placement contract is validated");
        var method = (MethodBase)target.Invoke(null, null)!;
        Check(method.DeclaringType == typeof(CustomSignPlacer) && method.Name == "PlaceSign", "Observer targets DoubleTrack's actual private placer instead of base-game PlaceSign");

        var track = Track("DoubleTrack road");
        var parallel = Track("Parallel road");
        var placer = new GameObject("DoubleTrack placer").Add(new CustomSignPlacer());
        var parts = new[] { Part(SignType.SpeedLimit, "8"), Part(SignType.Grade, "-1.5") };
        var root = Root(new Vector3(100, 0, -2), parts);
        var data = placer.AddPlan(track, new Vector3(100, 0, 0), false, parts);
        postfix.Invoke(null, new[] { (object)placer, data, root });
        var entries = ReadTrack(track);
        Check(entries.Count == 2 && entries.Any(e => e.Kind == RouteEventKind.Speed && e.Label == "80 km/h")
            && entries.Any(e => e.Kind == RouteEventKind.Grade && e.Label == "-1.5 % falling"),
            "Actual DoubleTrack placement retains posted speed and signed grade parts");
        Check(entries.All(e => e.Forward && e.Span == 100) && ReadTrack(parallel).Count == 0,
            "DoubleTrack's exact recipe track and direction cannot leak to a parallel road");
        var calls = placer.RecipeCalls;
        root.transform.position += new Vector3(0, 0, -4);
        track.Regenerate();
        entries = ReadTrack(track);
        Check(entries.Count == 2 && entries.All(e => e.Span == 100),
            "Post-placement sign-clearance movement preserves the native track anchor rather than projecting the displaced pole");
        WorldMover.currentMove = new Vector3(1000, 0, 500);
        root.transform.position += WorldMover.currentMove;
        track.Regenerate();
        Check(ReadTrack(track).All(e => e.Span == 100), "DoubleTrack's retained anchor follows only world-origin changes");
        Check(placer.RecipeCalls == calls && placer.PlacementCalls == 0, "Already captured DoubleTrack roots need no recipe calls or gameplay mutation");

        Prepare();
        UnityModManagerNet.UnityModManager.DoubleTrack = new UnityModManagerNet.UnityModManager.ModEntry
        { Loaded = true, Active = true, Assembly = typeof(CustomSignPlacer).Assembly };
        track = Track("Late DoubleTrack road");
        placer = new GameObject("Late DoubleTrack placer").Add(new CustomSignPlacer { sideways = 2 });
        var prefab = Prefab("Native speed prefab");
        parts = new[] { Part(SignType.SpeedLimit, "6", prefab), Part(SignType.Grade, "+2", Prefab("Native grade prefab")) };
        Root(new Vector3(250, 0, 0), parts);
        placer.AddPlan(track, new Vector3(252, 0, 0), true, parts);
        entries = ReadTrack(track);
        Check(entries.Count == 2 && entries.All(e => e.Span == 252 && !e.Forward),
            "Late-enabled DoubleTrack uses its own read-only recipes and exact root signature to recover direction and track anchor");
        Check(entries.All(e => e.Fact.Contains("Native recipe matched to observed sign")) && placer.PlacementCalls == 0,
            "DoubleTrack late bootstrap reports validated evidence and never invokes sign creation");
        Check(placer.RecipeCalls == 2 && NativeRouteSignIndex.Coverage.Contains("1 validated plan matches"),
            "Both DoubleTrack recipe directions are enumerated once and coverage counts actual matches");
        calls = placer.RecipeCalls;
        Time.realtimeSinceStartup += 6;
        ReadTrack(track);
        Check(placer.RecipeCalls == calls, "Unchanged optional source skips periodic recipe reconstruction");

        var manyParts = Enumerable.Range(0, 150).Select(i => Part(i % 2 == 0 ? SignType.SpeedLimit : SignType.Grade,
            i % 2 == 0 ? "8" : "+1.5")).ToArray();
        var manyRoot = Root(new Vector3(500, 0, 0), manyParts);
        var manyData = placer.AddPlan(track, new Vector3(500, 0, 0), false, manyParts);
        postfix.Invoke(null, new[] { (object)placer, manyData, manyRoot });
        entries = ReadTrack(track);
        Check(entries.Count == 152 && entries.Select(e => e.SourceId).Distinct().Count() == 152,
            "DoubleTrack speed and grade parts retain every distinct observation beyond ordinary list limits");

        Prepare();
        UnityModManagerNet.UnityModManager.DoubleTrack = new UnityModManagerNet.UnityModManager.ModEntry
        { Loaded = true, Active = true, Assembly = typeof(CustomSignPlacer).Assembly };
        track = Track("Excluded DoubleTrack road");
        placer = new GameObject("Filtered DoubleTrack placer").Add(new CustomSignPlacer());
        placer.noSignsTrackNameMarks.Add("Excluded");
        Root(new Vector3(50, 0, 0), parts);
        placer.AddPlan(track, new Vector3(50, 0, 0), false, parts);
        Check(ReadTrack(track).Count == 0, "Optional placer exclusion configuration is honoured during read-only bootstrap");

        Prepare();
        UnityModManagerNet.UnityModManager.DoubleTrack = new UnityModManagerNet.UnityModManager.ModEntry
        { Loaded = true, Active = true, Assembly = typeof(CustomSignPlacer).Assembly };
        track = Track("Ambiguous DoubleTrack road");
        placer = new GameObject("Ambiguous DoubleTrack placer").Add(new CustomSignPlacer());
        Root(new Vector3(50, 0, 0), parts); Root(new Vector3(50, 0, 0), parts);
        placer.AddPlan(track, new Vector3(50, 0, 0), false, parts);
        Check(ReadTrack(track).Count == 0 && NativeRouteSignIndex.Diagnostics.Any(d => d.Contains("Ambiguous native sign recipe")),
            "Ambiguous optional sign roots remain explicit incomplete coverage rather than guessed events");

        Prepare();
        track = Track("Delayed optional source");
        placer = new GameObject("Delayed DoubleTrack placer").Add(new CustomSignPlacer());
        Root(new Vector3(50, 0, 0), parts);
        placer.AddPlan(track, new Vector3(50, 0, 0), false, parts);
        Check(ReadTrack(track).Count == 0, "Observed optional roots alone never establish a guessed track association");
        UnityModManagerNet.UnityModManager.DoubleTrack = new UnityModManagerNet.UnityModManager.ModEntry
        { Loaded = true, Active = true, Assembly = typeof(CustomSignPlacer).Assembly };
        Time.realtimeSinceStartup += 6;
        Check(ReadTrack(track).Count == 2,
            "A newly available exact optional placer retries bootstrap even when observed sign roots did not change");
    }

    private static void CapturedMembershipAndParts()
    {
        Prepare();
        var a = Track("A");
        var b = Track("B");
        var placer = Placer();
        var speedPrefab = Prefab("SpeedPrefab");
        var accessory = new GameObject("ExtraArrow");
        var parts = new[]
        {
            Part(SignType.SpeedLimit, "8", speedPrefab, new[] { accessory, null! }),
            Part(SignType.Grade, "-1.5", Prefab("GradePrefab"))
        };
        var root = Root(new Vector3(100, 0, 3), parts);
        Observe(placer, a, new Vector3(100, 0, 0), root);
        var entries = ReadTrack(a);
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
        Check(ReferenceEquals(entries, ReadTrack(a)) && a.Subscribers == 1, "Repeated reads reuse immutable cache without duplicate subscription");
        var aProjectionReads = a.ProjectionReads;
        Check(ReadTrack(b).Count == 0, "Parallel track never receives captured signs from another track");
        Check(a.ProjectionReads == aProjectionReads && b.ProjectionReads == 0, "Track membership index avoids projecting unrelated sources");
        var revision = NativeRouteSignIndex.SnapshotRevision;
        Observe(placer, b, new Vector3(100, 0, 0), root, flip: true);
        Check(NativeRouteSignIndex.SnapshotRevision > revision, "Native recapture advances revision");
        Check(ReadTrack(a).Count == 0, "Captured reassignment removes old track membership and cached observations");
        Check(ReadTrack(b).Count == 4 && ReadTrack(b).All(s => !s.Forward), "Captured reassignment uses new exact track and direction");
        WorldMover.currentMove = new Vector3(1000, 0, 500);
        root.transform.position += WorldMover.currentMove;
        b.Regenerate();
        Check(ReadTrack(b).All(s => s.Span == 100), "Anchor offset preserves exact identity across world shift");
        root.Destroyed = true;
        Time.realtimeSinceStartup += 6;
        revision = NativeRouteSignIndex.SnapshotRevision;
        NativeRouteSignIndex.Prune();
        Check(NativeRouteSignIndex.SnapshotRevision > revision && ReadTrack(b).Count == 0, "Prune removes destroyed root membership and invalidates observations");
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
        Check(ReadTrack(a).Count == 1 && ReadTrack(b).Count == 0,
            "Late recipe cannot override conflicting captured track identity");
        Register(root, b, true, parts);
        Check(ReadTrack(a).Count == 0 && ReadTrack(b).Count == 1,
            "New captured identity supersedes previous captured membership atomically");

        Prepare();
        a = Track("A"); b = Track("B"); Placer();
        root = Root(new Vector3(10, 0, 0), parts);
        Register(root, a, false, parts);
        Register(root, b, false, parts);
        Check(ReadTrack(a).Count == 0 && ReadTrack(b).Count == 0,
            "Conflicting uncaptured plans quarantine root instead of assigning nearby track");
        Check(NativeRouteSignIndex.Diagnostics.Any(p => p.Contains("Ambiguous native-plan association rejected")), "Conflicting plan identity is diagnosed");
        Check(NativeRouteSignIndex.Coverage.Contains("coverage partial"), "Ambiguous identity cannot claim complete native coverage");
        var empty = Root(new Vector3(20, 0, 0), Array.Empty<SignParameters>());
        Register(empty, a, true, Array.Empty<SignParameters>());
        var pole = ReadTrack(a).Single();
        Check(pole.SourceId.EndsWith(":pole") && pole.Label.Contains("no parameters retained"), "Native pole without parameters remains an explicit observation");
    }

    private static void RecipeSignatureAndCache()
    {
        Prepare();
        var track = Track("Late track");
        var placer = Placer(plans: true);
        var prefab = Prefab("Recipe prefab");
        var parts = new[] { Part(SignType.SpeedLimit, "8", prefab) };
        var root = Root(new Vector3(50, 0, 0), parts);
        placer.AddPlan(track, new Vector3(50, 0, 0), false, parts);
        var entries = ReadTrack(track);
        Check(entries.Count == 1 && entries[0].Label == "80 km/h", "Actual native recipe validates observed root before late association");
        Check(entries[0].Fact.Contains("Native recipe matched to observed sign"), "Late recipe evidence differs from captured placement evidence");
        Check(NativeRouteSignIndex.Coverage.Contains("1 validated plan matches") && NativeRouteSignIndex.Coverage.Contains("0 unmatched plans"), "Matched plan coverage is reported from actual bootstrap");
        var calls = placer.RecipeCalls;
        Time.realtimeSinceStartup += 6;
        Check(ReadTrack(track).Count == 1 && placer.RecipeCalls == calls, "Unchanged cached signature skips repeating native recipe bootstrap after prune");
        parts[0] = Part(SignType.SpeedLimit, "9", prefab);
        Time.realtimeSinceStartup += 6;
        var refreshed = ReadTrack(track);
        Check(refreshed.Count == 1 && refreshed[0].Label == "90 km/h", "Changed raw native sign content refreshes published observations");
        Check(refreshed[0].SourceId == entries[0].SourceId, "Content edits preserve exact retained source identity");
        Check(placer.RecipeCalls == calls, "Changed content keeps already established exact root association without redundant recipe enumeration");
        var extraParts = new[] { Part(SignType.WarningBoard, "Late root", prefab) };
        var extraRoot = Root(new Vector3(75, 0, 0), extraParts);
        placer.AddPlan(track, new Vector3(75, 0, 0), false, extraParts);
        Observe(placer, track, new Vector3(75, 0, 0), extraRoot);
        Time.realtimeSinceStartup += 6;
        refreshed = ReadTrack(track);
        Check(refreshed.Count == 2 && refreshed.Any(s => s.SourceId.StartsWith("native-sign:" + extraRoot.GetInstanceID() + ":")),
            "Native placement observer retains newly generated root after initial late-enable discovery");
        Check(placer.RecipeCalls == calls, "Exact late native placement needs no repeated recipe or scene census");
        var revision = NativeRouteSignIndex.SnapshotRevision;
        calls = placer.RecipeCalls;
        track.Regenerate();
        Check(NativeRouteSignIndex.SnapshotRevision > revision, "Native geometry event advances sign snapshot revision");
        Check(!ReferenceEquals(refreshed, ReadTrack(track)) && placer.RecipeCalls == calls,
            "Geometry invalidation refreshes track cache while established root associations avoid redundant native recipes");
        NativeRouteSignIndex.Reset();
        Check(track.Subscribers == 0, "Late-enable index reset releases geometry subscription");
    }

    private static void RejectedRecipeAndProjection()
    {
        Prepare();
        var track = Track("Recipe track");
        var placer = Placer(plans: true);
        var firstPrefab = Prefab("First prefab");
        var secondPrefab = Prefab("Second prefab");
        var parts = new[] { Part(SignType.SpeedLimit, "8", firstPrefab) };
        Root(new Vector3(50, 0, 0), parts);
        placer.AddPlan(track, new Vector3(50, 0, 0), false, new[] { Part(SignType.SpeedLimit, "8", secondPrefab) });
        Check(ReadTrack(track).Count == 0, "Matching visible text cannot override different native prefab signature");
        Check(NativeRouteSignIndex.Diagnostics.Any(p => p.Contains("Unmatched native sign recipe")), "Signature mismatch records raw uncertainty instead of inventing event");

        Prepare();
        track = Track("Accessory identity"); placer = Placer(plans: true);
        parts = new[] { Part(SignType.SpeedLimit, "8", firstPrefab, new[] { new GameObject("Observed accessory") }) };
        Root(new Vector3(50, 0, 0), parts);
        placer.AddPlan(track, new Vector3(50, 0, 0), false,
            new[] { Part(SignType.SpeedLimit, "8", firstPrefab, new[] { new GameObject("Different recipe accessory") }) });
        Check(ReadTrack(track).Count == 0, "Cached native signature includes accessory identity as well as raw text and primary prefab");

        Prepare();
        track = Track("Text-only observed root"); placer = Placer(plans: true);
        var textRoot = new GameObject("Debug-only sign");
        textRoot.transform.position = new Vector3(50, 0, 0);
        textRoot.Add(new SignDebug { text = "8" });
        placer.AddPlan(track, new Vector3(50, 0, 0), false, new[] { Part(SignType.SpeedLimit, "8", firstPrefab) });
        var textOnly = ReadTrack(track);
        Check(textOnly.Count == 1 && textOnly[0].Label == "80 km/h", "Observed debug-only root can validate unique native recipe text without generated parameter component");
        Check(textOnly[0].Fact.Contains("raw text: 8") && textOnly[0].Fact.Contains("First prefab"), "Debug-only fallback retains recipe parameter evidence after association");

        Prepare();
        track = Track("Ambiguous scene"); placer = Placer(plans: true);
        parts = new[] { Part(SignType.WarningBoard, "8", firstPrefab) };
        Root(new Vector3(50, 0, 0), parts); Root(new Vector3(50, 0, 0), parts);
        placer.AddPlan(track, new Vector3(50, 0, 0), false, parts);
        Check(ReadTrack(track).Count == 0, "Two matching observed roots cannot establish unique recipe association");
        Check(NativeRouteSignIndex.Diagnostics.Any(p => p.Contains("Ambiguous native sign recipe")), "Multiple observed candidates remain explicit ambiguity");

        Prepare();
        track = Track("Known identity"); placer = Placer();
        var root = Root(new Vector3(50, 0, 0), parts);
        Observe(placer, track, root.transform.position, root);
        track.ProjectionAvailable = false;
        Check(ReadTrack(track).Count == 0, "Unavailable known-track projection is never assigned to guessed distance");
        Check(NativeRouteSignIndex.Diagnostics.Any(p => p.Contains("Known-track sign projection unavailable")), "Unavailable known-track projection records explicit source diagnostic");
        track.ProjectionAvailable = true;
        track.Regenerate();
        Check(ReadTrack(track).Count == 1, "Known source identity survives unavailable projection and native retry");
        track.Destroyed = true;
        Time.realtimeSinceStartup += 6;
        NativeRouteSignIndex.Prune();
        Check(ReadTrack(track).Count == 0, "Destroyed native track never yields retained sign observations");
    }
}
