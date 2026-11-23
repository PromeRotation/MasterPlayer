using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class CaduceusPlatformGeometry(CaduceusPlatformTemplate template, float height)
    : GeometryShape(new Vector3(-26, height, -407))
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;

    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var p = new Vector2(point.X, point.Z);
        var distance = float.MaxValue;
        for (var i = 0; i < 13; ++i)
            if ((template.Mask & (1 << i)) != 0) distance = Math.Min(distance, CaduceusPlatforms.Distance(p, i));
        var inside = distance <= epsilon;
        if (template.Inverted)
        {
            // Shared platform edges belong to the walkable union; only the explicit high edges block them.
            if (template.Inset == 0 ? !inside : distance >= -template.Inset - epsilon) return true;
        }
        else if (inside) return true;
        if (template.ActorHeight is not { } y) return false;
        foreach (var (lower, upper) in CaduceusPlatforms.HighEdges)
        {
            if (y + .1f >= CaduceusPlatforms.Heights[upper]) continue;
            var (a, b) = CaduceusPlatforms.Edge(lower, upper);
            var edge = b - a;
            var t = Math.Clamp(Vector2.Dot(p - a, edge) / edge.LengthSquared(), 0, 1);
            if (Vector2.DistanceSquared(p, a + edge * t) <= epsilon * epsilon) return true;
        }
        return false;
    }

    public override bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        if (template.ActorHeight is { } y)
            foreach (var (lower, upper) in CaduceusPlatforms.HighEdges)
            {
                if (y + .1f >= CaduceusPlatforms.Heights[upper]) continue;
                var (a, b) = CaduceusPlatforms.Edge(lower, upper);
                if (GeometryHelper.SegmentsIntersectXZ(start, end, new(a.X, height, a.Y), new(b.X, height, b.Y), epsilon))
                    return true;
            }
        var steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(new(start.X, start.Z), new(end.X, end.Z)) / .1f));
        for (var i = 0; i <= steps; ++i)
            if (Contains(Vector3.Lerp(start, end, (float)i / steps), epsilon)) return true;
        return false;
    }

    public override GeometryBounds GetBounds() => new RectangleGeometry(Position, 0, 70, 86).GetBounds();

    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
    {
        if (!template.Inverted)
            return Enumerable.Range(0, 13).Where(i => (template.Mask & (1 << i)) != 0)
                .Select(i => (IReadOnlyList<Vector3>)CaduceusPlatforms.Polygons[i].Select(p => new Vector3(p.X, height, p.Y)).ToArray()).ToArray();
        if (template.ActorHeight is not { } y) return [];
        return CaduceusPlatforms.HighEdges.Where(e => y + .1f < CaduceusPlatforms.Heights[e.Upper])
            .Select(e =>
            {
                var (a, b) = CaduceusPlatforms.Edge(e.Lower, e.Upper);
                return (IReadOnlyList<Vector3>)new Vector3[] { new(a.X, height, a.Y), new(b.X, height, b.Y) };
            }).ToArray();
    }
}
