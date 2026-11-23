using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record VaultKnockbackTemplate(IGeometryShape Arena, Vector3 Origin, IGeometryShape[] Dangers) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => new VaultKnockbackGeometry(this);

    internal Vector3 Landing(Vector3 point)
    {
        var delta = new Vector2(point.X - Origin.X, point.Z - Origin.Z);
        if (delta.LengthSquared() < .0001f) return point;
        var displacement = Vector2.Normalize(delta) * 20;
        var start = new Vector2(point.X, point.Z);
        var stop = 1f;
        // Use the first contour intersection, including the notches in this arena.
        foreach (var contour in Arena.GetContours())
            for (var i = 0; i < contour.Count; ++i)
            {
                var a = new Vector2(contour[i].X, contour[i].Z);
                var b = new Vector2(contour[(i + 1) % contour.Count].X, contour[(i + 1) % contour.Count].Z);
                var edge = b - a;
                var cross = displacement.X * edge.Y - displacement.Y * edge.X;
                if (MathF.Abs(cross) < .000001f) continue;
                var offset = a - start;
                var t = (offset.X * edge.Y - offset.Y * edge.X) / cross;
                var u = (offset.X * displacement.Y - offset.Y * displacement.X) / cross;
                if (t >= 0 && t < stop && u is >= 0 and <= 1) stop = t;
            }
        // Remain just inside the collision surface so epsilon cannot classify the landing as outside.
        var landing = start + displacement * Math.Max(0, stop - .00005f);
        return new(landing.X, point.Y, landing.Y);
    }
}
