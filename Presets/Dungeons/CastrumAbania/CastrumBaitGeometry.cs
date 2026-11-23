using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class CastrumBaitGeometry(CastrumBaitTemplate template) : GeometryShape(template.Arena.Position)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var direction = point - template.Origin;
        direction.Y = 0;
        if (direction.LengthSquared() < .01f) return true;
        direction = Vector3.Normalize(direction);
        return template.Party.Any(member =>
        {
            var delta = member - template.Origin;
            delta.Y = 0;
            var along = Vector3.Dot(delta, direction);
            return along >= 0 && along <= template.Length + epsilon
                && (delta - along * direction).Length() <= template.HalfWidth + epsilon;
        });
    }

    public override bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(start, end) / .1f));
        for (var i = 0; i <= steps; ++i)
            if (Contains(Vector3.Lerp(start, end, (float)i / steps), epsilon)) return true;
        return false;
    }

    public override GeometryBounds GetBounds() => template.Arena.GetBounds();
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];
}
