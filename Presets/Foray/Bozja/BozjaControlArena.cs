using System.Numerics;
using Clipper2Lib;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Bozja;

// Model keeps the boolean holes. The existing host consumes only the exterior
// with most vertices, including any notch where a tower intersects the border.
internal sealed class BozjaControlArena : GeometryShape
{
    private const float SCALE = 1048576f;
    private readonly PolygonRegionGeometry region;
    internal WorldPolygonGeometry HostBoundary { get; }
    internal float Radius { get; }

    internal BozjaControlArena(Vector3 center, IReadOnlyList<Vector3> towers) : base(center)
    {
        Paths64 Polygon(Vector3 p, float radius, int edges)
        {
            var path = new Path64();
            for (var i = 0; i < edges; ++i)
            {
                var (sin, cos) = Math.SinCos(i * (2 * MathF.PI / edges));
                path.Add(new Point64((radius * (float)sin + p.X) * SCALE,
                    (radius * (float)cos + p.Z) * SCALE));
            }
            var simplify = new Clipper64 { PreserveCollinear = false };
            simplify.AddSubject([path]);
            var result = new Paths64();
            if (!simplify.Execute(ClipType.Union, FillRule.NonZero, result))
                throw new InvalidOperationException("Control tower polygon normalization failed.");
            return new(result.Select(poly => new Path64(poly.Select(v =>
                new Point64((v.X / SCALE) * SCALE, (v.Y / SCALE) * SCALE)))));
        }
        var clipper = new Clipper64 { PreserveCollinear = false };
        clipper.AddSubject(Polygon(center, 29.5f, 32));
        foreach (var tower in towers) clipper.AddClip(Polygon(tower, 5.5f, 20));
        var tree = new PolyTree64();
        if (!clipper.Execute(ClipType.Difference, FillRule.NonZero, tree))
            throw new InvalidOperationException("Control tower arena difference failed.");
        var outside = new List<Vector2[]>();
        var holes = new List<Vector2[]>();
        Collect(tree);
        var vertices = outside.SelectMany(p => p).ToArray();
        var origin = new Vector2((vertices.Min(p => p.X) + vertices.Max(p => p.X)) * .5f,
            (vertices.Min(p => p.Y) + vertices.Max(p => p.Y)) * .5f);
        Radius = Math.Max(vertices.Max(p => p.X) - vertices.Min(p => p.X),
            vertices.Max(p => p.Y) - vertices.Min(p => p.Y)) * .5f;
        Vector2[][] Local(List<Vector2[]> polys) => polys.Select(p => p.Select(v => v - origin).ToArray()).ToArray();
        // Bounds recenters its polygon, but MiniArena.Bounds assignment does
        // not move MiniArena.Center. Source InBounds uses that fixed center;
        // the navigation bridge instead adds custom Bounds.Center.
        region = new(center, Local(outside), Local(holes), []);
        var best = outside.OrderByDescending(p => p.Length).First();
        HostBoundary = new(best.Select(p => new Vector3(p.X, center.Y, p.Y)), center);

        void Collect(PolyPath64 parent)
        {
            for (var i = 0; i < parent.Count; ++i)
            {
                var child = parent[i];
                var points = child.Polygon!.Select(p => new Vector2(p.X / SCALE, p.Y / SCALE)).ToArray();
                (child.IsHole ? holes : outside).Add(points);
                Collect(child);
            }
        }
    }

    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => region.Contains(point, epsilon);
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
        => region.GetContours(segments);
}
