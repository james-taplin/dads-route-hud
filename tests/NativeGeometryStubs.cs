// Native boundary doubles only. Tests compile the production graph/geometry adapters.
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
}
namespace DV.PointSet
{
    public struct Vector3d
    {
        public double x, y, z;
        public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
    }
    public sealed class EquiPointSet
    {
        public struct Point
        {
            public double span;
            public int index;
            public Vector3d position;
        }
        public Point[] points = System.Array.Empty<Point>();
        public double span;
    }
}
namespace DV.OriginShift
{
    public static class OriginShift { public static DV.PointSet.Vector3d currentMove; }
}
public sealed class RailTrack
{
    public sealed class Branch { public RailTrack? track; public bool first; }
    public struct ClosestResult { public DV.PointSet.EquiPointSet.Point? closestPoint; }
    public DV.PointSet.EquiPointSet? Points;
    public Branch? In, Out;
    public DV.PointSet.EquiPointSet.Point? Closest;
    public int InReads, OutReads, ProjectionReads;
    public UnityEngine.Vector3 LastProjectionPosition;
    // Native assemblies expose an unannotated return; null remains a tested runtime case.
    public DV.PointSet.EquiPointSet GetKinkedPointSet() => Points!;
    public Branch? GetInBranch() { InReads++; return In; }
    public Branch? GetOutBranch() { OutReads++; return Out; }
    public static ClosestResult GetClosestPoint(RailTrack track, UnityEngine.Vector3 position)
    {
        track.ProjectionReads++;
        track.LastProjectionPosition = position;
        return new ClosestResult { closestPoint = track.Closest };
    }
}
