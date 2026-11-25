using System.Numerics;
using Clipper2Lib;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

// GoldenBeam folds four two-edge ConeV polygons by intersection and XOR, then
// unions those results. Keep holes in both the navigation region and fill mesh.
internal static class CarnivaleGoldenBeamGeometry
{
    private const float SCALE = 1048576f;

    internal static IGeometryShape Create(Vector3 center, IReadOnlyList<DungeonPendingCast> casts)
    {
        var paths = casts.Select(c => new Paths64 { Cone(center, c) }).ToArray();
        var intersection = Apply(ClipType.Union, paths[0], []);
        var xor = Apply(ClipType.Union, paths[0], []);
        foreach (var path in paths.Skip(1))
        {
            intersection = Apply(ClipType.Intersection, intersection, path);
            xor = Apply(ClipType.Xor, xor, path);
        }
        intersection = Apply(ClipType.Difference, intersection, []);
        xor = Apply(ClipType.Difference, xor, []);
        var clipper = new Clipper64 { PreserveCollinear = false };
        clipper.AddSubject(intersection);
        clipper.AddClip(xor);
        var tree = new PolyTree64();
        if (!clipper.Execute(ClipType.Union, FillRule.NonZero, tree))
            throw new InvalidOperationException("GoldenBeam polygon union failed.");
        var exteriors = new List<Vector2[]>();
        var holes = new List<Vector2[]>();
        AddParts(tree);
        var triangles = Fill(exteriors.Concat(holes).ToArray());
        return new PolygonRegionGeometry(center, exteriors.ToArray(), holes.ToArray(), triangles);

        void AddParts(PolyPath64 parent)
        {
            for (var i = 0; i < parent.Count; ++i)
            {
                var child = parent[i];
                var polygon = Points(child);
                if (child.IsHole)
                    holes.Add(polygon);
                else
                    exteriors.Add(polygon);
                AddParts(child);
            }
        }
    }

    private static Vector2[] Points(PolyPath64 path)
        => path.Polygon!.Select(p => new Vector2(p.X / SCALE, p.Y / SCALE)).ToArray();

    private static Vector2[][] Fill(Vector2[][] contours)
    {
        // Boolean results can touch themselves at a vertex, so a simple-ring ear
        // clipper is insufficient. Between consecutive vertex X coordinates no
        // boundary edges cross: pair scanline intersections to fill trapezoids.
        // This also retains holes and disconnected islands without bridging them.
        var edges = contours.SelectMany(p => p.Select((a, i) => (A: a, B: p[(i + 1) % p.Length])))
            .Where(e => e.A.X != e.B.X).ToArray();
        var xs = contours.SelectMany(p => p).Select(p => p.X).Distinct().Order().ToArray();
        var triangles = new List<Vector2[]>();
        for (var i = 1; i < xs.Length; ++i)
        {
            var left = xs[i - 1];
            var right = xs[i];
            var middle = ((double)left + right) * .5;
            var active = edges.Where(e => Math.Min(e.A.X, e.B.X) < middle && middle < Math.Max(e.A.X, e.B.X))
                .OrderBy(e => Height(e, middle)).ToArray();
            if ((active.Length & 1) != 0)
                throw new InvalidOperationException("GoldenBeam fill has an unmatched boundary edge.");
            for (var j = 0; j < active.Length; j += 2)
            {
                var a = new Vector2(left, (float)Height(active[j], left));
                var b = new Vector2(right, (float)Height(active[j], right));
                var c = new Vector2(right, (float)Height(active[j + 1], right));
                var d = new Vector2(left, (float)Height(active[j + 1], left));
                Add(a, b, c);
                Add(a, c, d);
            }
        }
        return triangles.ToArray();

        static double Height((Vector2 A, Vector2 B) edge, double x) => edge.A.Y
            + (x - edge.A.X) / ((double)edge.B.X - edge.A.X) * ((double)edge.B.Y - edge.A.Y);
        void Add(Vector2 a, Vector2 b, Vector2 c)
        {
            var area = ((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X);
            if (area != 0) triangles.Add([a, b, c]);
        }
    }

    private static Path64 Cone(Vector3 center, DungeonPendingCast cast)
    {
        var halfAngle = 60f * (MathF.PI / 180f);
        var start = cast.Rotation - halfAngle;
        var offset = new Vector2(cast.Location.X - center.X, cast.Location.Z - center.Z);
        var result = new Path64();
        for (var i = 0; i < 3; ++i)
        {
            var (sin, cos) = Math.SinCos(start + i * halfAngle);
            result.Add(new Point64((40f * (float)sin + offset.X) * SCALE, (40f * (float)cos + offset.Y) * SCALE));
        }
        result.Add(new Point64(offset.X * SCALE, offset.Y * SCALE));
        return result;
    }

    private static Paths64 Apply(ClipType operation, Paths64 subject, Paths64 clip)
    {
        var clipper = new Clipper64 { PreserveCollinear = false };
        clipper.AddSubject(subject);
        clipper.AddClip(clip);
        var result = new Paths64();
        if (!clipper.Execute(operation, FillRule.NonZero, result))
            throw new InvalidOperationException("GoldenBeam polygon operation failed.");
        // PolygonClipper converts every intermediate result back through float
        // WDir before assigning it to the next operation.
        return new Paths64(result.Select(path => new Path64(path.Select(p =>
            new Point64((p.X / SCALE) * SCALE, (p.Y / SCALE) * SCALE)))));
    }
}
