using System;
using System.Collections.Generic;
using System.Linq;
using DV.Utils;
using DvMod.HeadsUpDisplay;
using Signals.Game;
using UnityEngine;
using UnityModManagerNet;
using HudMain = DvMod.HeadsUpDisplay.Main;

internal static class OptionalSignalsProgram
{
    private static int checks;
    private static float time = 1;
    private static readonly TrainCar car = new TrainCar();
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; }
    private static RouteInterval<RailTrack>[] Route(RailTrack track) => new[] { new RouteInterval<RailTrack>(track, true, 0, 1000, 0, 1000) };
    private static void Frame(IReadOnlyList<RouteInterval<RailTrack>> route, long revision, TrainCar? selected = null)
    { Time.realtimeSinceStartup = time += 0.1f; OptionalSignalTelemetry.Update(selected ?? car, route, revision); }
    private static void Complete(IReadOnlyList<RouteInterval<RailTrack>> route, long revision)
    {
        for (var i = 0; i < 100; i++)
        {
            Frame(route, revision);
            if (!OptionalSignalTelemetry.IsReading) return;
        }
        throw new Exception("Optional read did not complete within 100 test frames");
    }
    private static Signal Signal(string id) => new Signal { Id = id, Name = id };
    private static Controller Controller(RailTrack? track, string id, double span = 250, double direction = 1)
        => new Controller { Id = id, PlacementInfo = new Placement { Track = track, Span = span, Direction = direction }, AllSignals = new[] { Signal(id) } };
    private static void Prepare()
    {
        OptionalSignalTelemetry.Reset();
        UnityModManager.Mods.Clear();
        SingletonBehaviour<SignalManager>.SetExisting(null);
        AITraffic.Core.TrafficManager.SetExisting(null);
        AITraffic.Navigation.RailGraph.SetExisting(null);
        AITraffic.Navigation.SignalRegistry.FailEnrichment = false;
        HudMain.enabled = HudMain.settings.trackInfoSettings.enabled = true;
    }
    private static void Activate(SignalManager? manager, bool traffic = false)
    {
        UnityModManager.Mods["DVSignals"] = new UnityModManager.ModEntry();
        if (traffic) UnityModManager.Mods["AITraffic"] = new UnityModManager.ModEntry();
        SingletonBehaviour<SignalManager>.SetExisting(manager);
    }
    private static void Main()
    {
        AbsentSources();
        RetentionAndCoverage();
        SourceLifetimeAndCancellation();
        TrafficEnrichment();
        OptionalSignalTelemetry.Reset();
        Check(SingletonBehaviour<SignalManager>.InstanceGetterCalls == 0, "Adapter never invokes auto-creating signal singleton getter");
        Console.WriteLine(checks + " actual optional-signal adapter checks passed (reflection/native boundary doubles; not game acceptance).");
    }

    private static void AbsentSources()
    {
        Prepare();
        var route = Route(new RailTrack());
        var constructed = SignalManager.Constructions;
        Complete(route, 1);
        Check(!OptionalSignalTelemetry.IsAvailable, "Absent DVSignals stays optional");
        Check(OptionalSignalTelemetry.UpcomingSignals.Count == 0, "Absent source emits no invented signal facts");
        Check(SignalManager.Constructions == constructed, "Absent source creates no manager");
        Activate(null);
        Complete(route, 1);
        Check(OptionalSignalTelemetry.ActiveSignalSource == null, "Active assembly without existing instance has no source identity");
        Check(OptionalSignalTelemetry.Diagnostics.Any(p => p.Contains("manager unavailable")), "Missing existing manager is explicit partial coverage");
        Check(!OptionalSignalTelemetry.IsComplete && !OptionalSignalTelemetry.IsReading, "Unavailable manager finishes with partial status");
        Check(SignalManager.Constructions == constructed, "Existing-instance lookup never creates missing manager");
    }

    private static void RetentionAndCoverage()
    {
        Prepare();
        var track = new RailTrack { name = "Route", Occupied = true };
        var outside = new RailTrack { name = "Other track" };
        var main = Signal("main");
        var distant = Signal("distant");
        main.DistantSignal = distant;
        distant.DistantSignal = main; // A corrupt cycle must terminate without losing either head.
        main.Block = new Block { AllTracks = new[] { track } };
        var controller = Controller(track, "main", 250);
        controller.AllSignals = new[] { main, main };
        controller.CameraDistanceSqr = 3000000;
        var opposing = Controller(track, "opposing", 500, 0);
        var horizon = Controller(track, "horizon", 1000);
        var manager = new SignalManager { AllControllers = new[] { controller, controller, opposing, horizon, Controller(outside, "outside"), Controller(track, "beyond", 1001) } };
        Activate(manager);
        var route = Route(track);
        Complete(route, 10);
        var facts = OptionalSignalTelemetry.UpcomingSignals;
        Check(facts.Count == 4, "Every distinct main/distant/opposing/horizon head retained without duplicate controller entries");
        Check(facts.Any(s => s.SourceId == "dvsignals:signal:distant"), "Embedded distant head is discovered outside AllSignals list");
        Check(facts.Any(s => s.Name == "opposing" && !s.FacingTravel), "Opposing head remains an observation with facing metadata");
        Check(facts.Any(s => s.Name == "horizon" && s.span == 1000), "Exact configured horizon is inclusive");
        Check(!facts.Any(s => s.Name == "outside" || s.Name == "beyond"), "Exact native association excludes other tracks and beyond horizon");
        Check(facts.First().Freshness.Contains("Distant aspect may be stale"), "Distant observed aspects state unverified freshness");
        Check(facts.Single(s => s.Name == "main").Reservation == "Block occupied", "Signal block occupancy comes from native track observation");
        Check(OptionalSignalTelemetry.GetTrackReservation(track) == "Occupied", "Per-track reservation facts publish with pass");
        Check(OptionalSignalTelemetry.IsComplete && !OptionalSignalTelemetry.IsReading, "Complete native catalogue publishes atomically");
        Check(ReferenceEquals(OptionalSignalTelemetry.ActiveSignalSource, manager), "Source identity is actual existing native manager");
        Check(facts.All(s => s.ColorHex == "#00FF00"), "No AI Traffic still supports observed native lamp colour");
        var reverseRoute = new[] { new RouteInterval<RailTrack>(track, false, 1000, 0, 0, 1000) };
        Complete(reverseRoute, 11);
        facts = OptionalSignalTelemetry.UpcomingSignals;
        Check(facts.Single(s => s.Name == "opposing").FacingTravel, "Native opposing direction becomes facing for reverse route");
        Check(facts.Single(s => s.Name == "main").span == 750 && !facts.Single(s => s.Name == "main").FacingTravel,
            "Reverse route projects exact span and flips facing metadata");
        manager.AllControllers = new[] { controller, Controller(null, "unassociated"), Controller(track, "invalid direction", 50, 3) };
        Complete(route, 12);
        Check(OptionalSignalTelemetry.UpcomingSignals.Count == 2, "Partial catalogue preserves all successfully observed heads");
        Check(!OptionalSignalTelemetry.IsComplete && !OptionalSignalTelemetry.IsReading, "Completed partial pass is not confused with active continuation");
        Check(OptionalSignalTelemetry.Diagnostics.Any(p => p.Contains("lack an exact native track placement")), "Unassociated controllers state uncertain coverage");
        Check(OptionalSignalTelemetry.Diagnostics.Any(p => p.Contains("Invalid placement")), "Invalid native direction is diagnosed instead of guessed");
        manager.AllControllers = Enumerable.Range(0, 80).Select(i => Controller(track, "head:" + i, i * 10)).ToArray();
        Complete(route, 13);
        Check(OptionalSignalTelemetry.UpcomingSignals.Count == 80 && OptionalSignalTelemetry.IsComplete,
            "More than 64 associated signals are retained through the actual adapter");
        OptionalSignalTelemetry.Reset();
        Check(OptionalSignalTelemetry.UpcomingSignals.Count == 0 && OptionalSignalTelemetry.GetTrackReservation(track) == "", "Reset retires signal and transferred reservation collections");
    }

    private static void SourceLifetimeAndCancellation()
    {
        Prepare();
        var track = new RailTrack();
        var route = Route(track);
        var slow = new SlowControllers(Controller(track, "old manager"));
        var oldManager = new SignalManager { AllControllers = slow };
        Activate(oldManager);
        Frame(route, 20);
        Check(OptionalSignalTelemetry.IsReading, "Native-boundary delay creates a real multi-Update adapter continuation");
        var replacement = new SignalManager { AllControllers = new[] { Controller(track, "new manager") } };
        SingletonBehaviour<SignalManager>.SetExisting(replacement);
        Complete(route, 20);
        Check(slow.Disposed, "Manager replacement disposes old active native enumeration");
        Check(ReferenceEquals(OptionalSignalTelemetry.ActiveSignalSource, replacement), "Same assembly manager replacement changes actual source identity");
        Check(OptionalSignalTelemetry.UpcomingSignals.Count == 1 && OptionalSignalTelemetry.UpcomingSignals[0].Name == "new manager",
            "Replacement source cannot publish old manager observations");
        var refreshSlow = new SlowControllers(Controller(track, "updated same source"));
        replacement.AllControllers = refreshSlow;
        time += 1; // Make the normal half-second sample interval due.
        Frame(route, 20);
        Check(OptionalSignalTelemetry.IsReading && OptionalSignalTelemetry.UpcomingSignals.Count == 1
            && OptionalSignalTelemetry.UpcomingSignals[0].Name == "new manager",
            "Same-source periodic continuation retains its last completed sample until publication");
        Complete(route, 20);
        Check(refreshSlow.Disposed && OptionalSignalTelemetry.UpcomingSignals.Count == 1
            && OptionalSignalTelemetry.UpcomingSignals[0].Name == "updated same source",
            "Same-source periodic replacement publishes and disposes completed enumeration atomically");
        replacement.Destroyed = true;
        Complete(route, 20);
        Check(OptionalSignalTelemetry.ActiveSignalSource == null && OptionalSignalTelemetry.UpcomingSignals.Count == 0,
            "Unity destroyed existing manager retires source and facts");
        Check(OptionalSignalTelemetry.Diagnostics.Any(p => p.Contains("manager unavailable")), "Destroyed manager reports missing native source");

        var otherTrack = new RailTrack();
        var revisionSlow = new SlowControllers(Controller(track, "old route"));
        replacement = new SignalManager { AllControllers = revisionSlow };
        SingletonBehaviour<SignalManager>.SetExisting(replacement);
        Frame(route, 21);
        Check(OptionalSignalTelemetry.IsReading, "Revision test starts a real active pass");
        replacement.AllControllers = new[] { Controller(otherTrack, "new route") };
        Complete(Route(otherTrack), 22);
        Check(revisionSlow.Disposed, "Route revision change disposes old active enumeration");
        Check(OptionalSignalTelemetry.UpcomingSignals.Count == 1 && OptionalSignalTelemetry.UpcomingSignals[0].Name == "new route",
            "Changed interval revision publishes only newly associated facts");
        UnityModManager.Mods["DVSignals"].Active = false;
        Complete(Route(otherTrack), 22);
        Check(!OptionalSignalTelemetry.IsAvailable && OptionalSignalTelemetry.UpcomingSignals.Count == 0,
            "Disabled optional mod retires bound source and observations");
    }

    private static void TrafficEnrichment()
    {
        Prepare();
        var track = new RailTrack { Reserved = true, ReservationToken = true };
        var head = Signal("native name");
        head.Block = new Block { AllTracks = new[] { track } };
        var controller = Controller(track, "controller");
        controller.AllSignals = new[] { head };
        var manager = new SignalManager { AllControllers = new[] { controller } };
        Activate(manager, true);
        var getters = AITraffic.Core.TrafficManager.InstanceGetterCalls;
        var helpers = AITraffic.Navigation.SignalRegistry.ReservationHelperCalls;
        var route = Route(track);
        Complete(route, 30);
        Check(OptionalSignalTelemetry.UpcomingSignals[0].Name == "Enriched native name", "Active AI Traffic enriches names without owning discovery");
        Check(OptionalSignalTelemetry.UpcomingSignals[0].Aspect == "Enriched aspect", "Active AI Traffic enriches observed aspect labels");
        Check(OptionalSignalTelemetry.GetTrackReservation(track) == "Reserved; owner unknown", "No existing traffic manager leaves reservation ownership unknown");
        Check(AITraffic.Core.TrafficManager.InstanceGetterCalls == getters && AITraffic.Navigation.SignalRegistry.ReservationHelperCalls == helpers,
            "Reservation helper that auto-creates TrafficManager is skipped when no existing instance exists");
        AITraffic.Core.TrafficManager.SetExisting(new AITraffic.Core.TrafficManager());
        Complete(route, 30);
        Check(OptionalSignalTelemetry.GetTrackReservation(track) == "AI reserved", "Existing traffic manager permits positively identified AI reservation");
        var sourceIdentity = OptionalSignalTelemetry.ActiveTrafficSource;
        var graph = new AITraffic.Navigation.RailGraph();
        graph.Owners[track] = new object();
        AITraffic.Navigation.RailGraph.SetExisting(graph);
        Complete(route, 30);
        Check(!ReferenceEquals(sourceIdentity, OptionalSignalTelemetry.ActiveTrafficSource), "Replacing graph reservation source changes traffic source identity");
        Check(OptionalSignalTelemetry.GetTrackReservation(track) == "Reserved; owner unknown", "Unknown graph owner stays unknown");
        graph.Owners[track] = new AITraffic.Driver.AIEngineer();
        Complete(route, 31);
        Check(OptionalSignalTelemetry.GetTrackReservation(track) == "AI reserved", "Native graph AIEngineer holder is positive AI ownership");
        AITraffic.Navigation.SignalRegistry.FailEnrichment = true;
        Complete(route, 32);
        Check(OptionalSignalTelemetry.UpcomingSignals.Count == 1 && OptionalSignalTelemetry.UpcomingSignals[0].Name == "native name",
            "Optional enrichment failure retains discovered native head and native name");
        Check(OptionalSignalTelemetry.UpcomingSignals[0].Aspect == "Green", "Optional aspect-label failure falls back to observed native aspect");
        UnityModManager.Mods["AITraffic"].Active = false;
        Complete(route, 32);
        Check(OptionalSignalTelemetry.UpcomingSignals.Count == 1 && OptionalSignalTelemetry.UpcomingSignals[0].Name == "native name",
            "Disabling AI Traffic keeps native DVSignals discovery working");
        Check(OptionalSignalTelemetry.ActiveTrafficSource == null, "Disabled traffic source loses traffic identity");
    }
}
