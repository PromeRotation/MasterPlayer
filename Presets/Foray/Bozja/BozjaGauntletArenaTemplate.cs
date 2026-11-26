using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed record BozjaGauntletArenaTemplate : GeometryTemplate
{
    private static readonly PolygonRegionGeometry SHAPE = (PolygonRegionGeometry)DalriadaArenas.GauntletHazard(0);
    private static readonly Vector2[][] CONTOURS = SHAPE.GetContours().Select(c => c.Select(p =>
        new Vector2(p.X - SHAPE.Position.X, p.Z - SHAPE.Position.Z)).ToArray()).ToArray();
    public override GeometryKind Kind => GeometryKind.WorldPolygon;

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => DalriadaArenas.GauntletHazard(position.Y);

    internal float Distance(float x, float z)
    {
        if (Check(new(x, 0, z))) return 0;
        var distanceSquared = float.MaxValue;
        var cellX = MathF.Floor(x * .2f);
        var cellZ = MathF.Floor(z * .2f);
        foreach (var contour in CONTOURS)
            for (var i = 0; i < contour.Length; ++i)
            {
                var a = contour[(i + contour.Length - 1) % contour.Length];
                var b = contour[i];
                var dx = b.X - a.X;
                var dz = b.Y - a.Y;
                var ax = SHAPE.Position.X + a.X;
                var az = SHAPE.Position.Z + a.Y;
                var bx = ax + dx;
                var bz = az + dz;
                if (cellX < MathF.Floor(Math.Min(ax, bx) * .2f) || cellX > MathF.Floor(Math.Max(ax, bx) * .2f)
                    || cellZ < MathF.Floor(Math.Min(az, bz) * .2f) || cellZ > MathF.Floor(Math.Max(az, bz) * .2f)) continue;
                var factor = Math.Clamp(((x - ax) * dx + (z - az) * dz) * (1f / (dx * dx + dz * dz + 1e-8f)), 0, 1);
                var ex = x - (ax + factor * dx);
                var ez = z - (az + factor * dz);
                distanceSquared = MathF.Min(distanceSquared, ex * ex + ez * ez);
            }
        return MathF.Sqrt(distanceSquared);
    }

    internal bool Check(Vector3 point)
    {
        const float EPSILON = 1e-7f;
        var x = point.X - SHAPE.Position.X;
        var y = point.Z - SHAPE.Position.Z;
        var inside = false;
        foreach (var contour in CONTOURS)
            for (var i = 0; i < contour.Length; ++i)
            {
                var a = contour[i];
                var b = contour[(i + 1) % contour.Length];
                var minX = Math.Min(a.X, b.X);
                var maxX = Math.Max(a.X, b.X);
                if (MathF.Abs(a.Y - b.Y) <= EPSILON)
                {
                    if (MathF.Abs(y - a.Y) <= EPSILON && x >= minX - EPSILON && x <= maxX + EPSILON) return true;
                    continue;
                }
                if (a.Y > b.Y) (a, b) = (b, a);
                if (y < a.Y || y >= b.Y) continue;
                var slope = (b.X - a.X) / (b.Y - a.Y);
                var crossing = MathF.FusedMultiplyAdd(slope, y, a.X - slope * a.Y);
                if (MathF.Abs(x - crossing) <= EPSILON && x >= minX - EPSILON && x <= maxX + EPSILON) return true;
                if (crossing > x) inside = !inside;
            }
        return inside;
    }
}
