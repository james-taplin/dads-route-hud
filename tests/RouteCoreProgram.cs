using System;
using System.Collections.Generic;
using System.Linq;
using DvMod.HeadsUpDisplay;

internal static class RouteCoreProgram
{
    private static int checks;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    private static RouteWalkBuilder<string> Finish(Graph graph, string start, double span, bool forward,
        double distance = 50000, int stepSize = 11)
    {
        var builder = new RouteWalkBuilder<string>(graph, start, span, forward, distance);
        var calls = 0;
        while (!builder.IsComplete && calls++ < 100000) builder.Step(stepSize);
        Check(builder.IsComplete, "Test traversal did not terminate");
        return builder;
    }

    private static void Main()
    {
        BoundaryAndRetention();
        MoreThanOneHundredTracks();
        PartialStartLoop();
        ReverseAndCrossings();
        InvalidDataAndProgress();
        Pagination();
        ImmutableSnapshots();
        ShiftedViews();
        RestoredAndResizedWindows();
        AtomicDisplayReplacement();
        ConfigurableMetricColumns();
        FittedRouteGraph();
        ReadableNames();
        SwitchSides();
        StickyDirection();
        SyntheticTiming();
        Console.WriteLine(checks + " route-core checks passed.");
    }

    /// <summary>Readable names come from the game's own identifier parts, never a hardcoded yard list.</summary>
    private static void ReadableNames()
    {
        Check(RouteNaming.TrackNameFromParts("GF", "B", "5L", "Goods Factory", "[Y]_[GF]_[B-5-L]")
            == "Goods Factory · B yard, track 5, loading", "Yard track reads as plain English");
        Check(RouteNaming.TrackNameFromParts("GF", "B", "12SP", "Goods Factory", "x")
            == "Goods Factory · B yard, track 12, passenger storage", "Two-letter types and multi-digit numbers survive");
        Check(RouteNaming.TrackNameFromParts("", "", "M", "", "[Y]_[]_[--M]") == "Main line",
            "A main line with no yard identity is named, not left blank");
        Check(RouteNaming.TrackNameFromParts("CSW", "", "", "Coal Mine South", "x") == "Coal Mine South",
            "A station with no further parts is just the station");
        Check(RouteNaming.TrackNameFromParts("IMW", "A", "3S", "", "x") == "IMW · A yard, track 3, storage",
            "Without a resolved station the yard code still leads");
        Check(RouteNaming.TrackNameFromParts(null, null, null, null, "track_123") == "track_123",
            "An unidentified track falls back to its object name");
        Check(RouteNaming.TrackNameFromParts(null, null, null, null, null) == "Unnamed track",
            "Nothing identifying at all is reported, not blank");
        Check(RouteNaming.TrackNameFromParts("GF", "B", "9Z", "Goods Factory", "x")
            == "Goods Factory · B yard, track 9, Z", "An unknown type letter is shown as given, not guessed");

        RouteNaming.SplitTrackPart("5L", out var order, out var type);
        Check(order == "5" && type == "L", "Track part splits into number and type");
        RouteNaming.SplitTrackPart("M", out order, out type);
        Check(order == "" && type == "M", "A type with no number splits cleanly");
        RouteNaming.SplitTrackPart(null, out order, out type);
        Check(order == "" && type == "", "A missing track part is empty, not an error");

        Check(RouteNaming.SwitchName("Goods Factory", "junction-12", 2, 4) == "Goods Factory switch · branch 3 of 4",
            "Switch names its station and selected branch from one");
        Check(RouteNaming.SwitchName("", "junction-12", 0, 2) == "junction-12 · branch 1 of 2",
            "Without a station the native junction name is kept");
        Check(RouteNaming.SwitchName("Harbor", "j", 0, 0) == "Harbor switch",
            "An unusable branch count is omitted rather than shown as zero");
        Check(RouteNaming.SwitchName("Harbor", "j", 5, 3) == "Harbor switch",
            "A selected branch outside the count is not reported");

        Check(RouteNaming.GradeText(-1.44) == "-1.4 % falling" && RouteNaming.GradeText(1.25) == "+1.3 % climbing"
            && RouteNaming.GradeText(0) == "level" && RouteNaming.GradeText(double.NaN) == "grade unavailable",
            "Grades state their sense");

        Check(RouteNaming.Words("SpeedLimitDual") == "Speed limit dual", "Native type names read as words");
        Check(RouteNaming.Words("SignLP") == "Sign LP", "Runs of capitals are kept intact");
        Check(RouteNaming.Words(null) == "" && RouteNaming.Words("  ") == "", "Missing type names stay empty");

        Check(RouteNaming.KindWord(RouteEventKind.Junction) == "switch"
            && RouteNaming.KindWord(RouteEventKind.Speed) == "limit"
            && RouteNaming.KindWord(RouteEventKind.OtherSign) == "sign"
            && RouteNaming.KindWord(RouteEventKind.Track) == "track", "Each kind has one plain word");

        var coded = new RouteEvent("id", 10, RouteEventKind.Track, "Goods Factory · B yard, track 5, loading",
            code: "GF-B-5-L");
        var shifted = RouteSnapshotView.Shift(new RouteSnapshot<string>(1, 100, 100,
            RouteWalkTermination.HorizonReached, Array.Empty<RouteInterval<string>>(), new[] { coded },
            Array.Empty<RouteProfilePoint>(), false, "", ""), 5);
        Check(shifted.Events.Single().Code == "GF-B-5-L", "The game's code survives a shifted view");
    }

    /// <summary>
    /// Switch sides are judged by real-world geometry: heading north and leaving east is a right
    /// turn, whatever the engine's handedness.
    /// </summary>
    private static void SwitchSides()
    {
        const double north = 1, east = 1;
        Check(RouteTurn.Classify(0, north, east, 0) == RouteTurnDirection.Right, "North to east is a right turn");
        Check(RouteTurn.Classify(0, north, -east, 0) == RouteTurnDirection.Left, "North to west is a left turn");
        Check(RouteTurn.Classify(east, 0, 0, -north) == RouteTurnDirection.Right, "East to south is a right turn");
        Check(RouteTurn.Classify(east, 0, 0, north) == RouteTurnDirection.Left, "East to north is a left turn");
        Check(RouteTurn.Classify(0, north, 0, north) == RouteTurnDirection.Ahead, "Unchanged heading is straight ahead");
        Check(RouteTurn.Classify(0, 100, 1, 100) == RouteTurnDirection.Ahead, "A hair of divergence is still ahead");
        Check(RouteTurn.Classify(0, 100, 20, 100) == RouteTurnDirection.Right, "A real divergence takes a side");
        Check(RouteTurn.Classify(0, north, 0, -north) == RouteTurnDirection.Unknown, "A straight reversal has no side");
        Check(RouteTurn.Classify(0, 0, east, 0) == RouteTurnDirection.Unknown, "A degenerate heading is unknown");
        Check(RouteTurn.Classify(double.NaN, north, east, 0) == RouteTurnDirection.Unknown, "Unusable geometry is unknown");
        // The classification must not depend on how long the sampled segments happen to be.
        Check(RouteTurn.Classify(0, 0.01, 5000, 0) == RouteTurnDirection.Right, "Sample length does not change the side");

        Check(RouteTurn.Marker(RouteTurnDirection.Left) == "<<<" && RouteTurn.Marker(RouteTurnDirection.Right) == ">>>"
            && RouteTurn.Marker(RouteTurnDirection.Ahead) == "|||" && RouteTurn.Marker(RouteTurnDirection.Unknown) == "",
            "Markers are fixed tokens and absent when the side is unknown");

        Check(RouteNaming.SwitchText("Goods Factory", "j", 2, 4, false, false, RouteTurnDirection.Right)
            == "Goods Factory switch · branch 3 of 4, diverging right", "A facing switch states its side");
        Check(RouteNaming.SwitchText("Goods Factory", "j", 2, 4, false, false, RouteTurnDirection.Unknown)
            == "Goods Factory switch · branch 3 of 4", "An unknown side is omitted, not guessed");
        Check(RouteNaming.SwitchText("Harbor", "j", 1, 3, true, true, RouteTurnDirection.Unknown)
            == "Harbor switch · trailing, set for your route", "A trailing switch set for the route says so");
        Check(RouteNaming.SwitchText("Harbor", "j", 1, 3, true, false, RouteTurnDirection.Unknown)
            == "Harbor switch · trailing, set against you", "A trailing switch set elsewhere is reported");

        var marked = new RouteEvent("id", 20, RouteEventKind.Junction, "Harbor switch", marker: ">>>");
        var shifted = RouteSnapshotView.Shift(new RouteSnapshot<string>(2, 100, 100,
            RouteWalkTermination.HorizonReached, Array.Empty<RouteInterval<string>>(), new[] { marked },
            Array.Empty<RouteProfilePoint>(), false, "", ""), 5);
        Check(shifted.Events.Single().Marker == ">>>", "The marker survives a shifted view");
    }

    /// <summary>
    /// The reverser states the selected direction, so it leads; motion only decides when the lever
    /// is at neutral. The answer must also be sticky, because each change retires a whole capture.
    /// </summary>
    private static void StickyDirection()
    {
        // Full gear either way, and a short running cutoff, all state a direction.
        foreach (var cutoff in new[] { 0.5, 0.2, 0.1, 0.06 })
            Check(RouteDirection.Decide(0, cutoff, false, false) && !RouteDirection.Decide(0, -cutoff, false, true),
                "The reverser states the direction at " + cutoff);

        // The lever leads even when the train is still rolling the other way after a stop.
        Check(!RouteDirection.Decide(3, -0.4, true, true), "A reverser set back wins over forward roll");
        Check(RouteDirection.Decide(-3, 0.4, true, false), "A reverser set forward wins over reverse roll");
        Check(RouteDirection.Decide(0, 0.4, false, false), "A standing train shows the road it will take");

        // Mid-gear and lever float state nothing, so motion decides.
        foreach (var midGear in new[] { 0.0, 0.05, -0.05, 0.01 })
        {
            Check(RouteDirection.Decide(3, midGear, true, false), "Near neutral, forward motion decides at " + midGear);
            Check(!RouteDirection.Decide(-3, midGear, true, true), "Near neutral, reverse motion decides at " + midGear);
        }

        // Neither says anything: the previous answer must stand rather than flap while shunting.
        foreach (var creep in new[] { 0.0, 0.2, -0.2, 0.49, -0.49 })
            Check(RouteDirection.Decide(creep, 0, true, true) && !RouteDirection.Decide(creep, 0, true, false),
                "A decided direction survives a creep at " + creep);

        Check(RouteDirection.Decide(0.1, null, true, true) && !RouteDirection.Decide(0.1, null, true, false),
            "Without a reverser a slow vehicle keeps its decision");
        Check(RouteDirection.Decide(3, null, false, false) && !RouteDirection.Decide(-3, null, false, true),
            "Without a reverser clear motion still decides");
        Check(RouteDirection.Decide(0, null, false, false), "With nothing to go on at all, forward");
        Check(RouteDirection.Decide(double.NaN, null, true, true)
            && !RouteDirection.Decide(double.NaN, double.NaN, true, false),
            "Unusable inputs keep the decision");
    }

    private static void FittedRouteGraph()
    {
        foreach (var requested in new[] { 100d, 5000, 10000, 20000, 50000 })
        {
            var end = RouteGraphScale.End(requested, requested, true);
            Check(end == requested && RouteGraphScale.Fraction(requested, 0, end) == 1,
                "A completed selected range reaches the full graph width without a fixed 50 km denominator");
        }
        var fitted = RouteGraphScale.End(20000, 4900, true);
        Check(fitted == 4900 && RouteGraphScale.Fraction(4900, 0, fitted) == 1, "The reported 4.9/20 km case fills the graph using its actual mapped distance");
        Check(RouteGraphScale.Fraction(2450, 0, fitted) == 0.5, "Mid-route markers/profile share the fitted scale");
        var fixedEnd = RouteGraphScale.End(20000, 4900, false);
        Check(fixedEnd == 20000 && RouteGraphScale.Fraction(4900, 0, fixedEnd) == 0.245, "Selected-distance mode preserves true unavailable coverage");
        Check(RouteGraphScale.End(20000, 0, true) == 20000, "An empty capture retains a valid waiting axis");
        Check(RouteGraphScale.End(20000, 50000, true) == 20000, "Fit never displays beyond the selected horizon");
        Check(RouteGraphScale.End(20000, 0.5, true) == 0.5, "A short positive capture can still fill the width");
        Check(RouteGraphScale.Fraction(980, 980, 1960) == 0 && RouteGraphScale.Fraction(1960, 980, 1960) == 1, "Atlas subranges use their own shared start/end scale");
        Check(RouteGraphScale.End(double.NaN, double.NaN, true) == 1
            && RouteGraphScale.Fraction(1, 0, 0) == 0 && RouteGraphScale.Fraction(1, 0, double.NaN) == 0, "Invalid/empty ranges cannot generate invalid graph coordinates");
    }

    private static void ConfigurableMetricColumns()
    {
        var auto = MetricColumns.Arrange(new[] { 0, 0, 0, 0, 0 }, 2);
        Check(auto[0].SequenceEqual(new[] { 0, 2, 4 }) && auto[1].SequenceEqual(new[] { 1, 3 }), "Auto preserves existing row-major placement");
        var choices = new[] { 2, 1, 2, 8, 0 };
        var explicitColumns = MetricColumns.Arrange(choices, 8);
        Check(explicitColumns[1].SequenceEqual(new[] { 0, 2 }) && explicitColumns[0].SequenceEqual(new[] { 1 }), "Chosen columns preserve independent reading order");
        Check(explicitColumns[7].SequenceEqual(new[] { 3 }) && explicitColumns[4].SequenceEqual(new[] { 4 }), "Explicit and automatic assignments coexist");
        var folded = MetricColumns.Arrange(choices, 2);
        Check(folded[1].SequenceEqual(new[] { 0, 2, 3 }), "Reducing columns keeps out-of-range assignments visible in the last column");
        Check(choices.SequenceEqual(new[] { 2, 1, 2, 8, 0 }) && MetricColumns.Arrange(choices, 8)[7].Contains(3), "Reducing/restoring columns never changes saved choices");
        Check(MetricColumns.Arrange(choices, 1)[0].SequenceEqual(Enumerable.Range(0, choices.Length)), "One column retains every metric in reading order");
        Check(folded.SelectMany(c => c).OrderBy(i => i).SequenceEqual(Enumerable.Range(0, choices.Length)), "Every metric appears exactly once");
        Check(MetricColumns.Arrange(Array.Empty<int>(), 8).Count == 8, "Empty selection and empty columns are supported");
        Check(MetricColumns.Arrange(new[] { -1, int.MaxValue }, 99).SelectMany(c => c).Count() == 2, "Corrupt assignments/counts remain bounded and retain metrics");
        var tape = WindowGeometry.Resize(new WindowBounds(10, 10, 500, 140), 2000, -500, 1920, 1080, 320, 80);
        Check(tape.Height == 80 && tape.Width == 1910 && tape.X == 10, "Metrics can form a shallow full-width anchored tape");
        Check(WindowGeometry.TopRight(1020, 80, 1920, 1080, 320, 80).Height == 80, "Restoring a metrics tape preserves its shallow height");
    }

    private static void BoundaryAndRetention()
    {
        var graph = new Graph().Track("long", 60000);
        var walk = Finish(graph, "long", 0, true);
        Check(walk.Termination == RouteWalkTermination.HorizonReached && walk.ReachedDistance == 50000, "Actual 50 km horizon");
        var segment = walk.Intervals.Single();
        Check(segment.EndSpan == 50000 && segment.EndDistance == 50000, "Last interval clips at horizon");
        Check(segment.TryProject(49999, out var before) && before == 49999, "49,999 m retained");
        Check(segment.TryProject(50000, out var exact) && exact == 50000, "50,000 m included");
        Check(!segment.TryProject(50001, out _), "50,001 m excluded");
        Check(!segment.TryProject(double.NaN, out _) && !segment.TryProject(double.PositiveInfinity, out _), "Nonfinite event placement excluded");
        var events = Enumerable.Range(0, 50001).Select(x => (double)x)
            .Where(x => segment.TryProject(x, out _)).ToArray();
        Check(events.Length == 50001 && events.Last() == 50000, "Every available event retained, no row or signal cap");

        var reverse = Finish(graph, "long", 60000, false);
        var reverseSegment = reverse.Intervals.Single();
        Check(reverseSegment.StartSpan == 60000 && reverseSegment.EndSpan == 10000, "Reverse interval clips native span correctly");
        Check(reverseSegment.TryProject(10000, out var reverseEnd) && reverseEnd == 50000, "Reverse horizon included");
        Check(!reverseSegment.TryProject(9999, out _), "Reverse beyond horizon excluded");
    }

    private static void RestoredAndResizedWindows()
    {
        var restored = WindowGeometry.Clamp(3000, -50, 1100, 800, 1280, 720);
        Check(restored.X == 180 && restored.Y == 0 && restored.Width == 1100 && restored.Height == 720,
            "Resolution changes restore the entire window on screen");
        var tiny = WindowGeometry.Clamp(10, 10, 5, 5, 200, 90);
        Check(tiny.Width == 200 && tiny.Height == 90 && tiny.X == 0 && tiny.Y == 0,
            "Minimum geometry yields to a screen smaller than the normal minimum");
        var minimum = WindowGeometry.Clamp(-100, -100, -10, 0, 1920, 1080);
        Check(minimum.Width == 320 && minimum.Height == 120 && minimum.X == 0 && minimum.Y == 0,
            "Resize underflow retains usable header and grip");
        foreach (var bad in new[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity })
        {
            var result = WindowGeometry.Clamp(bad, bad, bad, bad, 1920, 1080);
            Check(result.X == 0 && result.Y == 0 && result.Width == 320 && result.Height == 120,
                "Corrupt saved geometry cannot strand or hide a window");
        }
        var enlarged = WindowGeometry.Clamp(400, 400, 4000, 4000, 1920, 1080);
        Check(enlarged.Width == 1920 && enlarged.Height == 1080 && enlarged.X == 0 && enlarged.Y == 0,
            "A resize beyond screen bounds preserves all window edges");
        var independent = WindowGeometry.Clamp(50, 60, 650, 250, 1920, 1080);
        Check(independent.X == 50 && independent.Y == 60 && independent.Width == 650 && independent.Height == 250,
            "Ordinary independent window geometry persists unchanged");
        var topRight = WindowGeometry.TopRight(520, 480, 1920, 1080);
        Check(topRight.X == 1390 && topRight.Y == 10, "Default position anchors ten pixels from the actual top-right screen edge");
        var smallerDisplay = WindowGeometry.TopRight(1020, 480, 800, 600);
        Check(smallerDisplay.X == 0 && smallerDisplay.Width == 800 && smallerDisplay.Y == 10,
            "Top-right placement uses fitted width on smaller displays");
        var resize = WindowGeometry.Resize(independent, 5000, 5000, 1920, 1080);
        Check(resize.X == 50 && resize.Y == 60 && resize.Width == 1870 && resize.Height == 1020,
            "Resizing against screen edges keeps the dragged window's origin anchored");
        var resizeBack = WindowGeometry.Resize(resize, -5000, -5000, 1920, 1080);
        Check(resizeBack.X == 50 && resizeBack.Y == 60 && resizeBack.Width == 320 && resizeBack.Height == 120,
            "Minimum resize keeps the window's independent saved position");
    }

    private static void AtomicDisplayReplacement()
    {
        RouteSnapshot<string> Sample(int count, long revision) => new RouteSnapshot<string>(revision, 50000, 6100,
            RouteWalkTermination.EndOfTrack, Array.Empty<RouteInterval<string>>(),
            Enumerable.Range(0, count).Select(i => new RouteEvent("item-" + i, i, i < 52 ? RouteEventKind.Track : RouteEventKind.Signal, "Item")),
            Array.Empty<RouteProfilePoint>(), false, "Observed", "Observed");
        var display = new RouteDisplayBuffer<string>();
        var full = Sample(79, 1); var partial = Sample(52, 2); var replacement = Sample(80, 2);
        Check(display.Select(full, false).Events.Count == 79, "Initial complete route is published");
        for (var frame = 0; frame < 4; frame++)
            Check(display.Select(partial, true).Events.Count == 79, "Periodic replacement cannot remove signal markers while its pass is reading");
        Check(ReferenceEquals(display.Select(replacement, false), replacement) && display.Capture!.Events.Count == 80,
            "Ready replacement publishes every new observation atomically");
        display.Clear();
        Check(ReferenceEquals(display.Select(partial, true), partial), "Hard route invalidation cannot carry old signal facts into the new route");
        display.Select(full, false);
        Check(ReferenceEquals(display.Select(partial, false), partial), "Unavailable or changed optional sources publish explicit current data, not a ghost signal sample");
    }

    private static void MoreThanOneHundredTracks()
    {
        var graph = new Graph();
        for (var i = 0; i < 1200; i++)
        {
            graph.Track("t" + i, 50);
            if (i > 0) graph.Next("t" + (i - 1), true, "t" + i, true);
        }
        var walk = new RouteWalkBuilder<string>(graph, "t0", 0, true);
        walk.Step(7);
        Check(!walk.IsComplete && walk.Termination == RouteWalkTermination.InProgress, "Work budget pauses instead of pretending completion");
        Check(walk.Intervals.Count == 7 && walk.ReachedDistance == 350, "Incremental work retained");
        while (!walk.IsComplete) walk.Step(19);
        Check(walk.Termination == RouteWalkTermination.HorizonReached, "Long chain reaches requested horizon");
        Check(walk.Intervals.Count == 1000 && walk.ReachedDistance == 50000, "More than 100 short tracks retained");
        Check(walk.Intervals.Last().Track == "t999", "Correct final short track");
        var intervalCount = walk.Intervals.Count;
        walk.Step(1);
        Check(walk.Intervals.Count == intervalCount, "Completed builder is idempotent");
    }

    private static void PartialStartLoop()
    {
        var graph = new Graph().Track("A", 1000).Track("B", 500).Track("C", 500)
            .Next("A", true, "B", true).Next("B", true, "C", true).Next("C", true, "A", true);
        var walk = Finish(graph, "A", 200, true, stepSize: 1);
        Check(walk.Termination == RouteWalkTermination.LoopClosed, "Directed loop is explicit");
        Check(walk.ReachedDistance == 2000 && walk.Intervals.Count == 4, "Starting partial track's unvisited prefix is retained");
        var last = walk.Intervals.Last();
        Check(last.Track == "A" && last.StartSpan == 0 && last.EndSpan == 200, "Stops at original start span, not first track re-entry");
        Check(last.TryProject(200, out var closure) && closure == 2000, "Loop-closing point retained for continuous profile");
        var projectedEvents = new List<double>();
        foreach (var interval in walk.Intervals)
            foreach (var span in interval.Track == "A" ? new[] { 100d, 200d, 900d } : Array.Empty<double>())
                if (interval.TryProject(span, out var distance)
                    && !(walk.Termination == RouteWalkTermination.LoopClosed && distance == walk.ReachedDistance))
                    projectedEvents.Add(distance);
        Check(projectedEvents.Count == 3 && projectedEvents.Contains(1900), "Closure event policy removes repeated endpoint but retains unvisited prefix events");

        var reverseGraph = new Graph().Track("A", 1000).Track("B", 500)
            .Next("A", false, "B", false).Next("B", false, "A", false);
        var reverse = Finish(reverseGraph, "A", 800, false);
        Check(reverse.Termination == RouteWalkTermination.LoopClosed && reverse.ReachedDistance == 1500, "Reverse partial-start loop length");
        Check(reverse.Intervals.Last().StartSpan == 1000 && reverse.Intervals.Last().EndSpan == 800, "Reverse unvisited suffix retained");
        var shortWalk = Finish(graph, "A", 200, true, 1900);
        Check(shortWalk.Termination == RouteWalkTermination.HorizonReached && shortWalk.ReachedDistance == 1900, "Horizon before loop closure wins");
    }

    private static void ReverseAndCrossings()
    {
        // The same physical track in the opposite direction is a distinct directed interval.
        var reversing = new Graph().Track("same", 100).Next("same", true, "same", false).Next("same", false, "same", true);
        var walk = Finish(reversing, "same", 0, true);
        Check(walk.Termination == RouteWalkTermination.LoopClosed && walk.ReachedDistance == 200, "Opposite direction is not prematurely called a loop");
        Check(walk.Intervals.Count == 2 && walk.Intervals[0].Forward && !walk.Intervals[1].Forward, "Both directional traversals retained");

        // IDs stand for physical objects: identical positions/display names are irrelevant.
        var crossing = new Graph().Track("crossing-upper", 100).Track("crossing-lower", 100)
            .Next("crossing-upper", true, "crossing-lower", true);
        var crossed = Finish(crossing, "crossing-upper", 0, true, 200);
        Check(crossed.Termination == RouteWalkTermination.HorizonReached && crossed.Intervals.Count == 2, "Physical crossing does not imply route loop");

        var boundaryStart = new Graph().Track("exit", 100).Track("next", 100).Next("exit", true, "next", true);
        var atExit = Finish(boundaryStart, "exit", 100, true, 50);
        Check(atExit.Termination == RouteWalkTermination.HorizonReached && atExit.Intervals.Single().Track == "next", "Starting at a connected exit is not zero-progress failure");
    }

    private static void InvalidDataAndProgress()
    {
        var graph = new Graph().Track("good", 100);
        foreach (var distance in new[] { 0d, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Check(Finish(graph, "good", 0, true, distance).Termination == RouteWalkTermination.InvalidDistance, "Invalid requested distance explicit");
        foreach (var span in new[] { -1d, 101, double.NaN, double.PositiveInfinity })
            Check(Finish(graph, "good", span, true).Termination == RouteWalkTermination.InvalidStart, "Invalid initial span explicit");
        foreach (var length in new[] { -1d, double.NaN, double.PositiveInfinity })
            Check(Finish(new Graph().Track("bad", length), "bad", 0, true).Termination == RouteWalkTermination.InvalidGeometry, "Invalid geometry explicit");
        Check(Finish(new Graph(), "missing", 0, true).Termination == RouteWalkTermination.MissingGeometry, "Missing native geometry explicit");
        Check(Finish(new Graph().Track("zero", 0), "zero", 0, true).Termination == RouteWalkTermination.NoProgress, "Zero-length track stops explicitly");
        var end = Finish(graph, "good", 0, true);
        Check(end.Termination == RouteWalkTermination.EndOfTrack && end.ReachedDistance == 100, "Dead end reports true shorter distance");
        var tiny = new Graph().Track("first", 1).Track("tiny", double.Epsilon).Next("first", true, "tiny", true);
        var noProgress = Finish(tiny, "first", 0, true);
        Check(noProgress.Termination == RouteWalkTermination.NoProgress && noProgress.ReachedDistance == 1, "Unrepresentable distance progress cannot hang");
        var threw = false;
        try { new RouteWalkBuilder<string>(graph, "good", 0, true).Step(0); }
        catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "Nonpositive per-step work budget rejected");
        NamedTermination();
    }

    /// <summary>A route short of the horizon must say which directed track it stopped on.</summary>
    private static void NamedTermination()
    {
        var deadEnd = Finish(new Graph().Track("only", 100), "only", 0, true);
        Check(deadEnd.TerminalTrack == "only" && deadEnd.TerminalForward,
            "Dead end names the track it ended on");

        // The track whose geometry is unavailable is named, not the last one traversed.
        var ungenerated = new Graph().Track("first", 100).Next("first", true, "absent", true);
        var missing = Finish(ungenerated, "first", 0, true);
        Check(missing.Termination == RouteWalkTermination.MissingGeometry
            && missing.TerminalTrack == "absent" && missing.ReachedDistance == 100,
            "Unavailable next geometry names that track");

        var reverse = new Graph().Track("entry", 100).Track("stub", 50).Next("entry", false, "stub", false);
        var reversed = Finish(reverse, "entry", 100, false);
        Check(reversed.Termination == RouteWalkTermination.EndOfTrack
            && reversed.TerminalTrack == "stub" && !reversed.TerminalForward
            && reversed.ReachedDistance == 150, "Reverse travel names the directed terminal track");

        var loop = new Graph().Track("ring", 400).Next("ring", true, "ring", true);
        var closed = Finish(loop, "ring", 0, true);
        Check(closed.Termination == RouteWalkTermination.LoopClosed && closed.TerminalTrack == "ring",
            "Loop closure names its own track");

        var full = Finish(new Graph().Track("long", 60000), "long", 0, true, 20000);
        Check(full.Termination == RouteWalkTermination.HorizonReached && full.ReachedDistance == 20000
            && full.TerminalTrack == "long", "Full selected look-ahead still reports its track");
    }

    private static void Pagination()
    {
        var all = Enumerable.Range(0, 50001).ToArray();
        var page = RoutePaging.GetPage(all, 0, 37);
        var visited = new List<int>();
        for (var index = 0; index < page.PageCount; index++) visited.AddRange(RoutePaging.GetPage(all, index, 37).Items);
        Check(visited.SequenceEqual(all), "Every retained event is reachable through paging, without omissions or duplicates");
        Check(RoutePaging.GetPage(all, int.MaxValue, 37).Items.Last() == 50000, "Outdated page selection clamps to final populated page");
        Check(RoutePaging.GetPage(all, -5, 37).PageIndex == 0, "Negative page clamps to first page");
        var empty = RoutePaging.GetPage(Array.Empty<int>(), 10, 37);
        Check(empty.Items.Count == 0 && empty.PageCount == 0 && empty.TotalCount == 0, "Empty catalogue is explicit");
        Check(all.Length == 50001, "Paging never changes retained source catalogue");
    }

    private static void ImmutableSnapshots()
    {
        var intervals = new List<RouteInterval<string>>
        {
            new RouteInterval<string>("same", true, 0, 50000, 0, 50000)
        };
        var events = Enumerable.Range(0, 50001)
            .Select(i => new RouteEvent("event-" + i, 50000 - i, RouteEventKind.Signal,
                "Observed signal", appliesToTravel: i % 2 == 0)).ToList();
        var profile = new List<RouteProfilePoint>
        {
            new RouteProfilePoint(50000, 10, 1), new RouteProfilePoint(0, 0, 0)
        };
        var diagnostics = new List<string> { "Signs are observed coverage" };
        var updated = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
        var snapshot = new RouteSnapshot<string>(7, 50000, 50000,
            RouteWalkTermination.HorizonReached, intervals, events, profile, false,
            "Observed signs", "Observed signals", diagnostics, updated);
        Check(snapshot.Events.Count == 50001, "Snapshot retains every available event without a count cap");
        Check(snapshot.Events.Count(e => !e.AppliesToTravel) == 25000, "Opposite-facing observations remain accessible");
        Check(snapshot.Events[0].Distance == 0 && snapshot.Events[50000].Distance == 50000,
            "Snapshot sorts all events while retaining both inclusive boundaries");
        Check(snapshot.Profile[0].Distance == 0 && snapshot.Profile[1].Distance == 50000,
            "Profile keeps the exact closure or horizon endpoint");
        Check(snapshot.Revision == 7 && snapshot.UpdatedAtUtc == updated,
            "Snapshot preserves capture revision and explicit timestamp");
        intervals.Clear(); events.Clear(); profile.Clear(); diagnostics.Clear();
        Check(snapshot.Intervals.Count == 1 && snapshot.Events.Count == 50001
            && snapshot.Profile.Count == 2 && snapshot.Diagnostics.Count == 1,
            "Snapshot copies source collections before later incremental capture changes");
        var allPages = new List<RouteEvent>();
        var first = RoutePaging.GetPage(snapshot.Events, 0, 13);
        for (var page = 0; page < first.PageCount; page++)
            allPages.AddRange(RoutePaging.GetPage(snapshot.Events, page, 13).Items);
        Check(allPages.SequenceEqual(snapshot.Events), "Every retained snapshot observation is accessible through pages");
        var empty = RouteSnapshot<string>.Empty();
        Check(empty.RequestedDistance == 50000 && empty.ReachedDistance == 0
            && empty.Events.Count == 0 && empty.Intervals.Count == 0 && !empty.IsCollecting,
            "Empty snapshot has no fabricated geometry or events");
    }

    private static void ShiftedViews()
    {
        var intervals = new[]
        {
            new RouteInterval<string>("forward", true, 10, 110, 0, 100),
            new RouteInterval<string>("reverse", false, 200, 0, 100, 300)
        };
        var events = new[]
        {
            new RouteEvent("passed", 25, RouteEventKind.Track, "Passed"),
            new RouteEvent("at-origin", 50, RouteEventKind.Signal, "At origin", appliesToTravel: false),
            new RouteEvent("upcoming", 200, RouteEventKind.Signal, "Upcoming", "RED", "FF0000", "Occupied", "Capture"),
            new RouteEvent("at-end", 300, RouteEventKind.OtherSign, "At end")
        };
        var profile = new[]
        {
            new RouteProfilePoint(0, 0, 0), new RouteProfilePoint(100, 10, 2),
            new RouteProfilePoint(300, 20, 3)
        };
        var captured = new DateTime(2026, 10, 18, 1, 0, 0, DateTimeKind.Utc);
        var source = new RouteSnapshot<string>(4, 50000, 300, RouteWalkTermination.HorizonReached,
            intervals, events, profile, false, "Observed signs", "Observed signals", updatedAtUtc: captured);
        Check(ReferenceEquals(source, RouteSnapshotView.Shift(source, 0)), "Zero progress preserves the existing immutable view");
        var shifted = RouteSnapshotView.Shift(source, 50);
        Check(shifted.RequestedDistance == 50000 && shifted.ReachedDistance == 250,
            "Shift preserves requested horizon and reports actual remaining captured distance");
        Check(shifted.Events.Select(e => e.Id).SequenceEqual(new[] { "at-origin", "upcoming", "at-end" })
            && shifted.Events.Select(e => e.Distance).SequenceEqual(new[] { 0d, 150d, 250d }),
            "Current view drops passed events and adjusts every retained distance");
        Check(!shifted.Events[0].AppliesToTravel && shifted.Events[1].Aspect == "RED"
            && shifted.Events[1].ColorHex == "FF0000" && shifted.Events[1].Fact == "Occupied"
            && shifted.Events[1].Freshness == "Capture", "Shift preserves opposite-facing and optional observation detail");
        Check(shifted.Intervals[0].StartSpan == 60 && shifted.Intervals[0].EndSpan == 110
            && shifted.Intervals[0].StartDistance == 0 && shifted.Intervals[0].EndDistance == 50,
            "Forward current interval is clipped in native spans and route distances");
        Check(shifted.Intervals[1].StartSpan == 200 && shifted.Intervals[1].StartDistance == 50
            && shifted.Intervals[1].EndDistance == 250, "Later reverse interval retains orientation and native geometry");
        Check(shifted.Profile[0].Distance == 0 && shifted.Profile[0].Elevation == 5
            && shifted.Profile[0].GradePercent == 1, "Current profile origin is interpolated within the captured geometry");
        Check(shifted.Profile.Last().Distance == 250 && shifted.Profile.Last().Elevation == 20,
            "Shifted profile preserves the capture endpoint");
        Check(shifted.IsCollecting && shifted.EventCoverage.Contains("tail refresh pending")
            && shifted.Termination == RouteWalkTermination.HorizonReached,
            "A shifted horizon explicitly awaits a refreshed tail rather than claiming 50 km coverage");
        Check(shifted.UpdatedAtUtc == captured && shifted.Revision == source.Revision,
            "View progress does not fabricate a fresh capture timestamp");
        Check(source.Events.Count == 4 && source.Events[0].Distance == 25 && source.Intervals[0].StartSpan == 10
            && source.Profile[0].Distance == 0 && !source.IsCollecting,
            "Base capture retains passed records and its original geometry unchanged");
        var reverse = RouteSnapshotView.Shift(source, 150);
        Check(reverse.Intervals.Count == 1 && reverse.Intervals[0].StartSpan == 150
            && reverse.Intervals[0].EndSpan == 0 && reverse.Intervals[0].StartDistance == 0,
            "Progress on reverse track clips from its native high span");
        var boundary = RouteSnapshotView.Shift(source, 100);
        Check(boundary.Intervals.Count == 1 && boundary.Intervals[0].Track == "reverse"
            && boundary.Profile.Count == 2 && boundary.Profile[0].Distance == 0
            && boundary.Profile[0].Elevation == 10, "Exact interval and profile boundaries have no zero-length predecessor or duplicate origin");
        var passedAll = RouteSnapshotView.Shift(source, 301);
        Check(passedAll.ReachedDistance == 0 && passedAll.Events.Count == 0 && passedAll.Intervals.Count == 0
            && passedAll.Profile.Count == 0 && passedAll.IsCollecting,
            "Advancing beyond a capture produces an explicit empty pending tail");
        foreach (var termination in new[] { RouteWalkTermination.LoopClosed, RouteWalkTermination.EndOfTrack })
        {
            var ended = new RouteSnapshot<string>(5, 50000, 300, termination, intervals, events, profile,
                false, "Observed signs", "Observed signals");
            var endView = RouteSnapshotView.Shift(ended, 200);
            Check(endView.Termination == termination && endView.ReachedDistance == 100
                && endView.IsCollecting == (termination == RouteWalkTermination.LoopClosed),
                "Dead end remains a true moving endpoint; captured loop boundary awaits a current-origin refresh");
        }
        var loopGraph = new Graph().Track("loop", 300).Next("loop", true, "loop", true);
        var initialLoop = Finish(loopGraph, "loop", 0, true);
        var loopCapture = new RouteSnapshot<string>(6, 50000, initialLoop.ReachedDistance,
            initialLoop.Termination, initialLoop.Intervals, Array.Empty<RouteEvent>(), profile,
            false, "Observed signs", "Observed signals");
        var loopView = RouteSnapshotView.Shift(loopCapture, 50);
        var recapturedLoop = Finish(loopGraph, "loop", 50, true);
        Check(loopView.ReachedDistance == 250 && loopView.IsCollecting
            && loopView.EventCoverage.Contains("captured loop boundary; refresh from current position pending")
            && recapturedLoop.ReachedDistance == 300 && recapturedLoop.Intervals.Last().EndSpan == 50,
            "Shifted loop identifies the old capture boundary while a fresh walk retains the current-origin prefix");
        foreach (var offset in new[] { -1d, double.NaN, double.NegativeInfinity, double.PositiveInfinity })
        {
            var rejected = false;
            try { RouteSnapshotView.Shift(source, offset); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid progress is rejected at the view boundary");
        }
    }

    private static void SyntheticTiming()
    {
        // Deterministic in-memory workload, not a Unity/game performance measurement.
        var graph = new Graph();
        for (var i = 0; i < 1000; i++)
        {
            graph.Track("bench-" + i, 50);
            if (i > 0) graph.Next("bench-" + (i - 1), true, "bench-" + i, true);
        }
        var walk = Finish(graph, "bench-0", 0, true, stepSize: 32);
        var walkMs = WarmMedian(() =>
        {
            var item = new RouteWalkBuilder<string>(graph, "bench-0", 0, true);
            while (!item.IsComplete) item.Step(32);
            GC.KeepAlive(item);
        });
        var events = Enumerable.Range(0, 50001).Select(i =>
            new RouteEvent("bench-event-" + i, i, RouteEventKind.Signal, "Signal", appliesToTravel: i % 2 == 0)).ToArray();
        var snapshot = new RouteSnapshot<string>(10, 50000, 50000, walk.Termination, walk.Intervals,
            events, Array.Empty<RouteProfilePoint>(), false, "Synthetic coverage", "Synthetic coverage");
        var captureMs = WarmMedian(() => GC.KeepAlive(new RouteSnapshot<string>(10, 50000, 50000,
            walk.Termination, walk.Intervals, events, Array.Empty<RouteProfilePoint>(), false,
            "Synthetic coverage", "Synthetic coverage")));
        var shifted = RouteSnapshotView.Shift(snapshot, 1);
        var shiftMs = WarmMedian(() => GC.KeepAlive(RouteSnapshotView.Shift(snapshot, 1)));
        Check(walk.Intervals.Count == 1000 && walk.ReachedDistance == 50000 && snapshot.Events.Count == 50001,
            "Synthetic timing workload covers 50 km across 1,000 tracks with 50,001 observations");
        Check(shifted.Events.Count == 50000 && shifted.Events.Select(e => e.Id).Distinct().Count() == 50000
            && shifted.Events.Last().Distance == 49999,
            "Synthetic shifted view retains every remaining ID exactly once through its true tail");
        Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "Synthetic in-memory timing (warm median of 5): 1,000-track/50km walk {0:F2} ms; 50,001-event snapshot {1:F2} ms; 50,000-event shifted view {2:F2} ms. Not game performance.",
            walkMs, captureMs, shiftMs));
    }

    private static double WarmMedian(Action action)
    {
        action();
        var samples = new double[5];
        for (var i = 0; i < samples.Length; i++)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            action();
            timer.Stop();
            samples[i] = timer.Elapsed.TotalMilliseconds;
        }
        Array.Sort(samples);
        return samples[samples.Length / 2];
    }

    private sealed class Graph : IRouteGraph<string>
    {
        private readonly Dictionary<string, double> lengths = new Dictionary<string, double>();
        private readonly Dictionary<(string, bool), (string, bool)> next = new Dictionary<(string, bool), (string, bool)>();
        public Graph Track(string key, double length) { lengths[key] = length; return this; }
        public Graph Next(string key, bool forward, string nextKey, bool nextForward) { next[(key, forward)] = (nextKey, nextForward); return this; }
        public bool TryGetLength(string track, out double length) => lengths.TryGetValue(track, out length);
        public bool TryGetNext(string track, bool forward, out string nextTrack, out bool nextForward)
        {
            if (next.TryGetValue((track, forward), out var value)) { nextTrack = value.Item1; nextForward = value.Item2; return true; }
            nextTrack = ""; nextForward = false; return false;
        }
    }
}
