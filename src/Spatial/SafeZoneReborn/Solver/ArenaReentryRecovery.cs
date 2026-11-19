using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

// A short overrun of a known convex arena may return through its outer edge.
// Unknown terrain, holes and concave gaps retain their ordinary blocking rules.
internal static class ArenaReentryRecovery
{
    internal const float MaximumDistance = 2;
    internal const float MaximumOutsideDistance = .5f;
    internal const float InteriorClearance = .25f;

    internal static bool IsSafe(SafeZoneSnapshot snapshot, Vector3 from, Vector3 to, DateTime now,
        float speed, SolveOptions options, float tolerance, double startDelay = 0)
    {
        var distance = GeometryHelper.DistanceXZ(from, to);
        if (!float.IsFinite(from.Y) || !float.IsFinite(to.Y) || !float.IsFinite(distance)
            || !float.IsFinite(speed) || speed <= 0 || !float.IsFinite(tolerance) || tolerance < 0
            || !double.IsFinite(startDelay) || startDelay < 0 || distance > MaximumDistance
            || startDelay + distance / speed > 1 || snapshot.ArenaBoundary is not { } boundary
            || !Supports(boundary)
            || !ArenaBoundaryTraversal.ContainsTube(boundary, to, to, InteriorClearance + tolerance)) return false;

        if (!boundary.Contains(from))
        {
            // Convexity proves a single entry without crossing a hole.
            float low = 0, high = 1;
            for (var i = 0; i < 24; ++i)
            {
                float middle = (low + high) * .5f;
                if (boundary.Contains(Vector3.Lerp(from, to, middle))) high = middle;
                else low = middle;
            }
            if (distance * high > MaximumOutsideDistance) return false;
        }
        else if (!ArenaBoundaryTraversal.ContainsSegment(boundary, from, to)) return false;
        if (snapshot.HardObstacles.Any(o => o.Geometry.Contains(from, options.HardSafetyMargin)
            || o.Geometry.Contains(to, options.HardSafetyMargin + tolerance)
            || o.Geometry.IntersectsSegment(from, to, options.HardSafetyMargin))) return false;
        var until = now.AddSeconds(startDelay + distance / speed + .1);
        return !snapshot.DangerAreas.Any(d => d.DangerWindows.Any(w =>
                w.Buffered(options.DangerStartSafetyTime, options.DangerEndSafetyTime).OverlapsInterval(now, until))
            && (d.Geometry.Contains(from, options.HardSafetyMargin)
                || d.Geometry.Contains(to, options.HardSafetyMargin + tolerance)
                || d.Geometry.IntersectsSegment(from, to, options.HardSafetyMargin)));
    }

    private static bool Supports(IGeometryShape boundary)
    {
        if (boundary is CircleGeometry or RectangleGeometry) return true;
        if (boundary is not WorldPolygonGeometry polygon) return false;
        var points = polygon.WorldPoints;
        float direction = 0;
        for (var i = 0; i < points.Count; ++i)
        {
            var a = points[(i + 1) % points.Count] - points[i];
            var b = points[(i + 2) % points.Count] - points[(i + 1) % points.Count];
            float cross = a.X * b.Z - a.Z * b.X;
            if (MathF.Abs(cross) <= .00001f) continue;
            if (direction * cross < 0) return false;
            direction = cross;
        }
        return direction != 0;
    }
}
