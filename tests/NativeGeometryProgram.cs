using System;
using DV.OriginShift;
using DV.PointSet;
using DvMod.HeadsUpDisplay;
using UnityEngine;

internal static class NativeGeometryProgram
{
    private static int checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; }
    private static void Near(double actual, double expected, string message)
        => Check(Math.Abs(actual - expected) < 1e-9, message + ": " + actual + " != " + expected);

    private static RailTrack Track(params (double span, double x, double y, double z)[] vertices)
    {
        var points = new EquiPointSet.Point[vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            var p = vertices[i];
            points[i] = new EquiPointSet.Point { index = i, span = p.span, position = new Vector3d(p.x, p.y, p.z) };
        }
        return new RailTrack { Points = new EquiPointSet { points = points, span = vertices.Length == 0 ? 0 : vertices[vertices.Length - 1].span } };
    }

    private static void Main()
    {
        Sampling();
        Headings();
        SelectedConnections();
        KnownTrackProjection();
        Console.WriteLine(checks + " native-geometry adapter checks passed (native boundary doubles; not game acceptance).");
    }

    private static void Sampling()
    {
        var track = Track((0, 0, 100, 0), (10, 10, 101, 0), (20, 20, 100, 0));
        Check(NativeRouteGeometry.TrySample(track, 5, true, out var elevation, out var grade), "Forward native geometry samples");
        Near(elevation, 100.5, "Elevation interpolates native doubles");
        Near(grade, 10, "Grade is rise over horizontal run");
        Check(NativeRouteGeometry.TrySample(track, 5, false, out elevation, out grade), "Reverse native geometry samples");
        Near(elevation, 100.5, "Reverse travel keeps physical elevation");
        Near(grade, -10, "Reverse travel reverses grade");
        Check(NativeRouteGeometry.TrySample(track, 10, true, out elevation, out grade), "Forward exact vertex samples");
        Near(grade, -10, "Forward vertex uses entered falling segment");
        Check(NativeRouteGeometry.TrySample(track, 10, false, out elevation, out grade), "Reverse exact vertex samples");
        Near(grade, -10, "Reverse vertex uses entered rising segment in reverse");
        Check(NativeRouteGeometry.TrySample(track, 0, false, out elevation, out grade), "Start endpoint remains sampleable in reverse");
        Near(elevation, 100, "Start endpoint elevation is exact");
        Check(NativeRouteGeometry.TrySample(track, 20, true, out elevation, out grade), "End endpoint remains sampleable forward");
        Near(elevation, 100, "End endpoint elevation is exact");
        Check(!NativeRouteGeometry.TrySample(track, -1, true, out _, out _), "Negative span is unavailable");
        Check(!NativeRouteGeometry.TrySample(track, 21, true, out _, out _), "Beyond-native span is unavailable");
        Check(!NativeRouteGeometry.TrySample(track, double.NaN, true, out _, out _), "NaN query is unavailable");
        Check(!NativeRouteGeometry.TrySample(track, double.PositiveInfinity, true, out _, out _), "Infinite query is unavailable");
        Check(!NativeRouteGeometry.TrySample(null!, 0, true, out _, out _), "Missing native track is unavailable");
        Check(!NativeRouteGeometry.TrySample(new RailTrack(), 0, true, out _, out _), "Missing native point set is unavailable");
        Check(!NativeRouteGeometry.TrySample(Track((0, 0, 0, 0)), 0, true, out _, out _), "Single native point is unavailable");
        Check(!NativeRouteGeometry.TrySample(Track((0, 0, 0, 0), (10, 0, 1, 0)), 5, true, out _, out _), "Vertical segment cannot supply horizontal grade");
        Check(!NativeRouteGeometry.TrySample(Track((0, 0, 0, 0), (10, double.NaN, 0, 0)), 5, true, out _, out _), "Nonfinite native position is unavailable");
        Check(!NativeRouteGeometry.TrySample(Track((0, 0, 0, 0), (0, 10, 0, 0)), 0, true, out _, out _), "Zero native span is unavailable");
        Check(!NativeRouteGeometry.TrySample(Track((0, 0, 0, 0), (double.PositiveInfinity, 10, 0, 0)), 5, true, out _, out _), "Nonfinite native total span is unavailable");
        OriginShift.currentMove = new Vector3d(1000000, -500000, 2000000);
        Check(NativeRouteGeometry.TrySample(track, 5, true, out elevation, out _), "Origin shift does not prevent native sampling");
        Near(elevation, 100.5, "Elevation stays in native unshifted frame");
        OriginShift.currentMove = default;
    }

    private static void Heading(RailTrack track, bool forward, bool leaving, double expectedX, double expectedZ)
    {
        Check(NativeRouteGeometry.TryHeading(track, forward, leaving, out var x, out var z), "Endpoint heading is available");
        Near(x, expectedX, "Heading horizontal x follows travel");
        Near(z, expectedZ, "Heading horizontal z follows travel");
    }
    private static void Headings()
    {
        var track = Track((0, 0, 0, 0), (10, 10, 0, 0), (20, 10, 0, 10));
        Heading(track, true, false, 10, 0);
        Heading(track, true, true, 0, 10);
        Heading(track, false, false, 0, -10);
        Heading(track, false, true, -10, 0);
        var repeated = Track((0, 0, 0, 0), (1, 0, 0, 0), (2, 10, 0, 0), (3, 10, 0, 0));
        Heading(repeated, true, false, 10, 0);
        Heading(repeated, true, true, 10, 0);
        Heading(repeated, false, false, -10, 0);
        Heading(repeated, false, true, -10, 0);
        Check(!NativeRouteGeometry.TryHeading(Track((0, 0, 0, 0), (1, 0, 2, 0)), true, true, out _, out _), "No horizontal heading is unavailable");
        Check(!NativeRouteGeometry.TryHeading(Track((0, 0, 0, 0), (1, double.NaN, 0, 0)), true, true, out _, out _), "Nonfinite endpoint heading is unavailable");
        Check(!NativeRouteGeometry.TryHeading(new RailTrack(), true, true, out _, out _), "Missing native heading geometry is unavailable");
    }

    private static void SelectedConnections()
    {
        var graph = new NativeRouteGraph();
        var start = Track((0, 0, 0, 0), (10, 10, 0, 0));
        var outgoing = Track((0, 0, 0, 0), (20, 20, 0, 0));
        var incoming = Track((0, 0, 0, 0), (30, 30, 0, 0));
        start.Out = new RailTrack.Branch { track = outgoing, first = false };
        start.In = new RailTrack.Branch { track = incoming, first = true };
        Check(graph.TryGetNext(start, true, out var next, out var direction) && ReferenceEquals(next, outgoing) && !direction,
            "Forward travel reads native outgoing branch and branch.first direction");
        Check(start.OutReads == 1 && start.InReads == 0, "Forward connection does not consult incoming branch");
        Check(graph.TryGetNext(start, false, out next, out direction) && ReferenceEquals(next, incoming) && direction,
            "Reverse travel reads native incoming branch and branch.first direction");
        Check(start.OutReads == 1 && start.InReads == 1, "Reverse connection does not consult outgoing branch");
        Check(graph.TryGetLength(start, out var length), "Native length is available");
        Near(length, 10, "Native total span is used as track length");
        Check(!graph.TryGetLength(new RailTrack(), out _), "Missing native length is unavailable");
        var zero = Track((0, 0, 0, 0), (0, 0, 0, 0));
        Check(graph.TryGetLength(zero, out length) && length == 0, "Zero native length reaches pure walker validation");
        var walk = new RouteWalkBuilder<RailTrack>(graph, start, 5, true, 20);
        while (!walk.IsComplete) walk.Step(1);
        Check(walk.Termination == RouteWalkTermination.HorizonReached && walk.Intervals.Count == 2, "Production graph integrates with incremental walker");
        Check(ReferenceEquals(walk.Intervals[1].Track, outgoing) && !walk.Intervals[1].Forward, "Walker follows exact native selected branch direction");
        Near(walk.Intervals[1].StartSpan, 20, "Reverse branch enters at its native end");
        Near(walk.Intervals[1].EndSpan, 5, "Reverse branch clips at requested native span");
        walk = new RouteWalkBuilder<RailTrack>(graph, zero, 0, true, 20);
        walk.Step(1);
        Check(walk.Termination == RouteWalkTermination.NoProgress, "Zero native geometry is reported as no progress");
        var invalid = Track((0, 0, 0, 0), (double.NaN, 10, 0, 0));
        walk = new RouteWalkBuilder<RailTrack>(graph, invalid, 0, true, 20);
        walk.Step(1);
        Check(walk.Termination == RouteWalkTermination.InvalidGeometry, "Nonfinite native length is reported as invalid geometry");
        Check(!graph.TryGetNext(outgoing, true, out _, out _), "Missing selected native connection remains a dead end");
        outgoing.Out = new RailTrack.Branch();
        Check(!graph.TryGetNext(outgoing, true, out _, out _), "Native branch with missing track is unavailable");
    }

    private static void KnownTrackProjection()
    {
        var track = Track((0, 0, 0, 0), (10, 10, 0, 0), (20, 10, 0, 10));
        track.Closest = track.Points!.points[1];
        Check(NativeRouteGeometry.TryProjectKnownTrack(track, new Vector3(8, 0, 0.25f), out var span), "Known-track query refines preceding native segment");
        Near(span, 8, "Previous-segment refinement preserves interpolated span");
        Check(NativeRouteGeometry.TryProjectKnownTrack(track, new Vector3(10.25f, 0, 7), out span), "Known-track query refines following native segment");
        Near(span, 17, "Following-segment refinement preserves interpolated span");
        Check(NativeRouteGeometry.TryProjectKnownTrack(track, new Vector3(10, 0, 0), out span), "Exact native vertex remains available");
        Near(span, 10, "Exact nearest vertex retains native span");
        var straight = Track((0, 0, 0, 0), (10, 10, 0, 0));
        straight.Closest = straight.Points!.points[0];
        Check(NativeRouteGeometry.TryProjectKnownTrack(straight, new Vector3(4, 1, 0), out span), "Exactly one metre residual is accepted");
        Near(span, 4, "One-metre accepted projection retains interpolated span");
        Check(!NativeRouteGeometry.TryProjectKnownTrack(straight, new Vector3(4, 1.001f, 0), out _), "Residual beyond one metre is unavailable");
        OriginShift.currentMove = new Vector3d(1000000, -500000, 2000000);
        var shifted = new Vector3(1000004, -500000, 2000000);
        Check(NativeRouteGeometry.TryProjectKnownTrack(straight, shifted, out span), "Native closest query plus refinement compensates origin shift");
        Near(span, 4, "Origin-shifted projection retains known native span");
        Check(straight.LastProjectionPosition.x == shifted.x && straight.LastProjectionPosition.z == shifted.z,
            "Native closest-point boundary receives original scene position");
        OriginShift.currentMove = new Vector3d(1000000.01, -500000, 2000000);
        Check(NativeRouteGeometry.TryProjectKnownTrack(straight, shifted, out span), "Fractional native origin shift remains projectable");
        Near(span, 1000004d - 1000000.01, "Origin subtraction keeps native double precision before refinement");
        OriginShift.currentMove = new Vector3d(double.NaN, 0, 0);
        Check(!NativeRouteGeometry.TryProjectKnownTrack(straight, shifted, out _), "Nonfinite origin shift is unavailable");
        OriginShift.currentMove = default;
        straight.Closest = null;
        Check(!NativeRouteGeometry.TryProjectKnownTrack(straight, new Vector3(4, 0, 0), out _), "Native closest-point failure remains unavailable");
        straight.Closest = new EquiPointSet.Point { index = 3, span = 4, position = new Vector3d(4, 0, 0) };
        Check(!NativeRouteGeometry.TryProjectKnownTrack(straight, new Vector3(4, 0, 0), out _), "Out-of-range native point index is unavailable");
        straight.Closest = new EquiPointSet.Point { index = 0, span = double.NaN, position = new Vector3d(4, 0, 0) };
        Check(!NativeRouteGeometry.TryProjectKnownTrack(straight, new Vector3(4, 0, 0), out _), "Nonfinite nearest native span is unavailable");
        straight.Closest = straight.Points.points[0];
        Check(!NativeRouteGeometry.TryProjectKnownTrack(straight, new Vector3(float.NaN, 0, 0), out _), "Nonfinite scene anchor is unavailable");
        Check(!NativeRouteGeometry.TryProjectKnownTrack(straight, new Vector3(100, 0, 0), out _), "Known track is never remotely snapped past endpoint");
    }
}
