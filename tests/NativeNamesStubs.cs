// Boundary doubles only; the native name/destination adapters compile from production source.
using System;
using System.Collections.Generic;
public sealed class RailTrack
{
    public string name = "Road";
    public LogicTrack? Logic;
    public Junction? inJunction, outJunction;
    public List<Junction.Branch>? In, Out;
    public double Length = 100;
    public double HeadingX, HeadingZ = 1;
    public int ConnectionReads, GeometryReads;
    public bool Throw;
    public event Action<RailTrack>? TrackPointsUpdated;
    public int GeometrySubscribers => TrackPointsUpdated?.GetInvocationList().Length ?? 0;
    public void Regenerate() => TrackPointsUpdated?.Invoke(this);
    public LogicTrack? LogicTrack() => Logic;
    public PointSet GetKinkedPointSet() { GeometryReads++; if (Throw) throw new InvalidOperationException(); return new PointSet { span = Length }; }
    public List<Junction.Branch>? GetAllInBranches() { ConnectionReads++; return In; }
    public List<Junction.Branch>? GetAllOutBranches() { ConnectionReads++; return Out; }
}
public sealed class PointSet { public double span; }
public sealed class Junction
{
    public sealed class Branch
    {
        public RailTrack? track;
        public bool first;
        public Branch(RailTrack track, bool first = true) { this.track = track; this.first = first; }
    }
    public Branch? inBranch;
    public List<Branch>? outBranches = new List<Branch>();
    public int selectedBranch;
}
public sealed class LogicTrack { public TrackID ID = new TrackID(); }
public sealed class TrackID
{
    public string yardId = "", SignIDSubYardPart = "", SignIDTrackPart = "", FullID = "";
    public bool Generic;
    public bool IsGeneric() => Generic;
}
public sealed class StationController
{
    public static List<StationController>? allStations = new List<StationController>();
    public StationInfo? stationInfo;
    public static StationController? GetStationByYardID(string yard)
        => allStations?.Find(station => station.stationInfo?.YardID == yard);
}
public sealed class StationInfo { public string Name = "", YardID = ""; }

namespace DvMod.HeadsUpDisplay
{
    internal static class NativeRouteGeometry
    {
        public static bool TryHeading(RailTrack track, bool forward, bool leaving, out double x, out double z)
        {
            x = track.HeadingX * (forward ? 1 : -1);
            z = track.HeadingZ * (forward ? 1 : -1);
            return x != 0 || z != 0;
        }
        public static bool TryJunctionHeading(RailTrack track, bool forward, out double x, out double z)
            => TryHeading(track, forward, false, out x, out z);
    }
}
