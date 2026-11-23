using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record CastrumKnockbackTemplate(IGeometryShape Arena, Vector3 Origin, float Distance,
    IGeometryShape[] LandingDangers) : GeometryTemplate
{
    private readonly IReadOnlyList<Vector3>? polygon = Arena is LocalPolygonGeometry ? Arena.GetContours()[0] : null;

    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new CastrumKnockbackGeometry(this);

    internal Vector3 Landing(Vector3 point)
    {
        var delta = point - Origin;
        delta.Y = 0;
        if (delta.LengthSquared() < .0001f) return point;
        var destination = point + Vector3.Normalize(delta) * Distance;
        if (polygon != null)
        {
            // Clip once against polygon edges. Repeated containment/bisection at
            // every raster sample multiplies polygon cost during knockback solves.
            var start = new Vector2(point.X, point.Z);
            var direction = new Vector2(destination.X - point.X, destination.Z - point.Z);
            var fraction = 1f;
            for (var i = 0; i < polygon.Count; ++i)
            {
                var a = new Vector2(polygon[i].X, polygon[i].Z);
                var b = polygon[(i + 1) % polygon.Count];
                var edge = new Vector2(b.X - a.X, b.Z - a.Y);
                var denominator = direction.X * edge.Y - direction.Y * edge.X;
                if (MathF.Abs(denominator) < .000001f) continue;
                var offset = a - start;
                var t = (offset.X * edge.Y - offset.Y * edge.X) / denominator;
                var u = (offset.X * direction.Y - offset.Y * direction.X) / denominator;
                if (t >= 0 && t < fraction && u >= 0 && u <= 1) fraction = t;
            }
            return Vector3.Lerp(point, destination, fraction < 1 ? MathF.Max(0, fraction - .000005f) : 1);
        }
        if (Arena.Contains(destination)) return destination;
        // Rahu Comet stops at the wall. The arena is convex, so a binary search
        // along the displacement finds the first wall without crossing a gap.
        var near = point;
        var far = destination;
        for (var i = 0; i < 16; ++i)
        {
            var middle = (near + far) * .5f;
            if (Arena.Contains(middle, 0)) near = middle;
            else far = middle;
        }
        return near;
    }
}
