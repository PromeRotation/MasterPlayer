using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed class PalaceLandingGeometry(PalaceLandingTemplate template) : GeometryShape(PalaceArena.Center)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var landing = template.Landing(point);
        return !PalaceArena.Boundary.Contains(landing) || template.Dangers.Any(d => d.Contains(landing, epsilon));
    }
    public override bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var count = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(start, end) / .1f));
        for (var i = 0; i <= count; ++i)
            if (Contains(Vector3.Lerp(start, end, (float)i / count), epsilon)) return true;
        return false;
    }
    public override GeometryBounds GetBounds() => PalaceArena.Boundary.GetBounds();
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];
}
