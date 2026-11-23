using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class SohrKhaiThinIceGeometry(Vector3 player) : GeometryShape(player)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var delta = point - Position;
        var distance = new Vector2(delta.X, delta.Z).Length();
        return !(MathF.Abs(delta.X) <= .5f + epsilon && MathF.Abs(delta.Z) <= .5f + epsilon
            || distance >= 11 - epsilon && distance <= 12.2f + epsilon
            || distance >= 22 - epsilon && distance <= 23.2f + epsilon);
    }
    public override bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(start, end) / .1f));
        for (var i = 0; i <= steps; ++i)
            if (Contains(Vector3.Lerp(start, end, (float)i / steps), epsilon)) return true;
        return false;
    }
    public override GeometryBounds GetBounds() => new(Position.X - 100, Position.Z - 100, Position.X + 100, Position.Z + 100);
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];
}
