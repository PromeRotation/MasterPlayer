using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class DungeonPolygonDifference
{
    internal static IGeometryShape Build(LocalPolygonGeometry original,
        IReadOnlyList<(Vector2[] Inner, Vector2[] Outer)> rings)
    {
        if (rings.Count == 0) return original;
        var center = original.Position;
        var world = original.GetContours()[0];
        var polygon = world.Select(p => new Vector2(p.X - center.X, p.Z - center.Z)).ToArray();
        if (!PolygonTriangulator.TryTriangulate(world, out var indices, .000001f))
            throw new InvalidOperationException("地城默认场地无法三角化。");
        var pieces = new List<Vector2[]>();
        for (var i = 0; i < indices.Length; i += 3)
            pieces.Add([polygon[indices[i]], polygon[indices[i + 1]], polygon[indices[i + 2]]]);
        foreach (var ring in rings)
        {
            var next = new List<Vector2[]>();
            foreach (var piece in pieces)
            {
                // (piece \ outer) union (piece intersect inner) keeps the prison's interior.
                var remaining = piece;
                for (var e = 0; e < ring.Outer.Length && remaining.Length >= 3; ++e)
                {
                    var a = ring.Outer[e]; var b = ring.Outer[(e + 1) % ring.Outer.Length];
                    var outside = Clip(remaining, a, b, false);
                    if (Valid(outside)) next.Add(outside);
                    remaining = Clip(remaining, a, b, true);
                }
                if (ring.Inner.Length >= 3)
                {
                    remaining = piece;
                    for (var e = 0; e < ring.Inner.Length && remaining.Length >= 3; ++e)
                        remaining = Clip(remaining, ring.Inner[e], ring.Inner[(e + 1) % ring.Inner.Length], true);
                    if (Valid(remaining)) next.Add(remaining);
                }
            }
            pieces = next;
        }
        var triangles = new List<Vector2[]>();
        foreach (var p in pieces)
            for (var i = 1; i < p.Length - 1; ++i)
                if (MathF.Abs(Cross(p[i] - p[0], p[i + 1] - p[0])) > .0000001f)
                    triangles.Add([p[0], p[i], p[i + 1]]);

        bool InPolygon(Vector2 p, Vector2[] vertices)
        {
            var inside = false;
            for (var i = 0; i < vertices.Length; ++i)
            {
                var a = vertices[i]; var b = vertices[(i + 1) % vertices.Length];
                if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }
            return inside;
        }
        bool Walkable(Vector2 p) => InPolygon(p, polygon)
            && !rings.Any(r => InPolygon(p, r.Outer) && !InPolygon(p, r.Inner));
        // Only original boundary segments can survive the boolean operation. Split at
        // intersections and keep segments separating walkable and forbidden space;
        // triangulation seams must never become radar/world arena boundaries.
        var contours = new[] { polygon }.Concat(rings.SelectMany(r => new[] { r.Inner, r.Outer }).Where(p => p.Length >= 3)).ToArray();
        var edges = contours.SelectMany(p => Enumerable.Range(0, p.Length).Select(i => (A: p[i], B: p[(i + 1) % p.Length]))).ToArray();
        var boundary = new List<(Vector2 A, Vector2 B)>();
        foreach (var e in edges)
        {
            var direction = e.B - e.A;
            var cuts = new List<float> { 0, 1 };
            foreach (var other in edges)
            {
                var d = other.B - other.A;
                var determinant = Cross(direction, d);
                if (MathF.Abs(determinant) < .000001f) continue;
                var delta = other.A - e.A;
                var t = Cross(delta, d) / determinant;
                var u = Cross(delta, direction) / determinant;
                if (t > 0 && t < 1 && u >= 0 && u <= 1) cuts.Add(t);
            }
            cuts.Sort();
            for (var i = 1; i < cuts.Count; ++i)
            {
                var a = e.A + cuts[i - 1] * direction; var b = e.A + cuts[i] * direction;
                if (Vector2.DistanceSquared(a, b) < .0000000001f) continue;
                var normal = Vector2.Normalize(new Vector2(-(b - a).Y, (b - a).X)) * .00001f;
                var midpoint = (a + b) * .5f;
                var left = Walkable(midpoint + normal); var right = Walkable(midpoint - normal);
                if (left != right) boundary.Add(left ? (a, b) : (b, a));
            }
        }
        static (int X, int Z) Key(Vector2 p) => ((int)MathF.Round(p.X * 100000), (int)MathF.Round(p.Y * 100000));
        boundary = boundary.DistinctBy(e => (Key(e.A), Key(e.B))).ToList();
        var loops = new List<IReadOnlyList<Vector3>>();
        while (boundary.Count > 0)
        {
            var edge = boundary[0]; boundary.RemoveAt(0);
            var loop = new List<Vector2> { edge.A }; var end = edge.B;
            while (Vector2.DistanceSquared(end, loop[0]) > .0000000004f)
            {
                loop.Add(end);
                var i = boundary.FindIndex(e => Vector2.DistanceSquared(e.A, end) <= .0000000004f);
                if (i < 0) throw new InvalidOperationException($"中央终端监狱边界不闭合：{loop[0]} -> {end}，最近边距 {boundary.Select(e => Vector2.Distance(e.A, end)).DefaultIfEmpty(float.MaxValue).Min()}，顶点 {string.Join(";", loop)}。");
                end = boundary[i].B; boundary.RemoveAt(i);
            }
            if (loop.Count >= 3)
                loops.Add(loop.Select(p => center + new Vector3(p.X, 0, p.Y)).ToArray());
        }
        return new PolygonRegionGeometry(center, pieces.ToArray(), [], triangles.ToArray(), loops);
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    private static bool Valid(Vector2[] p)
        => p.Length >= 3 && MathF.Abs(p.Select((a, i) => Cross(a, p[(i + 1) % p.Length])).Sum()) > .0000001f;

    private static Vector2[] Clip(Vector2[] polygon, Vector2 a, Vector2 b, bool inside)
    {
        if (polygon.Length == 0) return [];
        var result = new List<Vector2>();
        var previous = polygon[^1];
        var oldDistance = Cross(b - a, previous - a) * (inside ? 1 : -1);
        foreach (var current in polygon)
        {
            var distance = Cross(b - a, current - a) * (inside ? 1 : -1);
            if ((distance >= 0) != (oldDistance >= 0))
                result.Add(previous + (current - previous) * (oldDistance / (oldDistance - distance)));
            if (distance >= 0) result.Add(current);
            previous = current; oldDistance = distance;
        }
        // Clipping through a vertex can emit the same point twice.
        for (var i = result.Count - 1; i >= 0 && result.Count > 1; --i)
            if (Vector2.DistanceSquared(result[i], result[(i + 1) % result.Count]) < .0000000001f) result.RemoveAt(i);
        return result.ToArray();
    }
}
