using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class TroiaDynamicArena
{
    internal static readonly Vector3 Center = new(-35, 0, -298);
    internal static readonly Vector2[] WallCenters =
    [
        new(-41.511f, -279.09f), new(-49.142f, -283.858f), new(-53.91f, -291.489f),
        new(-53.91f, -304.511f), new(-49.142f, -312.142f), new(-41.511f, -316.91f),
        new(-28.489f, -316.911f), new(-20.858f, -312.142f), new(-16.09f, -304.511f),
        new(-16.09f, -291.489f), new(-20.858f, -283.858f), new(-28.489f, -279.09f),
    ];
    private static readonly IGeometryShape[] walls =
    [
        TroiaArenas.ScarmiglioneWall0(0), TroiaArenas.ScarmiglioneWall1(0), TroiaArenas.ScarmiglioneWall2(0),
        TroiaArenas.ScarmiglioneWall3(0), TroiaArenas.ScarmiglioneWall4(0), TroiaArenas.ScarmiglioneWall5(0),
        TroiaArenas.ScarmiglioneWall6(0), TroiaArenas.ScarmiglioneWall7(0), TroiaArenas.ScarmiglioneWall8(0),
        TroiaArenas.ScarmiglioneWall9(0), TroiaArenas.ScarmiglioneWall10(0), TroiaArenas.ScarmiglioneWall11(0),
    ];

    internal static IGeometryShape Create(float y, int mask, bool large)
    {
        var origin = Center with { Y = y };
        var baseShape = large ? TroiaArenas.ScarmiglioneLargeBase(0) : TroiaArenas.ScarmiglioneSmallBase(0);
        var circle = baseShape.GetContours()[0].Select(p => new Vector2(p.X, p.Z)).ToArray();
        var selected = Enumerable.Range(0, 12).Where(i => (mask & (1 << i)) != 0).Select(i => walls[i]).ToArray();
        var rectangles = selected.Select(s => s.GetContours()[0].Select(p => new Vector2(p.X, p.Z)).ToArray()).ToArray();
        var contours = Boundary(circle, rectangles, baseShape, selected);
        Vector2[] Local(IEnumerable<Vector2> p) => p.Select(v => v - new Vector2(Center.X, Center.Z)).ToArray();
        if (!large) return new LocalPolygonGeometry(origin, 0, Local(contours.Single()).Select(p => -p));
        List<List<Vector2>> pieces = [circle.ToList()];
        foreach (var rectangle in rectangles) pieces = pieces.SelectMany(p => Subtract(p, rectangle)).ToList();
        var triangles = pieces.SelectMany(p => Enumerable.Range(1, p.Count - 2)
            .Where(i => MathF.Abs(Cross(p[i] - p[0], p[i + 1] - p[0])) > 2 * GeometryHelper.DefaultEpsilon
                && Vector2.DistanceSquared(p[0], p[i]) > GeometryHelper.DefaultEpsilon * GeometryHelper.DefaultEpsilon
                && Vector2.DistanceSquared(p[0], p[i + 1]) > GeometryHelper.DefaultEpsilon * GeometryHelper.DefaultEpsilon
                && Vector2.DistanceSquared(p[i], p[i + 1]) > GeometryHelper.DefaultEpsilon * GeometryHelper.DefaultEpsilon)
            .Select(i => Local([p[0], p[i], p[i + 1]]))).ToArray();
        return new PolygonRegionGeometry(origin, [Local(contours.MaxBy(c => MathF.Abs(Area(c)))!)],
            contours.Where(c => Area(c) * Area(circle) < 0).Select(Local).ToArray(), triangles);
    }

    private static List<Vector2[]> Boundary(Vector2[] circle, Vector2[][] rectangles, IGeometryShape baseShape, IGeometryShape[] selected)
    {
        var all = new[] { circle }.Concat(rectangles).ToArray();
        var segments = new List<(Vector2 A, Vector2 B)>();
        for (var i = 0; i < all.Length; ++i)
            for (var edge = 0; edge < all[i].Length; ++edge)
            {
                var a = all[i][edge]; var b = all[i][(edge + 1) % all[i].Length];
                var direction = b - a;
                var splits = new List<float> { 0, 1 };
                for (var j = 0; j < all.Length; ++j)
                {
                    if (i == j) continue;
                    for (var k = 0; k < all[j].Length; ++k)
                    {
                        var c = all[j][k]; var d = all[j][(k + 1) % all[j].Length];
                        var determinant = Cross(direction, d - c);
                        if (MathF.Abs(determinant) < .000001f) continue;
                        var t = Cross(c - a, d - c) / determinant;
                        var u = Cross(c - a, direction) / determinant;
                        if (t is > 0 and < 1 && u is >= 0 and <= 1) splits.Add(t);
                    }
                }
                var sorted = splits.Distinct().Order().ToArray();
                for (var k = 0; k + 1 < sorted.Length; ++k)
                {
                    var p = a + direction * sorted[k]; var q = a + direction * sorted[k + 1];
                    if (Vector2.DistanceSquared(p, q) <= .00000001f) continue;
                    var middle = (p + q) * .5f;
                    var world = new Vector3(middle.X, 0, middle.Y);
                    if (i != 0 && !baseShape.Contains(world, .00002f)) continue;
                    if (selected.Where((s, j) => j != i - 1).Any(s => s.Contains(world, 0))) continue;
                    if (i != 0 && Area(all[i]) * Area(circle) > 0) (p, q) = (q, p);
                    segments.Add((p, q));
                }
            }
        var result = new List<Vector2[]>();
        while (segments.Count > 0)
        {
            var first = segments[0]; segments.RemoveAt(0);
            var points = new List<Vector2> { first.A };
            var end = first.B;
            while (Vector2.DistanceSquared(end, points[0]) > .00000004f)
            {
                points.Add(end);
                var next = segments.FindIndex(s => Vector2.DistanceSquared(s.A, end) <= .00000004f);
                if (next < 0) throw new InvalidOperationException("特罗亚场地轮廓未闭合。");
                end = segments[next].B; segments.RemoveAt(next);
            }
            result.Add(points.ToArray());
        }
        return result;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    private static float Area(Vector2[] polygon) => Enumerable.Range(0, polygon.Length)
        .Sum(i => Cross(polygon[i], polygon[(i + 1) % polygon.Length]));

    private static IEnumerable<List<Vector2>> Subtract(List<Vector2> polygon, Vector2[] hole)
    {
        var signedArea = 0f;
        for (var i = 0; i < hole.Length; ++i)
            signedArea += hole[i].X * hole[(i + 1) % hole.Length].Y - hole[(i + 1) % hole.Length].X * hole[i].Y;
        var clockwise = signedArea < 0;
        for (var i = 0; i < hole.Length && polygon.Count >= 3; ++i)
        {
            var a = hole[i]; var b = hole[(i + 1) % hole.Length];
            var outside = Clip(polygon, a, b, !clockwise);
            if (outside.Count >= 3) yield return outside;
            polygon = Clip(polygon, a, b, clockwise);
        }
    }
    private static List<Vector2> Clip(List<Vector2> polygon, Vector2 a, Vector2 b, bool negative)
    {
        var output = new List<Vector2>();
        for (var i = 0; i < polygon.Count; ++i)
        {
            var p = polygon[i]; var q = polygon[(i + 1) % polygon.Count];
            var d1 = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            var d2 = (b.X - a.X) * (q.Y - a.Y) - (b.Y - a.Y) * (q.X - a.X);
            var keep1 = negative ? d1 <= 0 : d1 >= 0; var keep2 = negative ? d2 <= 0 : d2 >= 0;
            if (keep1) output.Add(p);
            if (keep1 != keep2) output.Add(Vector2.Lerp(p, q, d1 / (d1 - d2)));
        }
        return output;
    }
}
