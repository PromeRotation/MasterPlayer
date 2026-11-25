using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class TreasurePolePushGeometry(TreasurePolePushTemplate template) : GeometryShape(template.Center)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var direction = new Vector2(point.X - template.Origin.X, point.Z - template.Origin.Z);
        if (direction.LengthSquared() > 0) direction = Vector2.Normalize(direction);
        var landing = new Vector2(point.X, point.Z) + direction * template.Distance;
        return MathF.Abs(landing.X - template.Center.X) > template.HalfWidth
            || MathF.Abs(landing.Y - template.Center.Z) > template.HalfWidth;
    }
    public override GeometryBounds GetBounds() => new RectangleGeometry(template.Center, 0, 39, 39).GetBounds();
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];
}
