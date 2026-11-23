using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class XelphatolKnockback
{
    internal static readonly Vector3 CENTER = new(245.28799f, 0, 13.62114f);
    internal static readonly (Vector2 A, Vector2 B)[] WALLS = BuildWalls();

    private static (Vector2 A, Vector2 B)[] BuildWalls()
    {
        var result = new List<(Vector2, Vector2)>();
        Vector2 center = new(CENTER.X, CENTER.Z);
        for (var i = 0; i < 4; ++i)
        {
            var angle = i * MathF.PI / 2;
            Vector2 Rotate(Vector2 p)
            {
                p -= center;
                return center + new Vector2(MathF.Cos(angle) * p.X - MathF.Sin(angle) * p.Y,
                    MathF.Sin(angle) * p.X + MathF.Cos(angle) * p.Y);
            }
            result.Add((Rotate(new(227.487f, 16.825f)), Rotate(new(226.567f, 13.39f))));
            result.Add((Rotate(new(226.567f, 13.39f)), Rotate(new(227.392f, 10.301f))));
        }
        return result.ToArray();
    }

    internal static Vector3 Landing(IGeometryShape arena, Vector3 origin, Vector3 point, float hitbox)
    {
        var delta = new Vector2(point.X - origin.X, point.Z - origin.Z);
        if (delta.LengthSquared() < .000001f) return point;
        var direction = Vector2.Normalize(delta);
        Vector2 start = new(point.X, point.Z);
        static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
        var distance = 30f;
        var wallDistance = float.MaxValue;
        foreach (var (a, b) in WALLS)
        {
            var segment = b - a;
            var denominator = Cross(direction, segment);
            if (MathF.Abs(denominator) < .000001f) continue;
            var t = Cross(a - start, segment) / denominator;
            var u = Cross(a - start, direction) / denominator;
            if (t >= 0 && t <= 30 && u >= 0 && u <= 1) wallDistance = Math.Min(wallDistance, t);
        }
        if (wallDistance < float.MaxValue)
            distance = Math.Min(distance, wallDistance - Math.Max(.001f, Math.Max(.5f, hitbox) - .499f));
        else
        {
            // 上游在未撞到指定墙时只画到首个场地边缘之外 0.001 码，
            // 该点仍判出界；不能将普通场地边缘当作安全墙。
            foreach (var contour in arena.GetContours())
                for (var i = 0; i < contour.Count; ++i)
                {
                    Vector2 a = new(contour[i].X, contour[i].Z);
                    var b3 = contour[(i + 1) % contour.Count];
                    var edge = new Vector2(b3.X, b3.Z) - a;
                    var denominator = Cross(direction, edge);
                    if (MathF.Abs(denominator) < .000001f) continue;
                    var t = Cross(a - start, edge) / denominator;
                    var u = Cross(a - start, direction) / denominator;
                    if (t >= 0 && t < distance && u >= 0 && u <= 1) distance = Math.Min(30, t + .001f);
                }
        }
        return point + new Vector3(direction.X, 0, direction.Y) * distance;
    }
}
