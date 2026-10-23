using System;
using System.Collections.Generic;
using DvMod.HeadsUpDisplay;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string text)
    { if (!condition) throw new Exception(text); checks++; }
    private static RailTrack Station(string yard, string name)
    {
        StationController.allStations!.Add(new StationController { stationInfo = new StationInfo { YardID = yard, Name = name } });
        return new RailTrack { Logic = new LogicTrack { ID = new TrackID { yardId = yard, SignIDSubYardPart = "A", SignIDTrackPart = "1M", FullID = yard + "-A-1-M" } } };
    }
    private static Junction Switch(params Junction.Branch[] branches)
        => new Junction { inBranch = new Junction.Branch(new RailTrack()), outBranches = new List<Junction.Branch>(branches) };
    private static string Name(Junction junction, bool trailing = false, bool set = false)
        => NativeJunctionNames.Describe(junction, junction.inBranch!.track!, trailing, set, RouteTurnDirection.Ahead);
    private static void Drain(int frames = 400)
    { for (var index = 0; index < frames; index++) NativeJunctionNames.Update(); }
    private static void Reset()
    {
        NativeJunctionNames.Reset(); NativeTrackNames.Reset();
        StationController.allStations = new List<StationController>();
    }
    public static void Main()
    {
        Reset();
        var city = Station("CW", "City West");
        NativeTrackNames.Describe(city, out var label, out var code);
        Check(label == "City West · A yard, track 1, main line" && code == "CW-A-1-M", "Station track uses the actual station name and native parts, preserving its code");
        NativeTrackNames.Describe(new RailTrack { name = "[track diverging]99", Logic = new LogicTrack { ID = new TrackID { Generic = true } } }, out label, out code);
        Check(label == "Diverging track", "Generic junction track source labels become readable");
        NativeTrackNames.Describe(new RailTrack { name = "[track through]82" }, out label, out code);
        Check(label == "Through track", "Through tracks do not show structural brackets or source IDs");
        Check(RouteNaming.FallbackTrackName("[#] Road 20") == "Main line", "Native generic main-line marker has a readable fallback");
        Check(RouteNaming.FallbackTrackName("Road 17") == "Road 17", "An existing meaningful road name is preserved");
        Check(RouteNaming.FallbackTrackName("custom line") == "custom line", "Unknown custom names are preserved instead of guessed");
        Check(RouteNaming.FallbackTrackName("doubletrack-Siding-6-D") == "Siding 6 D", "DoubleTrack siding labels omit provider clutter while preserving unknown native tokens");
        Check(NativeTrackNames.StationName("#Y") == "", "Placeholder yards never claim station identities");
        StationController.allStations = null; NativeTrackNames.Reset();
        Check(NativeTrackNames.StationName("CW") == "", "Station lookup tolerates a scene transition");
        StationController.allStations = new List<StationController>(); Station("CW", "City West");
        Check(NativeTrackNames.StationName("CW") == "City West", "Transient absence does not poison the station-name cache");
        Reset();
        Check(NativeTrackNames.StationName("SW") == "", "An initialized but empty station catalogue has no invented station name");
        Station("SW", "Sawmill");
        Check(NativeTrackNames.StationName("SW") == "Sawmill", "A station added after catalogue initialization can resolve its name");
        StationController.allStations!.Add(new StationController { stationInfo = new StationInfo { YardID = "HB", Name = "" } });
        Check(NativeTrackNames.StationName("HB") == "", "A partially initialized station supplies no name yet");
        StationController.allStations[1].stationInfo!.Name = "Harbour";
        Check(NativeTrackNames.StationName("HB") == "Harbour", "A station whose name populates later does not remain cached as unnamed");

        Reset(); city = Station("CW", "City West"); var sawmill = Station("SW", "Sawmill");
        var forward = new RailTrack { Out = new List<Junction.Branch> { new Junction.Branch(city) } };
        var reverse = new RailTrack { In = new List<Junction.Branch> { new Junction.Branch(sawmill, false) } };
        var junction = Switch(new Junction.Branch(forward, true), new Junction.Branch(reverse, false));
        Check(Name(junction).Contains("destination being mapped"), "Collection queues mapping without synchronous graph traversal");
        Check(forward.ConnectionReads == 0 && reverse.ConnectionReads == 0, "Description does not traverse the graph");
        Drain(); var text = Name(junction);
        Check(text.Contains("set towards City West (straight ahead)") && text.Contains("branch 2 towards Sawmill"), "Both main-line branches show their connected station destinations");
        Check(forward.ConnectionReads == 1 && reverse.ConnectionReads == 1, "Native first=true goes outward and first=false inward");
        var before = forward.ConnectionReads + reverse.ConnectionReads;
        Name(junction); Drain(); Name(junction);
        Check(before == forward.ConnectionReads + reverse.ConnectionReads, "Completed branch searches are reused across captures");
        junction.selectedBranch = 1;
        text = Name(junction);
        Check(text.Contains("set towards Sawmill") && text.Contains("branch 1 towards City West"), "Switch selection relabels cached options without recrawling");
        Check(!text.Contains("left") && !text.Contains("right"), "Branch order never invents left or right directions");

        junction.selectedBranch = 0; // The changed reverse branch is outside the selected route.
        var replacement = Station("FF", "Food Factory");
        reverse.In = new List<Junction.Branch> { new Junction.Branch(replacement) };
        reverse.Regenerate();
        Check(Name(junction).Contains("destination being mapped") && !Name(junction).Contains("Sawmill"),
            "An off-route searched track's native regeneration retires cached destination labels immediately");
        Drain();
        Check(Name(junction).Contains("branch 2 towards Food Factory") && !Name(junction).Contains("Sawmill"),
            "Off-route native geometry invalidation resolves the changed branch connection");
        Check(reverse.GeometrySubscribers == 1,
            "Multiple captures share one native invalidation subscription per searched track");
        junction.selectedBranch = 1;

        var harbour = Station("HB", "Harbour");
        junction.outBranches![0] = new Junction.Branch(harbour);
        Check(Name(junction).Contains("destination being mapped"), "Replacing a root connection invalidates its junction destination cache");
        Drain();
        Check(Name(junction).Contains("branch 1 towards Harbour") && !Name(junction).Contains("City West"), "A changed native branch replaces stale station labels");
        NativeJunctionNames.Reset();
        Check(reverse.GeometrySubscribers == 0 && forward.GeometrySubscribers == 0,
            "Reset releases all searched-track invalidation subscriptions");
        Check(Name(junction).Contains("destination being mapped"), "World/geometry reset removes completed native destination searches");

        Reset(); var first = Station("A", "First station"); var later = Station("B", "Later station");
        first.Out = new List<Junction.Branch> { new Junction.Branch(later) };
        junction = Switch(new Junction.Branch(first)); Name(junction); Drain(); text = Name(junction);
        Check(text.Contains("First station") && !text.Contains("Later station") && first.ConnectionReads == 0, "Each path stops at its first named station");

        Reset(); var a = Station("A", "Alpha"); var b = Station("B", "Beta");
        var split = new RailTrack { Out = new List<Junction.Branch> { new Junction.Branch(a), new Junction.Branch(b) } };
        junction = Switch(new Junction.Branch(split)); Name(junction); Drain();
        Check(Name(junction).Contains("Alpha / Beta"), "Further native potential branches preserve all first-station alternatives");
        junction.inBranch = new Junction.Branch(a, false); Name(junction); Drain();
        Check(Name(junction, true, false).Contains("trailing, set against you · onward towards Alpha"), "Trailing travel resolves the in-branch away from the junction");
        Check(Name(junction, true, true).Contains("set for your route"), "Trailing switch alignment remains explicit");

        Reset(); var loop = new RailTrack(); loop.Out = new List<Junction.Branch> { new Junction.Branch(loop) };
        junction = Switch(new Junction.Branch(loop)); Name(junction); Drain();
        Check(Name(junction).Contains("destination unknown") && loop.ConnectionReads == 1, "Directed loop protection terminates without inventing a station");
        Reset(); var back = new RailTrack(); junction = Switch(new Junction.Branch(back)); back.outJunction = junction;
        back.Out = new List<Junction.Branch> { new Junction.Branch(Station("X", "Wrong via original junction")) };
        Name(junction); Drain();
        Check(Name(junction).Contains("destination unknown") && back.ConnectionReads == 0, "Returning to the original junction cannot import another branch's destination");

        Reset(); var invalid = new RailTrack { Throw = true }; junction = Switch(new Junction.Branch(invalid));
        Name(junction); Drain();
        Check(Name(junction).Contains("destination unknown") && invalid.GeometryReads == 1, "A failed native query is contained and is not retried each frame");
        Reset(); var far = new RailTrack { Length = 50001, Out = new List<Junction.Branch> { new Junction.Branch(Station("F", "Beyond mapping range")) } };
        junction = Switch(new Junction.Branch(far)); Name(junction); Drain();
        Check(Name(junction).Contains("destination unknown") && far.ConnectionReads == 0, "Destination search does not cross its explicit 50 km mapping bound");
        Reset(); a = Station("A", "Alpha");
        split = new RailTrack { Out = new List<Junction.Branch> { new Junction.Branch(a), new Junction.Branch(new RailTrack { Length = double.NaN }) } };
        junction = Switch(new Junction.Branch(split)); Name(junction); Drain();
        Check(Name(junction).Contains("Alpha (other connections unresolved)"), "One resolved path never hides an unresolved alternative");

        Reset(); var chain = new RailTrack(); var current = chain;
        for (var index = 0; index < 4200; index++)
        { var next = new RailTrack { Length = 0 }; current.Length = 0; current.Out = new List<Junction.Branch> { new Junction.Branch(next) }; current = next; }
        current.Out = new List<Junction.Branch> { new Junction.Branch(Station("X", "Beyond work bound")) };
        junction = Switch(new Junction.Branch(chain)); Name(junction); NativeJunctionNames.Update();
        Check(Name(junction).Contains("destination being mapped"), "Long searches stay incremental after one Update");
        Drain(2000);
        Check(Name(junction).Contains("destination unknown") && !Name(junction).Contains("Beyond work bound"), "Bounded native work reports unknown instead of claiming full destination coverage");
        Check(chain.ConnectionReads == 1, "Long searches do not revisit their starting track");

        Reset(); junction = new Junction { inBranch = null, outBranches = null, selectedBranch = 4 };
        text = NativeJunctionNames.Describe(junction, new RailTrack(), false, false, RouteTurnDirection.Unknown);
        Check(text == "Junction · selected branch unavailable", "Missing branch metadata produces a human-readable unavailable label");
        Console.WriteLine(checks + " native naming/destination checks passed");
    }
}
