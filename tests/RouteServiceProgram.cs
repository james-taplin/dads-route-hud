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
        OptionalSignalTelemetry.Reset();
        Frame(car, 23, 46);
        Check(!RouteMonitorService.Current.Events.Any(e => e.Kind == RouteEventKind.Signal), "Replacement source cannot inherit the old source's signals");
        RouteMonitorService.Reset();
        Console.WriteLine(checks + " route-service lifecycle checks passed (native/optional test doubles; not game acceptance).");
    }
}
