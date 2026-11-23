using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class DungeonLosGeometry(Vector3 origin, IReadOnlyList<DungeonActor> pillars, bool safe,
    float range = 100, bool impassable = true, float blockerMargin = .5f) : GeometryShape(origin)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    private IReadOnlyList<IReadOnlyList<Vector3>>? contours;

    public override GeometryBounds GetBounds() => GeometryBounds.FromCenterRadius(Position, range);

    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var delta = new Vector2(point.X - Position.X, point.Z - Position.Z);
        var distance = delta.Length();
        var sheltered = false;
        if (distance <= range + epsilon)
        {
            foreach (var pillar in pillars)
            {
                var offset = new Vector2(pillar.Position.X - Position.X, pillar.Position.Z - Position.Z);
                var d = offset.Length();
                var width = pillar.HitboxRadius < d ? MathF.Asin(pillar.HitboxRadius / d) : MathF.PI / 2;
                if (distance >= d + .2f - epsilon
                    && Math.Abs(MathF.IEEERemainder(MathF.Atan2(delta.X, delta.Y) - MathF.Atan2(offset.X, offset.Y), MathF.Tau)) <= width + epsilon)
                    sheltered = true;
                if (impassable && Vector2.DistanceSquared(new(point.X, point.Z), new(pillar.Position.X, pillar.Position.Z))
                    <= MathF.Pow(pillar.HitboxRadius + blockerMargin - epsilon, 2))
                    return !safe && distance <= range + epsilon;
            }
        }
        return safe ? sheltered : distance <= range + epsilon && !sheltered;
    }

    public override bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        if (Contains(start, epsilon) || Contains(end, epsilon)) return true;
        var a = new Vector2(start.X - Position.X, start.Z - Position.Z);
        var step = new Vector2(end.X - start.X, end.Z - start.Z);
        var stops = new List<float> { 0, 1 };
        AddCircle(Vector2.Zero, range);
        foreach (var pillar in pillars)
        {
            var p = new Vector2(pillar.Position.X - Position.X, pillar.Position.Z - Position.Z);
            var d = p.Length();
            AddCircle(Vector2.Zero, d + .2f);
            if (impassable) AddCircle(p, pillar.HitboxRadius + blockerMargin);
            var angle = MathF.Atan2(p.X, p.Y);
            var width = pillar.HitboxRadius < d ? MathF.Asin(pillar.HitboxRadius / d) : MathF.PI / 2;
            foreach (var edge in new[] { angle - width, angle + width })
            {
                var ray = new Vector2(MathF.Sin(edge), MathF.Cos(edge));
                var denominator = Cross(step, ray);
                if (MathF.Abs(denominator) > 1e-8f) Add(-Cross(a, ray) / denominator);
            }
        }
        stops.Sort();
        for (var i = 1; i < stops.Count; ++i)
            if (Contains(Vector3.Lerp(start, end, (stops[i - 1] + stops[i]) * .5f), epsilon)) return true;
        return false;

        void Add(float t) { if (t > 0 && t < 1) stops.Add(t); }
        void AddCircle(Vector2 center, float radius)
        {
            var offset = a - center;
            var qa = step.LengthSquared();
            if (qa <= 1e-12f) return;
            var qb = 2 * Vector2.Dot(offset, step);
            var discriminant = qb * qb - 4 * qa * (offset.LengthSquared() - radius * radius);
            if (discriminant < 0) return;
            var root = MathF.Sqrt(discriminant);
            Add((-qb - root) / (2 * qa)); Add((-qb + root) / (2 * qa));
        }
    }

    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
    {
        if (!safe) return [];
        if (contours != null) return contours;
        var output = new List<IReadOnlyList<Vector3>>();
        // 对圆弧分片，再逐个扣除不可站立的柱子。输出无洞凸片，两个绘制端均不会把洞填回。
        foreach (var pillar in pillars)
        {
            var offset = new Vector2(pillar.Position.X - Position.X, pillar.Position.Z - Position.Z);
            var distance = offset.Length();
            if (distance + .2f >= range) continue;
            var direction = MathF.Atan2(offset.X, offset.Y);
            var width = pillar.HitboxRadius < distance ? MathF.Asin(pillar.HitboxRadius / distance) : MathF.PI / 2;
            var count = Math.Max(16, (int)MathF.Ceiling(width * 128));
            for (var i = 0; i < count; ++i)
            {
                var left = direction - width + 2 * width * i / count;
                var right = direction - width + 2 * width * (i + 1) / count;
                List<List<Vector2>> pieces =
                [
                    [Point(distance + .2f, left), Point(range, left), Point(range, right), Point(distance + .2f, right)],
                ];
                foreach (var blocker in impassable ? pillars : [])
                {
                    var center = new Vector2(blocker.Position.X, blocker.Position.Z);
                    var radius = blocker.HitboxRadius + blockerMargin;
                    pieces = pieces.SelectMany(p => SubtractCircle(p, center, radius)).ToList();
                }
                foreach (var piece in pieces)
                    if (piece.Count >= 3) output.Add(piece.Select(p => new Vector3(p.X, Position.Y, p.Y)).ToArray());
            }
        }
        return contours = output;
        Vector2 Point(float radius, float angle) => new(Position.X + MathF.Sin(angle) * radius, Position.Z + MathF.Cos(angle) * radius);
    }

    private static IEnumerable<List<Vector2>> SubtractCircle(List<Vector2> polygon, Vector2 center, float radius)
    {
        var minX = polygon.Min(p => p.X);
        var maxX = polygon.Max(p => p.X);
        var minZ = polygon.Min(p => p.Y);
        var maxZ = polygon.Max(p => p.Y);
        if (center.X + radius < minX || center.X - radius > maxX || center.Y + radius < minZ || center.Y - radius > maxZ)
        {
            yield return polygon;
            yield break;
        }
        // 用外接64边形扣除圆，绘制误差保守地留在柱子外；Contains 仍用解析圆。
        var r = radius / MathF.Cos(MathF.PI / 64);
        for (var i = 0; i < 64 && polygon.Count >= 3; ++i)
        {
            var a = center + new Vector2(MathF.Cos(MathF.Tau * i / 64), MathF.Sin(MathF.Tau * i / 64)) * r;
            var b = center + new Vector2(MathF.Cos(MathF.Tau * (i + 1) / 64), MathF.Sin(MathF.Tau * (i + 1) / 64)) * r;
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
            var d1 = Cross(b - a, from - a);
            var d2 = Cross(b - a, to - a);
            var keep1 = inside ? d1 >= 0 : d1 <= 0;
            var keep2 = inside ? d2 >= 0 : d2 <= 0;
            if (keep1) result.Add(from);
            if (keep1 != keep2) result.Add(Vector2.Lerp(from, to, d1 / (d1 - d2)));
        }
        return result;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}
