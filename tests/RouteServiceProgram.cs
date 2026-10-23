using System;
using System.Linq;
using DvMod.HeadsUpDisplay;
using UnityEngine;

internal static class RouteServiceProgram
{
    private static int checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; }

    private static void Frame(TrainCar car, float time, double span)
    {
        Time.realtimeSinceStartup = time;
        foreach (var bogie in car.Bogies) bogie.traveller.Span = span;
        RouteMonitorService.Update(car);
    }

    private static void Main()
    {
        var track = new RailTrack();
        var car = new TrainCar();
        foreach (var bogie in car.Bogies) bogie.track = track;
        RouteMonitorService.Reset();
        OptionalSignalTelemetry.Reset();
        // The production collector has a real 2 ms frame budget. Cold JIT can split
        // initial collection across extra frames; settle setup without advancing
        // simulated time or changing the lifecycle timestamps/assertions below.
        for (var setupFrame = 0; setupFrame < 32
            && (OptionalSignalTelemetry.UpcomingSignals.Count == 0 || OptionalSignalTelemetry.IsReading); setupFrame++)
            Frame(car, 1, 10);
        if (OptionalSignalTelemetry.UpcomingSignals.Count == 0 || OptionalSignalTelemetry.IsReading)
            throw new Exception("Initial asynchronous capture did not settle within 32 setup updates");
        for (var i = 0; i < 4; i++) Frame(car, 1 + i * 0.3f, 10);
        Check(RouteMonitorService.Current.Events.Any(e => e.Kind == RouteEventKind.Signal), "Initial signal sample is ready");
        var resets = OptionalSignalTelemetry.ResetCount;
        // Finish a replacement after travelling 10 metres on the old capture. The new origin is 20,
        // and must not inherit the old capture's 10-metre offset on its completion frame.
        Frame(car, 3.2f, 20);
        Check(RouteMonitorService.Current.Events.Any(e => e.Kind == RouteEventKind.Signal), "Same-route reading retains signal rows");
        Frame(car, 3.5f, 20.1);
        Check(OptionalSignalTelemetry.ResetCount == resets, "New capture uses its own origin; motion must not reset optional telemetry");
        Frame(car, 3.8f, 20.2);
        Check(RouteMonitorService.Current.Events.Any(e => e.Kind == RouteEventKind.Signal), "Replacement publishes signals atomically");
        Frame(car, 4.1f, 20.3);
        Frame(car, 4.4f, 20.4);
        Check(RouteMonitorService.Current.Diagnostics.Any(d => d.StartsWith("Capture 2 since loading")), "One scheduled replacement, no spurious left-route rebuild");
        for (var i = 0; i < 25; i++)
        {
            Frame(car, 4.7f + i * 0.3f, 20.5 + i);
            Check(RouteMonitorService.Current.Events.Any(e => e.Kind == RouteEventKind.Signal), "Repeated moving refresh must not expose native-only data");
        }
        Check(OptionalSignalTelemetry.ResetCount == resets, "Repeated moving refresh does not spuriously invalidate the route");
        OptionalSignalTelemetry.ActiveSignalSource = null;
        Frame(car, 12.5f, 46);
        Check(!RouteMonitorService.Current.Events.Any(e => e.Kind == RouteEventKind.Signal), "Disabled source clears previous signal facts");
        OptionalSignalTelemetry.ActiveSignalSource = new object();
        for (var i = 0; i < 4; i++) Frame(car, 12.8f + i * 0.3f, 46);
        Check(RouteMonitorService.Current.Events.Any(e => e.Kind == RouteEventKind.Signal), "Re-enabled source supplies a new sample");
        car.SimController.controlsOverrider.Reverser.Value = 0;
        Frame(car, 14, 46);
        Check(!RouteMonitorService.Current.Events.Any(e => e.Kind == RouteEventKind.Signal), "Reversal cannot retain the old direction's signals");
        RouteMonitorService.Reset();
        OptionalSignalTelemetry.Reset();
        car.SimController.controlsOverrider.Reverser.Value = 1;
        car.Speed = 0;
        for (var i = 0; i < 4; i++) Frame(car, 15 + i * 0.3f, 46);
        for (var i = 0; i < 20; i++)
        {
            Frame(car, 16.2f + i * 0.3f, 46);
            Check(RouteMonitorService.Current.Events.Any(e => e.Kind == RouteEventKind.Signal), "Stationary refresh retains signals even with partial coverage diagnostics");
        }
        // A source replacement during a read must not borrow the old source's facts.
        OptionalSignalTelemetry.ActiveSignalSource = new object();
        Frame(car, 23, 46);
        Check(!RouteMonitorService.Current.Events.Any(e => e.Kind == RouteEventKind.Signal), "Replacement source cannot inherit the old source's signals");
        RouteMonitorService.Reset();
        CheckTopologyInvalidation();
        var lifecycleChecks = checks;
        CheckHudTimings();
        Console.WriteLine(lifecycleChecks + " route-service lifecycle checks and " + (checks - lifecycleChecks)
            + " HUD timing checks passed (native/optional test doubles; not game acceptance).");
    }

    private static bool HasTimingCalls(string? report, HudTimingStage stage, int calls)
        => report != null && report.Split(';').Any(part => part.Contains(stage + " max=")
            && part.Contains(" calls=" + calls + " "));

    private static void CheckHudTimings()
    {
        // Use the real monotonic clock without sleeping; durations are deliberately
        // unspecified, while report boundaries and call counts are deterministic.
        HudTimings.Enabled = false;
        HudTimings.Reset();
        using (HudTimings.Measure(HudTimingStage.RouteUpdate)) { }
        Check(HudTimings.TakeReport(100) == null, "Disabled logging does not record or produce a timing report");

        HudTimings.Enabled = true;
        using (HudTimings.Measure(HudTimingStage.RouteUpdate))
            using (HudTimings.Measure(HudTimingStage.RouteCollectStep)) { }
        using (HudTimings.Measure(HudTimingStage.RouteCollectStep)) { }
        var report = HudTimings.TakeReport(100);
        Check(report != null && report.Contains("inclusive; do not add"), "Nested timing reports explain their inclusive boundaries");
        Check(HasTimingCalls(report, HudTimingStage.RouteUpdate, 1), "One parent call is aggregated independently of nested scopes");
        Check(HasTimingCalls(report, HudTimingStage.RouteCollectStep, 2), "Repeated child calls are aggregated into one stage row");

        using (HudTimings.Measure(HudTimingStage.RouteUpdate)) { }
        Check(HudTimings.TakeReport(104.999) == null, "Reports are throttled throughout the five-second interval");
        report = HudTimings.TakeReport(105);
        Check(HasTimingCalls(report, HudTimingStage.RouteUpdate, 1), "The next report contains calls retained during the throttle interval");
        Check(report != null && !report.Contains(HudTimingStage.RouteCollectStep + " max="), "Already reported calls do not leak into the next aggregate");
        Check(HudTimings.TakeReport(105) == null, "Repeated report requests at the same timestamp remain throttled");
        Check(HudTimings.TakeReport(110) == null, "An empty timing interval produces no log message");

        try
        {
            using (HudTimings.Measure(HudTimingStage.SignBootstrap))
                throw new InvalidOperationException("Expected timing scope failure");
        }
        catch (InvalidOperationException) { }
        Check(HasTimingCalls(HudTimings.TakeReport(115), HudTimingStage.SignBootstrap, 1),
            "A failing native operation is still timed when its scope unwinds");

        using (HudTimings.Measure(HudTimingStage.SignMaintenance)) { }
        HudTimings.Enabled = false;
        Check(HudTimings.TakeReport(116) == null, "Disabling logging discards a pending aggregate");
        HudTimings.Enabled = true;
        using (HudTimings.Measure(HudTimingStage.RouteUpdate)) { }
        report = HudTimings.TakeReport(116);
        Check(HasTimingCalls(report, HudTimingStage.RouteUpdate, 1), "Re-enabled logging starts a fresh reporting interval");
        Check(report != null && !report.Contains(HudTimingStage.SignMaintenance + " max="), "Disabled-period cleanup retires previous samples");

        var disabledScope = HudTimings.Measure(HudTimingStage.SignProjection);
        HudTimings.Enabled = false;
        disabledScope.Dispose();
        HudTimings.Enabled = true;
        Check(HudTimings.TakeReport(122) == null, "An in-flight scope cannot record after logging was disabled");

        var interruptedScope = HudTimings.Measure(HudTimingStage.SignProjection);
        HudTimings.Enabled = false;
        HudTimings.Enabled = true;
        interruptedScope.Dispose();
        Check(HudTimings.TakeReport(128) == null, "An off/on transition cannot resurrect an earlier in-flight scope");

        var previousWorldScope = HudTimings.Measure(HudTimingStage.SignProjection);
        HudTimings.Reset();
        previousWorldScope.Dispose();
        Check(HudTimings.TakeReport(129) == null, "Reset invalidates scopes from the previous world");
        using (HudTimings.Measure(HudTimingStage.RouteUpdate)) { }
        report = HudTimings.TakeReport(134);
        Check(HasTimingCalls(report, HudTimingStage.RouteUpdate, 1)
            && report != null && !report.Contains(HudTimingStage.SignProjection + " max="),
            "The new world report contains only new measurements");

        HudTimings.Reset();
        using (HudTimings.Measure(HudTimingStage.RouteUpdate)) { }
        Check(HudTimings.TakeReport(double.NaN) == null, "NaN timestamps cannot publish timing reports");
        Check(HudTimings.TakeReport(double.PositiveInfinity) == null, "Positive infinity cannot advance the report deadline");
        Check(HudTimings.TakeReport(double.NegativeInfinity) == null, "Negative infinity cannot advance the report deadline");
        Check(HasTimingCalls(HudTimings.TakeReport(0), HudTimingStage.RouteUpdate, 1), "Invalid timestamps preserve pending measurements for the next valid boundary");
        using (HudTimings.Measure(HudTimingStage.RouteUpdate)) { }
        Check(HasTimingCalls(HudTimings.TakeReport(-1), HudTimingStage.RouteUpdate, 1), "A clock rewind starts a new interval instead of suppressing reports indefinitely");
        HudTimings.Enabled = false;
        HudTimings.Reset();
    }

    private static void CheckTopologyInvalidation()
    {
        var oldBranch = new RailTrack { name = "Old branch" };
        var newBranch = new RailTrack { name = "New branch" };
        var start = new RailTrack { name = "Approach", Length = 200, Next = oldBranch };
        var car = new TrainCar();
        foreach (var bogie in car.Bogies) bogie.track = start;
        for (var i = 0; i < 4; i++) Frame(car, 30 + i * 0.3f, 10);
        Check(RouteMonitorService.Current.Events.Any(e => e.Label == "Old branch"), "Initial native selected branch is captured");
        var resets = OptionalSignalTelemetry.ResetCount;
        start.Next = newBranch;
        Frame(car, 31.3f, 10);
        Check(!RouteMonitorService.Current.Events.Any(e => e.Label == "Old branch"), "Changed topology retires the old route even while its anchor still matches");
        Check(!RouteMonitorService.Current.Events.Any(e => e.Kind == RouteEventKind.Signal), "Changed topology cannot borrow old signal facts");
        Check(OptionalSignalTelemetry.ResetCount > resets, "A genuine path change resets optional observations");
        for (var i = 0; i < 4; i++) Frame(car, 31.6f + i * 0.3f, 10);
        Check(RouteMonitorService.Current.Events.Any(e => e.Label == "New branch"), "Replacement route follows the newly selected native branch");

        // Change the native edge after the incremental walk has already read it, from
        // inside the native sign boundary. A completed stale capture must be rejected.
        NativeRouteSignIndex.OnEnsureTrack = track =>
        {
            if (track != start) return;
            start.Next = oldBranch;
            NativeRouteSignIndex.OnEnsureTrack = null;
        };
        resets = OptionalSignalTelemetry.ResetCount;
        Frame(car, 34, 10);
        Check(!RouteMonitorService.Current.Events.Any(e => e.Label == "New branch"), "Topology change during collection rejects the completed obsolete capture");
        Check(!RouteMonitorService.Current.Events.Any(e => e.Kind == RouteEventKind.Signal), "Rejected capture cannot keep previous route signals visible");
        Check(OptionalSignalTelemetry.ResetCount > resets, "Topology change during collection resets optional observations");
        for (var i = 0; i < 5; i++) Frame(car, 34.3f + i * 0.3f, 10);
        Check(RouteMonitorService.Current.Events.Any(e => e.Label == "Old branch"), "Rejected capture retries the actual new path");

        var previousRevision = RouteMonitorService.Current.Revision;
        NativeRouteSignIndex.OnEnsureTrack = track =>
        {
            NativeRouteSignIndex.OnEnsureTrack = null;
            track.Regenerate();
        };
        Frame(car, 37, 10);
        Check(RouteMonitorService.Current.Revision == previousRevision, "Geometry regenerated during collection cannot publish a mixed new capture");
        Check(RouteMonitorService.Current.Events.Any(e => e.Kind == RouteEventKind.Signal), "Soft geometry refresh retains a usable previous same-route sample");
        for (var i = 0; i < 5; i++) Frame(car, 37.3f + i * 0.3f, 10);
        Check(RouteMonitorService.Current.Revision > previousRevision, "Geometry invalidation retries and publishes a fresh capture");
        RouteMonitorService.Reset();
    }
}
