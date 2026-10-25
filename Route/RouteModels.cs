using System;
using System.Collections.Generic;
using System.Linq;

namespace DvMod.HeadsUpDisplay
{
    public enum RouteEventKind { Track, Junction, Speed, Grade, Signal, OtherSign }

    /// <summary>A retained source observation. Display grouping never removes its identity.</summary>
    public sealed class RouteEvent
    {
        public string Id { get; }
        public double Distance { get; }
        public RouteEventKind Kind { get; }
        public string Label { get; }
        public string Aspect { get; }
        public string ColorHex { get; }
        public string Fact { get; }
        public string Freshness { get; }
        public bool AppliesToTravel { get; }
        /// <summary>The game's own short identifier, kept so a row still matches job overviews.</summary>
        public string Code { get; }
        /// <summary>A short fixed marker drawn ahead of the description, such as a switch's side.</summary>
        public string Marker { get; }
        public double? SpeedLimitKph { get; }
        public double? AlternateSpeedLimitKph { get; }

        public RouteEvent(string id, double distance, RouteEventKind kind, string label,
            string aspect = "", string colorHex = "", string fact = "", string freshness = "",
            bool appliesToTravel = true, string code = "", string marker = "",
            double? speedLimitKph = null, double? alternateSpeedLimitKph = null)
        {
            if (double.IsNaN(distance) || double.IsInfinity(distance) || distance < 0)
                throw new ArgumentOutOfRangeException(nameof(distance));
            Id = id ?? "";
            Distance = distance;
            Kind = kind;
            Label = label ?? "";
            Aspect = aspect ?? "";
            ColorHex = colorHex ?? "";
            Fact = fact ?? "";
            Freshness = freshness ?? "";
            AppliesToTravel = appliesToTravel;
            Code = code ?? "";
            Marker = marker ?? "";
            SpeedLimitKph = speedLimitKph;
            AlternateSpeedLimitKph = alternateSpeedLimitKph;
        }
    }

    public readonly struct RouteProfilePoint
    {
        public double Distance { get; }
        public double Elevation { get; }
        public double GradePercent { get; }
        public RouteProfilePoint(double distance, double elevation, double gradePercent)
        {
            Distance = distance;
            Elevation = elevation;
            GradePercent = gradePercent;
        }
    }

    /// <summary>Immutable display data. Geometry completion and observation coverage stay separate.</summary>
    public sealed class RouteSnapshot<TTrack> where TTrack : notnull
    {
        public long Revision { get; }
        public double RequestedDistance { get; }
        public double ReachedDistance { get; }
        public RouteWalkTermination Termination { get; }
        public IReadOnlyList<RouteInterval<TTrack>> Intervals { get; }
        public IReadOnlyList<RouteEvent> Events { get; }
        public IReadOnlyList<RouteProfilePoint> Profile { get; }
        public bool IsCollecting { get; }
        public string EventCoverage { get; }
        public string SignalCoverage { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public DateTime UpdatedAtUtc { get; }

        public RouteSnapshot(long revision, double requestedDistance, double reachedDistance,
            RouteWalkTermination termination, IEnumerable<RouteInterval<TTrack>> intervals,
            IEnumerable<RouteEvent> events, IEnumerable<RouteProfilePoint> profile, bool isCollecting,
            string eventCoverage, string signalCoverage, IEnumerable<string>? diagnostics = null,
            DateTime? updatedAtUtc = null, bool alreadySorted = false)
        {
            Revision = revision;
            RequestedDistance = requestedDistance;
            ReachedDistance = reachedDistance;
            Termination = termination;
            Intervals = Array.AsReadOnly(intervals.ToArray());
            Events = Array.AsReadOnly(alreadySorted ? events.ToArray()
                : events.OrderBy(e => e.Distance).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray());
            Profile = Array.AsReadOnly(alreadySorted ? profile.ToArray() : profile.OrderBy(p => p.Distance).ToArray());
            IsCollecting = isCollecting;
            EventCoverage = eventCoverage ?? "";
            SignalCoverage = signalCoverage ?? "";
            Diagnostics = Array.AsReadOnly((diagnostics ?? Enumerable.Empty<string>()).ToArray());
            UpdatedAtUtc = updatedAtUtc ?? DateTime.UtcNow;
        }

        public static RouteSnapshot<TTrack> Empty(double requestedDistance = 50000) =>
            new RouteSnapshot<TTrack>(0, requestedDistance, 0, RouteWalkTermination.InProgress,
                Array.Empty<RouteInterval<TTrack>>(), Array.Empty<RouteEvent>(), Array.Empty<RouteProfilePoint>(),
                false, "No locomotive selected", "Optional signal data not sampled");

        /// <summary>Changes presentation status while sharing already-frozen capture collections.</summary>
        public RouteSnapshot<TTrack> WithStatus(bool isCollecting, string eventCoverage, string signalCoverage) =>
            new RouteSnapshot<TTrack>(this, isCollecting, eventCoverage, signalCoverage);

        private RouteSnapshot(RouteSnapshot<TTrack> source, bool isCollecting, string eventCoverage, string signalCoverage)
        {
            Revision = source.Revision;
            RequestedDistance = source.RequestedDistance;
            ReachedDistance = source.ReachedDistance;
            Termination = source.Termination;
            Intervals = source.Intervals;
            Events = source.Events;
            Profile = source.Profile;
            Diagnostics = source.Diagnostics;
            IsCollecting = isCollecting;
            EventCoverage = eventCoverage ?? "";
            SignalCoverage = signalCoverage ?? "";
            UpdatedAtUtc = source.UpdatedAtUtc;
        }
    }
}
