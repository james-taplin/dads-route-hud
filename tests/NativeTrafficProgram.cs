using System;
using System.Linq;
using DvMod.HeadsUpDisplay;
using UnityEngine;
internal static class NativeTrafficProgram
{
    private static int checks;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    private static TrainCar Car(RailTrack track, double span, string id = "car") => new TrainCar { ID = id,
        Bogies = new[] { new Bogie { track = track, traveller = new Traveller { Span = span } } } };
    private static RouteSnapshot<RailTrack> Route(RailTrack track, bool reverse = false) => new RouteSnapshot<RailTrack>(1, 1000, 1000,
        RouteWalkTermination.HorizonReached, new[] { new RouteInterval<RailTrack>(track, !reverse, reverse ? 1000 : 0, reverse ? 0 : 1000, 0, 1000) },
        Array.Empty<RouteEvent>(), Array.Empty<RouteProfilePoint>(), false, "", "");
    private static void Settle(TrainCar player, RouteSnapshot<RailTrack> route)
    { for (var i = 0; i < 100; i++) NativeRouteTraffic.Update(player, route); }
    public static void Main()
    {
        NativeRouteTraffic.Reset(); Time.realtimeSinceStartup = 1;
        var main = new RailTrack(); var parallel = new RailTrack(); var own = new Trainset();
        var player = Car(main, 10, "player"); player.trainset = own; player.IsLoco = true; player.Speed = -10;
        var ownTail = Car(parallel, 0, "own tail"); ownTail.trainset = own;
        own.cars.AddRange(new[] { ownTail, player });
        var other = Car(main, 200, "other loco"); other.IsLoco = true;
        var wagon = Car(main, 220, "other wagon");
        var offRoute = Car(parallel, 200, "parallel");
        var inactive = Car(main, 300, "inactive"); inactive.gameObject.activeInHierarchy = false;
        var derailed = Car(main, 400, "derailed"); derailed.derailed = true;
        var broken = Car(main, 500, "broken"); broken.Throw = true;
        CarSpawner.Instance = new CarSpawner();
        CarSpawner.Instance.AllCars!.AddRange(new[] { player, ownTail, other, wagon, wagon, offRoute, inactive, derailed, broken });
        var route = Route(main); Settle(player, route);
        Check(NativeRouteTraffic.OwnCars.Count == 2 && NativeRouteTraffic.OwnCars.Any(c => c.Id == "own tail"), "The full own consist is retained for the origin schematic even when its tail is outside the captured rails");
        Check(NativeRouteTraffic.OwnCars[0].Id == "own tail" && NativeRouteTraffic.OwnCars[1].Id == "player", "Own schematic follows the native consist order instead of global spawn order");
        Check(NativeRouteTraffic.Markers.Count == 2 && NativeRouteTraffic.Markers.Any(c => c.IsLoco), "Other train loco and car produce individual markers without duplicate registry entries");
        Check(NativeRouteTraffic.Markers.All(c => c.Id.StartsWith("other")), "Parallel, inactive, derailed and own cars are excluded from physical other-traffic markers");
        Check(NativeRouteTraffic.Status.Contains("partial") && NativeRouteTraffic.Status.Contains("1 unreadable"), "One unreadable native car does not hide successfully sampled traffic");
        Check(Math.Abs(NativeRouteTraffic.SpeedKph!.Value - 36) < 0.001, "Native reverse metres-per-second speed converts to positive km/h");
        other.Bogies[0].traveller!.Span = 250;
        Settle(player, route);
        Check(NativeRouteTraffic.Markers.Single(c => c.Id == "other loco").Centre == 200, "Traffic is sampled at cadence rather than rescanned on every Update");
        Time.realtimeSinceStartup += 0.6f; Settle(player, route);
        Check(NativeRouteTraffic.Markers.Single(c => c.Id == "other loco").Centre == 250, "A new native sample advances moving cars");
        NativeRouteTraffic.Update(player, Route(main, true));
        Check(NativeRouteTraffic.Markers.Single(c => c.Id == "other loco").Centre == 750, "Retained native observations reproject against the current reversed route");
        var changed = Route(parallel); NativeRouteTraffic.Update(player, changed);
        Check(NativeRouteTraffic.Markers.Count == 0, "A changed route immediately excludes old off-route traffic");
        CarSpawner.Instance.AllCars.Clear();
        CarSpawner.Instance.AllCars.AddRange(Enumerable.Range(0, 200).Select(i => Car(main, i + 50, "batch " + i)));
        Time.realtimeSinceStartup += 1;
        NativeRouteTraffic.Update(player, route);
        Check(NativeRouteTraffic.Markers.Count < 200, "A registry larger than 32 cars yields work between Updates");
        Settle(player, route);
        Check(NativeRouteTraffic.Markers.Count == 200, "Batched sampling retains every associated native car without a display cap");
        Check(NativeRouteTraffic.OwnCars.Count == 2, "Own consist uses native train membership independently of the other-car registry");
        CarSpawner.Instance = null; NativeRouteTraffic.Update(player, route);
        Check(NativeRouteTraffic.Markers.Count == 0 && NativeRouteTraffic.OwnCars.Count == 2 && NativeRouteTraffic.Status.Contains("unavailable"), "Unavailable native registry clears other occupancy while retaining known own consist without creating a manager");
        NativeRouteTraffic.Reset();
        Check(NativeRouteTraffic.SpeedKph == null && NativeRouteTraffic.Markers.Count == 0, "World/HUD reset retires live speed and traffic");
        Console.WriteLine(checks + " native traffic checks passed (native boundary doubles; not game acceptance).");
    }
}
