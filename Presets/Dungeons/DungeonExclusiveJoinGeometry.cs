using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class DungeonExclusiveJoinGeometry(IGeometryShape arena, IGeometryShape[] candidates) : GeometryShape(arena.Position)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => candidates.Count(c => c.Contains(point, epsilon)) != 1;
    public override bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(start, end) / .1f));
        for (var i = 0; i <= steps; ++i)
            if (Contains(Vector3.Lerp(start, end, (float)i / steps), epsilon)) return true;
        return false;
    }
    public override GeometryBounds GetBounds() => arena.GetBounds();
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];
}
