using System;
using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

// Only the boundary producer may attach a real interior to its navigation band.
// Every unmarked danger keeps the ordinary spatial and temporal checks.
internal static class ArenaMarginRecovery
{
    internal const float MaximumDistance = 2f;
    internal const double MaximumDuration = 1;

    internal static bool HasSource(SafeZoneSnapshot snapshot)
        => snapshot.DangerAreas.Any(d => d.DangerWindows.Any(w => w.ArenaMarginInterior != null));

    internal static IEnumerable<Vector3> ProjectedCandidates(SafeZoneSnapshot snapshot, Vector3 player, Vector3 cell)
    {
        foreach (var interior in snapshot.DangerAreas.SelectMany(d => d.DangerWindows)
            .Select(w => w.ArenaMarginInterior).OfType<RectangleGeometry>().Distinct())
        {
            var a = GeometryHelper.WorldToLocal(player, interior.Position, interior.Rotation);
            var b = GeometryHelper.WorldToLocal(cell, interior.Position, interior.Rotation);
            // A grid center may require an unnecessary sideways step at a wall.
            // Project it into the inward rectangle, then validate this exact point.
            var point = new Vector2(Math.Clamp(b.X, MathF.Min(0, a.X), MathF.Max(0, a.X)),
                Math.Clamp(b.Y, MathF.Min(0, a.Y), MathF.Max(0, a.Y)));
            yield return GeometryHelper.LocalToWorld(point, interior.Position, interior.Rotation);
        }
    }

    internal static bool IsSafe(SafeZoneSnapshot snapshot, Vector3 from, Vector3 to, DateTime now,
        float speed, SolveOptions options, float tolerance, double startDelay = 0, bool requireInitialMargin = false)
    {
        var distance = GeometryHelper.DistanceXZ(from, to);
        if (!float.IsFinite(distance) || !float.IsFinite(speed) || speed <= 0
            || !double.IsFinite(startDelay) || startDelay < 0 || distance > MaximumDistance
            || startDelay + distance / speed > MaximumDuration || snapshot.ArenaBoundary is not { } boundary
            || !ArenaBoundaryTraversal.ContainsSegment(boundary, from, to)) return false;
        if (snapshot.HardObstacles.Any(o => o.Geometry.Contains(from, options.HardSafetyMargin)
            || o.Geometry.Contains(to, options.HardSafetyMargin)
            || o.Geometry.IntersectsSegment(from, to, options.HardSafetyMargin))) return false;

        var arrival = now.AddSeconds(startDelay + distance / speed + .1);
        var source = false;
        var initialMargin = false;
        foreach (var danger in snapshot.DangerAreas)
        {
            foreach (var window in danger.DangerWindows)
            {
                if (!window.Buffered(options.DangerStartSafetyTime, options.DangerEndSafetyTime)
                    .OverlapsInterval(now, arrival)) continue;
                if (window.ArenaMarginInterior is { } interior)
                {
                    source = true;
                    // The true fence, holes and floor boundary never gain an exception.
                    if (!interior.Contains(from, -.001f) || !interior.Contains(to, -.001f)
                        || !ArenaBoundaryTraversal.ContainsSegment(interior, from, to)
                        || !MovesInward(interior, from, to)) return false;
                    if (danger.Geometry.Contains(to, options.HardSafetyMargin + tolerance)) return false;
                    initialMargin |= danger.Geometry.Contains(from, options.HardSafetyMargin);
                    continue;
                }
                if (danger.Geometry.Contains(from, options.HardSafetyMargin)
                    || danger.Geometry.Contains(to, options.HardSafetyMargin + tolerance)
                    || danger.Geometry.IntersectsSegment(from, to, options.HardSafetyMargin)) return false;
            }
        }
        return source && (!requireInitialMargin || initialMargin);
    }

    private static bool MovesInward(IGeometryShape interior, Vector3 from, Vector3 to)
    {
        if (interior is CircleGeometry circle)
        {
            var end = to - circle.Position; end.Y = 0;
            var direction = to - from; direction.Y = 0;
            // Squared distance decreases throughout this straight segment.
            return Vector3.Dot(end, direction) <= 0;
        }
        if (interior is RectangleGeometry rectangle)
        {
            var a = GeometryHelper.WorldToLocal(from, rectangle.Position, rectangle.Rotation);
            var b = GeometryHelper.WorldToLocal(to, rectangle.Position, rectangle.Rotation);
            // Each wall clearance improves monotonically; no lateral tour of the band.
            return b.X * (b.X - a.X) <= 0 && b.Y * (b.Y - a.Y) <= 0;
        }
        return false;
    }
}
