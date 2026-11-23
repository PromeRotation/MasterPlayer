using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class MistwakeKnockbackGeometry(MistwakeKnockbackTemplate template) : GeometryShape(template.Center)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var d = template.Landing(point) - Position;
        return d.X * d.X + d.Z * d.Z > (19 - epsilon) * (19 - epsilon);
    }
    public override bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var count = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(start, end) / .1f));
        for (var i = 0; i <= count; ++i)
            if (Contains(Vector3.Lerp(start, end, (float)i / count), epsilon)) return true;
        return false;
    }
    public override GeometryBounds GetBounds() => new CircleGeometry(Position, 40).GetBounds();
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];
}
