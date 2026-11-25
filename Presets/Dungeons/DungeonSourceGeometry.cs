using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

// Source text checks intentionally exclude a donut's inner edge and normalize
// cone offsets even at the apex. Navigation keeps the MP geometry contract.
internal static class DungeonSourceGeometry
{
    internal static bool Contains(GeometryTemplate shape, Vector3 origin, float rotation, Vector3 point)
    {
        var offset = new Vector2(point.X - origin.X, point.Z - origin.Z);
        var squared = offset.LengthSquared();
        if (shape is CircleTemplate circle) return squared <= circle.Radius * circle.Radius;
        if (shape is DonutTemplate donut)
            return squared > donut.InnerRadius * donut.InnerRadius && squared <= donut.OuterRadius * donut.OuterRadius;
        if (shape is PtDonutSectorTemplate sector)
            return squared > sector.Inner * sector.Inner && squared <= sector.Outer * sector.Outer
                && Contains(new FanTemplate(sector.Outer, sector.Angle), origin, rotation, point);
        var (sin, cos) = Math.SinCos(rotation);
        var along = offset.X * (float)sin + offset.Y * (float)cos;
        if (shape is FanTemplate fan)
        {
            var inverse = squared > 0 ? 1f / MathF.Sqrt(squared) : 0;
            var normalizedDot = offset.X * inverse * (float)sin + offset.Y * inverse * (float)cos;
            return squared <= fan.Radius * fan.Radius && normalizedDot >= (float)Math.Cos(fan.AngleRadians * .5f);
        }
        if (shape is DungeonCrossTemplate cross)
        {
            var side = offset.X * (float)cos - offset.Y * (float)sin;
            return MathF.Abs(along) <= cross.Length && MathF.Abs(side) <= cross.HalfWidth
                || MathF.Abs(side) <= cross.Length && MathF.Abs(along) <= cross.HalfWidth;
        }
        return shape.Resolve(origin, rotation, null).Contains(point, 0);
    }
}
