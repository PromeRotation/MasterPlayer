using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class DeadEndsWaveGeometry(Vector3 origin, float rotation) : GeometryShape(origin)
{
    // Match the source's 0.05-yard chord error: 14 edges per semicircle, 26 per circle.
    private readonly WorldPolygonGeometry fan = new(
        new[] { origin }.Concat(Enumerable.Range(0, 15).Select(i =>
            origin + new Vector3(6 * MathF.Sin(rotation - MathF.PI / 2 + MathF.PI * i / 14), 0,
                6 * MathF.Cos(rotation - MathF.PI / 2 + MathF.PI * i / 14)))));
    private static readonly Vector2[] centers = [new(271.473f, -178.027f), new(261.494f, -178.027f)];
    private readonly WorldPolygonGeometry[] circles = centers.Select(c => new WorldPolygonGeometry(
        Enumerable.Range(0, 26).Select(i => new Vector3(c.X + 6 * MathF.Sin(MathF.Tau * i / 26), origin.Y,
            c.Y + 6 * MathF.Cos(MathF.Tau * i / 26))))).ToArray();
    private IReadOnlyList<IReadOnlyList<Vector3>>? contours;
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => fan.Contains(point, epsilon) && circles.All(c => !c.Contains(point, -epsilon));
    public override GeometryBounds GetBounds() => fan.GetBounds();
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
    {
        if (contours != null) return contours;
        var result = new List<IReadOnlyList<Vector3>>();
        for (var i = 0; i < 14; ++i)
        {
            var a = rotation - MathF.PI / 2 + MathF.PI * i / 14;
            var b = rotation - MathF.PI / 2 + MathF.PI * (i + 1) / 14;
            List<List<Vector2>> pieces = [[new(Position.X, Position.Z), Point(a), Point(b)]];
            foreach (var center in centers)
                pieces = pieces.SelectMany(p => Subtract(p, center)).ToList();
            result.AddRange(pieces.Where(p => p.Count >= 3).Select(p => (IReadOnlyList<Vector3>)p.Select(v => new Vector3(v.X, Position.Y, v.Y)).ToArray()));
        }
        return contours = result;
        Vector2 Point(float angle) => new(Position.X + 6 * MathF.Sin(angle), Position.Z + 6 * MathF.Cos(angle));
    }

    private static IEnumerable<List<Vector2>> Subtract(List<Vector2> polygon, Vector2 center)
    {
        for (var i = 0; i < 26 && polygon.Count >= 3; ++i)
        {
            var a = center + new Vector2(MathF.Sin(MathF.Tau * i / 26), MathF.Cos(MathF.Tau * i / 26)) * 6;
            var b = center + new Vector2(MathF.Sin(MathF.Tau * (i + 1) / 26), MathF.Cos(MathF.Tau * (i + 1) / 26)) * 6;
            var outside = Clip(polygon, a, b, false);
            if (outside.Count >= 3) yield return outside;
            polygon = Clip(polygon, a, b, true);
        }
    }

    private static List<Vector2> Clip(List<Vector2> polygon, Vector2 a, Vector2 b, bool inside)
    {
        var result = new List<Vector2>();
        for (var i = 0; i < polygon.Count; ++i)
        {
            var from = polygon[i];
            var to = polygon[(i + 1) % polygon.Count];
            var d1 = (b.X - a.X) * (from.Y - a.Y) - (b.Y - a.Y) * (from.X - a.X);
            var d2 = (b.X - a.X) * (to.Y - a.Y) - (b.Y - a.Y) * (to.X - a.X);
            var keep1 = inside ? d1 <= 0 : d1 >= 0;
            var keep2 = inside ? d2 <= 0 : d2 >= 0;
            if (keep1) result.Add(from);
            if (keep1 != keep2) result.Add(Vector2.Lerp(from, to, d1 / (d1 - d2)));
        }
        return result;
    }
}
