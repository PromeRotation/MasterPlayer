using System.Numerics;
using Clipper2Lib;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

// Display/Check keep the regular polygon islands; only the forbidden distance
// is inverted. Order uses twenty sides, DAL2's voidzones use forty.
internal sealed record BozjaOrderTemplate : GeometryTemplate
{
    private const float SCALE = 1048576f;
    private readonly Vector2[][] contours;
    private readonly Vector2[][] triangles;
    internal Vector3 Origin { get; }
    internal bool Inverted { get; }

    internal BozjaOrderTemplate(Vector3 origin, IEnumerable<Vector3> towers, bool inverted,
        float radius = 5, int sides = 20)
    {
        Origin = origin;
        Inverted = inverted;
        var paths = new Paths64();
        foreach (var tower in towers)
        {
            var path = new Path64();
            for (var i = 0; i < sides; ++i)
            {
                var (sin, cos) = Math.SinCos(i * (MathF.Tau / sides));
                path.Add(new Point64((radius * (float)sin + (tower.X - origin.X)) * SCALE,
                    (radius * (float)cos + (tower.Z - origin.Z)) * SCALE));
            }
            var normalize = new Clipper64 { PreserveCollinear = false };
            normalize.AddSubject([path]);
            var single = new Paths64();
            if (!normalize.Execute(ClipType.Union, FillRule.NonZero, single))
                throw new InvalidOperationException("Order tower polygon normalization failed.");
            paths.AddRange(single.Select(p => new Path64(p.Select(v =>
                new Point64((v.X / SCALE) * SCALE, (v.Y / SCALE) * SCALE)))));
        }
        var combined = new Clipper64 { PreserveCollinear = false };
        combined.AddSubject(paths);
        var output = new Paths64();
        if (!combined.Execute(ClipType.Union, FillRule.NonZero, output))
            throw new InvalidOperationException("Order tower polygon union failed.");
        contours = output.Select(p => p.Select(v => new Vector2(v.X / SCALE, v.Y / SCALE)).ToArray()).ToArray();
        triangles = DungeonPolygonFill.Create(contours);
    }

    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new PolygonRegionGeometry(position, contours, [], triangles);

    internal bool Check(Vector3 point)
    {
        const float EPSILON = 1e-7f;
        var x = point.X - Origin.X;
        var y = point.Z - Origin.Z;
        var inside = false;
        foreach (var polygon in contours)
            for (var i = 0; i < polygon.Length; ++i)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Length];
                var minX = Math.Min(a.X, b.X);
                var maxX = Math.Max(a.X, b.X);
                if (MathF.Abs(a.Y - b.Y) <= EPSILON)
                {
                    if (MathF.Abs(y - a.Y) <= EPSILON && x >= minX - EPSILON && x <= maxX + EPSILON) return true;
                    continue;
                }
                if (a.Y > b.Y) (a, b) = (b, a);
                // PolygonBoundaryIndex2D excludes the upper endpoint; public
                // MP polygon display geometry includes all boundary vertices.
                if (y < a.Y || y >= b.Y) continue;
                var slope = (b.X - a.X) / (b.Y - a.Y);
                var intercept = a.X - slope * a.Y;
                var crossing = MathF.FusedMultiplyAdd(slope, y, intercept);
                if (MathF.Abs(x - crossing) <= EPSILON && x >= minX - EPSILON && x <= maxX + EPSILON) return true;
                if (crossing > x) inside = !inside;
            }
        return inside;
    }

    internal float Distance(float x, float z)
    {
        var inside = Check(new(x, Origin.Y, z));
        if (inside != Inverted) return 0;
        var distance = float.MaxValue;
        var cellX = MathF.Floor(x * .2f);
        var cellZ = MathF.Floor(z * .2f);
        foreach (var contour in contours)
            for (var i = 0; i < contour.Length; ++i)
            {
                // SDPolygonWithHolesBase builds prev->current edges and uses
                // scalar float operations. Reversing an edge changes rounding.
                var previous = contour[(i + contour.Length - 1) % contour.Length];
                var current = contour[i];
                var ax = Origin.X + previous.X;
                var az = Origin.Z + previous.Y;
                var dx = current.X - previous.X;
                var dz = current.Y - previous.Y;
                // The source distance queries only edges indexed in this
                // five-yalm cell, including its empty-cell MaxValue result.
                var bx = ax + dx;
                var bz = az + dz;
                if (cellX < MathF.Floor(Math.Min(ax, bx) * .2f) || cellX > MathF.Floor(Math.Max(ax, bx) * .2f)
                    || cellZ < MathF.Floor(Math.Min(az, bz) * .2f) || cellZ > MathF.Floor(Math.Max(az, bz) * .2f))
                    continue;
                var t = Math.Clamp(((x - ax) * dx + (z - az) * dz) * (1f / (dx * dx + dz * dz + 1e-8f)), 0, 1);
                var distanceX = x - (ax + t * dx);
                var distanceZ = z - (az + t * dz);
                distance = MathF.Min(distance, distanceX * distanceX + distanceZ * distanceZ);
            }
        return MathF.Sqrt(distance);
    }
}
