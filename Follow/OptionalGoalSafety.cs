using System.Numerics;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

namespace MasterPlayer.Follow;

internal static class OptionalGoalSafety
{
    // BMR NavigationDecision: prioritize escape, and abandon mostly covered uptime goals.
    public const float MinSafeFraction = 0.25f;
    public const float UrgentSeconds = 1;
    private static readonly SolveOptions HostDefaults = new();
    public static float Clearance => HostDefaults.HardSafetyMargin;
    public static float SampleStep => HostDefaults.GridResolution;
    public static bool Pending(DateTime start, DateTime? end, DateTime now)
        => start != DateTime.MaxValue && (end == null || (end.Value - now).TotalSeconds >= -HostDefaults.DangerEndSafetyTime);
    public static bool Urgent(DateTime start, DateTime? end, DateTime now)
        => Pending(start, end, now) && (start - now).TotalSeconds <= UrgentSeconds + HostDefaults.DangerStartSafetyTime;

    public static bool NeedsEscape(SafeZoneSnapshot snapshot, Vector3 player, DateTime now)
        => snapshot.HardObstacles.Any(a => a.Geometry.Contains(player))
            || (snapshot.ArenaBoundary is { } boundary && !boundary.Contains(player))
            || snapshot.DangerAreas.Any(a => a.DangerWindows.Any(w => Urgent(w.DangerFrom, w.DangerUntil, now))
                && new GoalExclusion(a.Geometry).Contains(player));
}

// Immutable geometry with clearance, used only to filter optional goals. Never changes danger geometry.
internal sealed class GoalExclusion
{
    private readonly IGeometryShape _shape;
    private readonly GeometryBounds _bounds;
    private readonly (Vector3 Start, Vector3 End)[] _edges;

    public GoalExclusion(IGeometryShape shape)
    {
        _shape = shape;
        _bounds = shape.GetBounds().Expand(OptionalGoalSafety.Clearance);
        _edges = shape switch
        {
            CircleGeometry or DonutGeometry or FanGeometry or CircleComplementGeometry => [],
            FrozenConstraintGeometry grid => grid.GetBoundarySegments().ToArray(),
            _ => shape.GetContours().SelectMany(c => c.Select((p, i) => (p, c[(i + 1) % c.Count]))).ToArray(),
        };
    }

    public bool Contains(Vector3 point, float epsilon = 0.0001f)
    {
        if (!_bounds.Contains(point, epsilon)) return false;
        var margin = OptionalGoalSafety.Clearance + epsilon;
        var distance = Vector2.Distance(new(point.X, point.Z), new(_shape.Position.X, _shape.Position.Z));
        if (_shape is CircleGeometry circle) return distance <= circle.Radius + margin;
        if (_shape is DonutGeometry donut) return distance >= donut.InnerRadius - margin && distance <= donut.OuterRadius + margin;
        if (_shape is CircleComplementGeometry complement) return complement.Contains(point, margin);
        if (_shape.Contains(point, epsilon)) return true;
        if (_shape is FanGeometry fan)
        {
            var delta = MathF.Abs(MathF.IEEERemainder(MathF.Atan2(point.X - fan.Position.X, point.Z - fan.Position.Z) - fan.Rotation, MathF.Tau));
            if (delta <= fan.HalfAngleRadians && Math.Abs(distance - fan.Radius) <= margin) return true;
            var left = fan.Rotation - fan.HalfAngleRadians;
            var right = fan.Rotation + fan.HalfAngleRadians;
            return NearSegment(point, fan.Position, fan.Position + new Vector3(MathF.Sin(left),0,MathF.Cos(left)) * fan.Radius, margin)
                || NearSegment(point, fan.Position, fan.Position + new Vector3(MathF.Sin(right),0,MathF.Cos(right)) * fan.Radius, margin);
        }
        foreach (var edge in _edges)
            if (NearSegment(point, edge.Start, edge.End, margin)) return true;
        // Unknown opaque shapes cannot promise clearance; abandon optional uptime within their bounds.
        return _edges.Length == 0 && _shape is not FrozenConstraintGeometry;
    }

    private static bool NearSegment(Vector3 point, Vector3 start, Vector3 end, float margin)
    {
        var delta = new Vector2(end.X - start.X, end.Z - start.Z);
        var offset = new Vector2(point.X - start.X, point.Z - start.Z);
        var t = delta.LengthSquared() > 1e-10f ? Math.Clamp(Vector2.Dot(offset,delta) / delta.LengthSquared(),0,1) : 0;
        return (offset - delta * t).LengthSquared() <= margin * margin;
    }
}
