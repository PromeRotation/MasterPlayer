using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class WarriorOfLightZeroCircleGeometry(Vector3 position) : GeometryShape(position)
{
    public override GeometryKind Kind => GeometryKind.Circle;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => Vector2.DistanceSquared(new(point.X, point.Z), new(Position.X, Position.Z)) <= epsilon * epsilon;
    public override GeometryBounds GetBounds() => new CircleGeometry(Position, float.Epsilon).GetBounds();
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];
}
