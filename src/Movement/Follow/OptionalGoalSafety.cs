using System.Numerics;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

namespace MasterPlayer.Follow;

internal static class OptionalGoalSafety
{
    // Prioritize escape, and abandon mostly covered uptime goals.
    public const float MinSafeFraction = 0.25f;
    public const float UrgentSeconds = 1;
    private static readonly SolveOptions HostDefaults = new();
    public static float Clearance => HostDefaults.HardSafetyMargin;
    public static float SampleStep => HostDefaults.GridResolution;
    public static bool Pending(DateTime start, DateTime? end, DateTime now)
        => start != DateTime.MaxValue && (end == null || (end.Value - now).TotalSeconds >= -HostDefaults.DangerEndSafetyTime);
    public static bool Urgent(DateTime start, DateTime? end, DateTime now)
        => Pending(start, end, now) && (start - now).TotalSeconds <= UrgentSeconds + HostDefaults.DangerStartSafetyTime;

    // 近战位只在判定前这么多秒才让出：够走出 6 米圈（含余量）的时间，之前继续打。
    public static float FollowMaskLeadSeconds = 2.5f; // 离线模拟扫参数时可改
    public static bool MasksFollowGoal(DateTime start, DateTime? end, DateTime now, IGeometryShape geometry)
        => Pending(start, end, now) && (IsLarge(geometry) || (start - now).TotalSeconds <= FollowMaskLeadSeconds);

    // 大范围（40 米扇形、长直线、大圆）2.5 秒走不出去：读条一开始就让出近战位。
    // 离线模拟：食人魔燃烧猛击 40 米 63° 扇形要提前约 5 秒才躲得开。
    private static bool IsLarge(IGeometryShape geometry) => geometry switch
    {
        FanGeometry fan => fan.Radius >= 15f,
        CircleGeometry circle => circle.Radius >= 10f,
        DonutGeometry donut => donut.OuterRadius - donut.InnerRadius >= 10f,
        _ => geometry.GetBounds() is var b && MathF.Max(b.MaxX - b.MinX, b.MaxZ - b.MinZ) >= 20f,
    };

    public static bool NeedsEscape(SafeZoneSnapshot snapshot, Vector3 player, DateTime now)
        => snapshot.HardObstacles.Any(a => a.Geometry.Contains(player))
            || (snapshot.ArenaBoundary is { } boundary && !boundary.Contains(player))
            || snapshot.DangerAreas.Any(a => a.DangerWindows.Any(w => Urgent(w.DangerFrom, w.DangerUntil, now))
                && new GoalExclusion(a.Geometry).Contains(player));
}

internal static class PositionSafety
{
    private static readonly SolveOptions HostDefaults = new();
    // 点在 [from, until] 内不与任何危险窗口（含余量）、硬障碍重叠，且在场地边界内。
    public static bool IsSafe(SafeZoneSnapshot snapshot, Vector3 point, DateTime from, DateTime until)
        => !snapshot.HardObstacles.Any(a => a.Geometry.Contains(point))
            && (snapshot.ArenaBoundary is not { } boundary || boundary.Contains(point))
            && !snapshot.DangerAreas.Any(a => a.DangerWindows.Any(w => w.DangerFrom != DateTime.MaxValue
                    && w.Buffered(HostDefaults.DangerStartSafetyTime, HostDefaults.DangerEndSafetyTime).OverlapsInterval(from, until))
                && new GoalExclusion(a.Geometry).Contains(point));
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
