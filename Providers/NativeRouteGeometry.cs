using System;
using DV.OriginShift;
using DV.PointSet;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>Uses the game's selected connections and cached railway geometry.</summary>
    internal sealed class NativeRouteGraph : IRouteGraph<RailTrack>
    {
        public bool TryGetLength(RailTrack track, out double length)
        {
            length = 0;
            if (track == null) return false;
            var points = track.GetKinkedPointSet();
            if (points?.points == null || points.points.Length < 2) return false;
            // Let the pure walker distinguish zero/invalid geometry from missing geometry.
            length = points.span;
            return true;
        }

        public bool TryGetNext(RailTrack track, bool forward, out RailTrack nextTrack, out bool nextForward)
        {
            nextTrack = null!;
            nextForward = false;
            if (track == null) return false;
            var branch = forward ? track.GetOutBranch() : track.GetInBranch();
            if (branch == null || branch.track == null) return false;
            nextTrack = branch.track;
            nextForward = branch.first;
            return true;
        }
    }

    internal static class NativeRouteGeometry
    {
        // Native sign anchors cancel the pole displacement. A larger residual means
        // the retained anchor no longer agrees with this known track's geometry.
        public const double KnownTrackProjectionToleranceMeters = 1;

        /// <summary>
        /// Samples double-precision native geometry. Elevation stays in the pointset's
        /// unshifted frame; signed grade is rise/horizontal run, without sign rounding.
        /// </summary>
        public static bool TrySample(RailTrack track, double span, bool forward,
            out double elevation, out double gradePercent)
        {
            elevation = 0;
            gradePercent = 0;
            if (track == null || !Finite(span)) return false;
            var pointSet = track.GetKinkedPointSet();
            if (!Usable(pointSet) || span < 0 || span > pointSet.span) return false;
            var points = pointSet.points;
            var index = SegmentAt(points, span, forward);
            var start = points[index];
            var end = points[index + 1];
            var spanLength = end.span - start.span;
            if (!Finite(spanLength) || spanLength <= 0 || !Finite(start.position) || !Finite(end.position))
                return false;
            var fraction = (span - start.span) / spanLength;
            if (!Finite(fraction) || fraction < 0 || fraction > 1) return false;
            var dx = end.position.x - start.position.x;
            var dy = end.position.y - start.position.y;
            var dz = end.position.z - start.position.z;
            var horizontal = Math.Sqrt(dx * dx + dz * dz);
            if (!Finite(horizontal) || horizontal <= 0) return false;
            elevation = start.position.y + fraction * dy;
            gradePercent = (forward ? dy : -dy) / horizontal * 100;
            return Finite(elevation) && Finite(gradePercent);
        }

        /// <summary>
        /// Projects a position only onto an already identified track. The native closest
        /// point query compensates for world shifts; refinement stays on its two adjacent
        /// native segments and cannot select a parallel track. An anchor farther than
        /// one metre from the identified geometry is unavailable, never snapped remotely.
        /// </summary>
        public static bool TryProjectKnownTrack(RailTrack track, Vector3 scenePosition, out double span)
        {
            span = 0;
            if (track == null || !Finite(scenePosition)) return false;
            var pointSet = track.GetKinkedPointSet();
            if (!Usable(pointSet)) return false;
            var nearest = RailTrack.GetClosestPoint(track, scenePosition).closestPoint;
            if (!nearest.HasValue || !Finite(nearest.Value.span)
                || nearest.Value.span < 0 || nearest.Value.span > pointSet.span) return false;
            var closest = nearest.Value;
            if (!Finite(closest.position)) return false;
            // Subtract as doubles to avoid another float rounding at large origin shifts.
            var move = OriginShift.currentMove;
            var x = (double)scenePosition.x - move.x;
            var y = (double)scenePosition.y - move.y;
            var z = (double)scenePosition.z - move.z;
            if (!Finite(x) || !Finite(y) || !Finite(z)) return false;
            span = closest.span;
            var bestDistance = SquaredDistance(x, y, z, closest.position.x, closest.position.y, closest.position.z);
            var index = closest.index;
            if (index < 0 || index >= pointSet.points.Length) return false;
            Refine(pointSet.points, index - 1, x, y, z, ref bestDistance, ref span);
            Refine(pointSet.points, index, x, y, z, ref bestDistance, ref span);
            return Finite(bestDistance)
                && bestDistance <= KnownTrackProjectionToleranceMeters * KnownTrackProjectionToleranceMeters
                && Finite(span) && span >= 0 && span <= pointSet.span;
        }

        /// <summary>
        /// The horizontal heading of travel at one end of a track: where travel leaves the far
        /// end, or where it enters this track. Degenerate samples are stepped over, so a kinked
        /// point set with repeated positions still yields a usable heading.
        /// </summary>
        public static bool TryHeading(RailTrack track, bool forward, bool leaving, out double x, out double z)
        {
            x = 0;
            z = 0;
            if (track == null) return false;
            var pointSet = track.GetKinkedPointSet();
            if (!Usable(pointSet)) return false;
            var points = pointSet.points;
            var last = points.Length - 1;
            var anchor = leaving == forward ? last : 0;
            var step = anchor == 0 ? 1 : -1;
            for (var distance = 1; distance <= 8; distance++)
            {
                var other = anchor + step * distance;
                if (other < 0 || other > last) return false;
                var near = leaving ? points[other].position : points[anchor].position;
                var far = leaving ? points[anchor].position : points[other].position;
                if (!Finite(near) || !Finite(far)) return false;
                var dx = far.x - near.x;
                var dz = far.z - near.z;
                if (!Finite(dx) || !Finite(dz) || (dx == 0 && dz == 0)) continue;
                x = dx;
                z = dz;
                return true;
            }
            return false;
        }

        /// <summary>Look into the branch curve: its initial tangent can match the straight route.</summary>
        public static bool TryJunctionHeading(RailTrack track, bool forward, out double x, out double z)
        {
            x = z = 0;
            if (track == null) return false;
            var points = track.GetKinkedPointSet();
            if (!Usable(points)) return false;
            var target = forward ? Math.Min(30, points.span) : Math.Max(0, points.span - 30);
            var index = SegmentAt(points.points, target, forward);
            var start = points.points[index];
            var end = points.points[index + 1];
            var length = end.span - start.span;
            if (!Finite(length) || length <= 0) return false;
            var fraction = (target - start.span) / length;
            var anchor = points.points[forward ? 0 : points.points.Length - 1].position;
            if (!Finite(fraction) || fraction < 0 || fraction > 1 || !Finite(start.position)
                || !Finite(end.position) || !Finite(anchor)) return false;
            x = start.position.x + (end.position.x - start.position.x) * fraction - anchor.x;
            z = start.position.z + (end.position.z - start.position.z) * fraction - anchor.z;
            return Finite(x) && Finite(z) && (x != 0 || z != 0);
        }

        private static void Refine(EquiPointSet.Point[] points, int index, double x, double y, double z,
            ref double bestDistance, ref double bestSpan)
        {
            if (index < 0 || index + 1 >= points.Length) return;
            var a = points[index];
            var b = points[index + 1];
            if (!Finite(a.position) || !Finite(b.position) || !Finite(a.span) || !Finite(b.span)
                || b.span <= a.span) return;
            var dx = b.position.x - a.position.x;
            var dy = b.position.y - a.position.y;
            var dz = b.position.z - a.position.z;
            var denominator = dx * dx + dy * dy + dz * dz;
            if (!Finite(denominator) || denominator <= 0) return;
            var fraction = ((x - a.position.x) * dx + (y - a.position.y) * dy + (z - a.position.z) * dz) / denominator;
            if (!Finite(fraction)) return;
            fraction = Math.Max(0, Math.Min(1, fraction));
            var distance = SquaredDistance(x, y, z, a.position.x + fraction * dx,
                a.position.y + fraction * dy, a.position.z + fraction * dz);
            if (!Finite(distance) || distance >= bestDistance) return;
            bestDistance = distance;
            bestSpan = a.span + fraction * (b.span - a.span);
        }

        private static int SegmentAt(EquiPointSet.Point[] points, double span, bool forward)
        {
            var low = 0;
            var high = points.Length - 1;
            while (low < high)
            {
                var mid = low + (high - low + 1) / 2;
                if (points[mid].span <= span) low = mid;
                else high = mid - 1;
            }
            // At a sample vertex, use the segment being entered in the travel direction.
            if (!forward && low > 0 && points[low].span == span) low--;
            return Math.Min(low, points.Length - 2);
        }

        private static bool Usable(EquiPointSet? pointSet) => pointSet?.points != null
            && pointSet.points.Length >= 2 && Finite(pointSet.span) && pointSet.span > 0;

        private static double SquaredDistance(double x, double y, double z, double px, double py, double pz)
        {
            var dx = x - px;
            var dy = y - py;
            var dz = z - pz;
            return dx * dx + dy * dy + dz * dz;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Vector3d value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
