using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class FractalFloorBoundary
{
    internal static int CellIndex(Vector3 position)
    {
        static int Coordinate(float value) => value switch { < -10 => 0, < 0 => 1, < 10 => 2, _ => 3 };
        return Coordinate(position.X) | Coordinate(position.Z + 350) << 2;
    }

    internal static Vector3 CellCenter(int index, float y) => new(-15 + 10 * (index & 3), y, -365 + 10 * (index >> 2));

    internal static IGeometryShape Create(float y, ushort broken)
    {
        if (broken is 0 or ushort.MaxValue) return new RectangleGeometry(new(0, y, -350), 0, 39, 39);
        float[] coordinates = [-19.5f, -10, 0, 10, 19.5f];
        var edges = new HashSet<(Vector2 A, Vector2 B)>();
        var triangles = new List<Vector2[]>();
        bool Safe(int x, int z) => x is >= 0 and < 4 && z is >= 0 and < 4 && (broken & (1 << (x | z << 2))) == 0;
        for (var z = 0; z < 4; ++z)
            for (var x = 0; x < 4; ++x)
            {
                if (!Safe(x, z)) continue;
                var a = new Vector2(coordinates[x], coordinates[z]);
                var b = new Vector2(coordinates[x + 1], coordinates[z]);
                var c = new Vector2(coordinates[x + 1], coordinates[z + 1]);
                var d = new Vector2(coordinates[x], coordinates[z + 1]);
                triangles.Add([a, b, c]); triangles.Add([a, c, d]);
                if (!Safe(x, z - 1)) edges.Add((a, b));
                if (!Safe(x + 1, z)) edges.Add((b, c));
                if (!Safe(x, z + 1)) edges.Add((c, d));
                if (!Safe(x - 1, z)) edges.Add((d, a));
            }
        var exteriors = new List<Vector2[]>();
        var holes = new List<Vector2[]>();
        while (edges.Count != 0)
        {
            var first = edges.First();
            var edge = first;
            var points = new List<Vector2>();
            do
            {
                points.Add(edge.A);
                edges.Remove(edge);
                if (edge.B == first.A) break;
                var direction = edge.B - edge.A;
                // At diagonal contacts keep each island's interior on the left.
                edge = edges.Where(e => e.A == edge.B).OrderByDescending(e =>
                {
                    var next = e.B - e.A;
                    return MathF.Atan2(direction.X * next.Y - direction.Y * next.X, Vector2.Dot(direction, next));
                }).First();
            } while (true);
            AddLoops(points);
        }
        return new PolygonRegionGeometry(new(0, y, -350), exteriors.ToArray(), holes.ToArray(), triangles.ToArray());

        void AddLoops(List<Vector2> points)
        {
            var visited = new Dictionary<Vector2, int>();
            for (var i = 0; i < points.Count; ++i)
            {
                if (visited.TryAdd(points[i], i)) continue;
                var start = visited[points[i]];
                // Diagonally touching holes can share a vertex with the exterior.
                // Store simple rings so polygon validation and world drawing agree.
                AddLoops(points.GetRange(start, i - start));
                AddLoops(points.Take(start).Concat(points.Skip(i)).ToList());
                return;
            }
            var area = 0f;
            for (var i = 0; i < points.Count; ++i)
            {
                var a = points[i]; var b = points[(i + 1) % points.Count];
                area += a.X * b.Y - a.Y * b.X;
            }
            (area > 0 ? exteriors : holes).Add(points.ToArray());
        }
    }
}
