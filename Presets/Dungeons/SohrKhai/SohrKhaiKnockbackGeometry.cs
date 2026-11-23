using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class SohrKhaiKnockbackGeometry(SohrKhaiKnockbackTemplate template) : GeometryShape(template.Arena.Position)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => new RayRectangleGeometry(template.Origin, template.Direction, -20, 20, 4.5f).Contains(point, epsilon)
            && !template.Arena.Contains(SohrKhaiPoqhirajArena.Landing(point, template.Direction, template.Removed), epsilon);
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
