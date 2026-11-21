using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class CastrumKnockbackGeometry(CastrumKnockbackTemplate template) : GeometryShape(template.Arena.Position)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        if (!template.Arena.Contains(point)) return true;
        var landing = template.Landing(point);
        return template.LandingDangers.Any(danger => danger.Contains(landing, epsilon));
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
