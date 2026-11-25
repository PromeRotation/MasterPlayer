using System.Numerics;
using Clipper2Lib;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

// The native output adapter retains real islands/holes and a matching fill mesh.
internal static class QuantumGeometry
{
    private const float SCALE = 1048576f;
    private static readonly (float X, float Z, float Degrees)[] PILLARS =
    [
        (-600f, -306.99991f, 0), (-596.50012f, -306.06201f, -30),
        (-593.93793f, -303.49991f, -60), (-592.99988f, -299.99991f, -89.98f),
        (-593.93781f, -296.49991f, -120), (-596.50012f, -293.93771f, -150),
        (-600f, -293f, 0), (-603.5f, -293.93781f, 150),
        (-606.06219f, -296.5f, 120), (-607.00012f, -300f, 89.98f),
        (-606.06219f, -303.5f, 60), (-603.5f, -306.06219f, 30),
        (-600.00018f, -307f, 0), (-596.50012f, -306.06201f, 150),
        (-593.93799f, -303.49991f, 120), (-592.99988f, -299.99991f, 89.98f),
        (-593.93793f, -296.49991f, 60), (-596.50018f, -293.93771f, 30),
        (-600.00018f, -293f, 0), (-603.50018f, -293.9379f, -30),
        (-606.06219f, -296.5f, -60), (-607.00012f, -300f, -89.98f),
        (-606.06219f, -303.5f, -120), (-603.5f, -306.06219f, -150),
    ];

    internal static IGeometryShape Floor { get; } = new RectangleGeometry(QuantumEncounter.CENTER, 0, 40, 30);
    internal static IGeometryShape RestoredFloor { get; } = QuantumArenaData.Initial(0);
    internal static IGeometryShape Light { get; } = Region(PtArenaData.LightPuddles, []);
    internal static IGeometryShape Dark { get; } = Region(PtArenaData.DarkPuddles, []);
    internal static IGeometryShape LightInverse { get; } = Region([RestoredFloor], [Light]);
    internal static IGeometryShape DarkInverse { get; } = Region([RestoredFloor], [Dark]);

    internal static IGeometryShape Arena(IEnumerable<int> pillars)
        => Region([Floor], pillars.Select(Pillar).ToArray());

    private static IGeometryShape Pillar(int index)
    {
        var (x, z, degrees) = PILLARS[index];
        var center = new Vector3(x, 0, z);
        var rotation = degrees * (MathF.PI / 180f);
        // Source Polygon computes local offsets in double trig, then narrows once.
        return new WorldPolygonGeometry(Enumerable.Range(0, 64).Select(i =>
        {
            var angle = rotation + i * (MathF.Tau / 64);
            var (sin, cos) = Math.SinCos(angle);
            return center + new Vector3((float)sin * 2.57745f, 0, (float)cos * 2.57745f);
        }));
    }

    internal static IGeometryShape Refuge(IReadOnlyList<(Vector3 Direction, Vector3 Position)> crystals)
        => Region([Floor], crystals.Select(c => (IGeometryShape)new RectangleGeometry(c.Position,
            MathF.Atan2(c.Direction.X, c.Direction.Z), 10, 80)).ToArray());

    internal static IGeometryShape Region(IGeometryShape[] include, IGeometryShape[] exclude)
    {
        var center = QuantumEncounter.CENTER;
        var clipper = new Clipper64 { PreserveCollinear = false };
        clipper.AddSubject(Paths(include));
        clipper.AddClip(Paths(exclude));
        var tree = new PolyTree64();
        if (!clipper.Execute(ClipType.Difference, FillRule.NonZero, tree))
            throw new InvalidOperationException("Quantum polygon difference failed.");
        List<Vector2[]> exteriors = [];
        List<Vector2[]> holes = [];
        Add(tree);
        var contours = exteriors.Concat(holes).ToArray();
        return new PolygonRegionGeometry(center, exteriors.ToArray(), holes.ToArray(), Fill(contours));

        Paths64 Paths(IGeometryShape[] shapes)
        {
            var result = new Paths64();
            foreach (var shape in shapes)
                foreach (var contour in shape.GetContours())
                {
                    var path = new Path64(contour.Select(p => new Point64(
                        (p.X - center.X) * SCALE, (p.Z - center.Z) * SCALE)));
                    result.Add(path);
                }
            return result;
        }
        void Add(PolyPath64 parent)
        {
            for (var i = 0; i < parent.Count; ++i)
            {
                var child = parent[i];
                var points = child.Polygon!.Select(p => new Vector2(p.X / SCALE, p.Y / SCALE)).ToArray();
                (child.IsHole ? holes : exteriors).Add(points);
                Add(child);
            }
        }
    }

    private static Vector2[][] Fill(Vector2[][] contours)
    {
        var edges = contours.SelectMany(p => p.Select((a, i) => (A: a, B: p[(i + 1) % p.Length])))
            .Where(e => e.A.X != e.B.X).ToArray();
        var xs = contours.SelectMany(p => p).Select(p => p.X).Distinct().Order().ToArray();
        List<Vector2[]> triangles = [];
        for (var i = 1; i < xs.Length; ++i)
        {
            var left = xs[i - 1];
            var right = xs[i];
            var middle = ((double)left + right) * .5;
            var active = edges.Where(e => Math.Min(e.A.X, e.B.X) < middle && middle < Math.Max(e.A.X, e.B.X))
                .OrderBy(e => Height(e, middle)).ToArray();
            if ((active.Length & 1) != 0) throw new InvalidOperationException("Quantum polygon fill has an unmatched edge.");
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

        static double Height((Vector2 A, Vector2 B) edge, double x)
            => edge.A.Y + (x - edge.A.X) / ((double)edge.B.X - edge.A.X) * ((double)edge.B.Y - edge.A.Y);
        void Add(Vector2 a, Vector2 b, Vector2 c)
        {
            var area = ((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X);
            if (area != 0) triangles.Add([a, b, c]);
        }
    }
}
