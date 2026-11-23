using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class CaduceusPlatforms
{
    private const float X = 9 * .8660254f;
    private static readonly (int X, int Z)[] grid =
        [(0, 0), (0, 1), (1, 1), (0, 2), (1, 2), (2, 2), (3, 2), (0, 3), (1, 3), (2, 3), (1, 4), (2, 4), (3, 4)];
    private static readonly Vector2[] corners = [new(X, -4.5f), new(X, 4.5f), new(0, 9), new(-X, 4.5f), new(-X, -4.5f), new(0, -9)];
    internal static readonly float[] Heights = [4.5f, .9f, .5f, -.7f, -.3f, .1f, .5f, 1.7f, 1.3f, .9f, 2.1f, 2.5f, 4.9f];
    internal static readonly (int Lower, int Upper)[] HighEdges = [(1, 0), (3, 7), (4, 7), (9, 12), (11, 12)];
    internal static readonly (int Lower, int Upper)[] JumpEdges = [(3, 1), (4, 1), (4, 2), (4, 8), (5, 8), (5, 9), (8, 10), (8, 11), (9, 11)];
    internal static readonly Vector2[][] Polygons = Enumerable.Range(0, 13).Select(Polygon).ToArray();

    internal static Vector2 Center(int index)
    {
        var (x, z) = grid[index];
        return new Vector2(.6f - x * X * 2 - (z % 2) * X, -374 - z * 13.5f);
    }

    private static Vector2[] Polygon(int index)
    {
        if (index < 12) return corners.Select(c => Center(index) + c).ToArray();
        var offset = new Vector2(3.5f, 2);
        var center = Center(12) - offset;
        Vector2[] half = [new(offset.X, -offset.Y - 9), new(offset.X + X, -offset.Y - 4.5f),
            new(offset.X + X, offset.Y + 4.5f), new(offset.X, offset.Y + 9)];
        return half.Select(c => center + c).Concat(half.Select(c => center - c)).ToArray();
    }

    internal static (Vector2 A, Vector2 B) Edge(int from, int to)
    {
        var (x1, z1) = grid[from]; var (x2, z2) = grid[to];
        var (a, b) = (x2 - x1, z2 - z1, z1 & 1) switch
        {
            (-1, 0, _) => (0, 1),
            (-1, -1, 0) or (0, -1, 1) => (1, 2),
            (0, -1, 0) or (1, -1, 1) => (2, 3),
            (1, 0, _) => (3, 4),
            (0, 1, 0) or (1, 1, 1) => (4, 5),
            (-1, 1, 0) or (0, 1, 1) => (5, 0),
            _ => throw new ArgumentException("Nonadjacent Caduceus platforms."),
        };
        return (Center(from) + corners[a], Center(from) + corners[b]);
    }

    internal static bool WantJump(Vector2 position, Vector2 direction)
    {
        if (direction.LengthSquared() <= .000001f) return false;
        direction = Vector2.Normalize(direction);
        foreach (var (lower, upper) in JumpEdges)
        {
            var (a, b) = Edge(lower, upper);
            var e = Vector2.Normalize(b - a);
            // BMR WDir.OrthoL = (Z, -X), in FFXIV X/Z coordinates.
            var n = new Vector2(e.Y, -e.X);
            var dot = Vector2.Dot(direction, n);
            if (dot < .05f) continue;
            var t = Vector2.Dot(n, a - position) / dot;
            if (t < 0 || t > 2.5f) continue;
            var normal = new Vector2(direction.Y, -direction.X);
            var along = Vector2.Dot(normal, position - a) / Vector2.Dot(e, normal);
            if (along >= 0 && along <= Vector2.Distance(a, b)) return true;
        }
        return false;
    }

    internal static float Distance(Vector2 point, int index)
    {
        var polygon = Polygons[index];
        var distanceToPolygon = float.MinValue;
        for (var i = 0; i < polygon.Length; ++i)
        {
            var a = polygon[i]; var edge = polygon[(i + 1) % polygon.Length] - a;
            var relative = point - a;
            var distance = (edge.X * relative.Y - edge.Y * relative.X) / edge.Length();
            distanceToPolygon = Math.Max(distanceToPolygon, -distance);
        }
        return distanceToPolygon;
    }
}
