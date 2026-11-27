using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

// A direct SDInvertedRect is a goal in the existing host. Keep its raw
// predicate separate from the normalized, centered goal conversion.
internal sealed record BozjaCll1MagnetTemplate(Vector2 Origin, Vector2 Direction) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.Rectangle;

    internal float Distance(float x, float z)
    {
        var dx = x - Origin.X;
        var dz = z - Origin.Y;
        var along = dx * Direction.X + dz * Direction.Y;
        var side = dx * Direction.Y - dz * Direction.X;
        var front = along - 5;
        var back = -along;
        var left = side - .5f;
        var right = -side - .5f;
        var maxAlong = front > back ? front : back;
        var maxSide = left > right ? left : right;
        return maxAlong > maxSide ? -maxAlong : -maxSide;
    }

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
    {
        var length = MathF.Sqrt(Direction.X * Direction.X + Direction.Y * Direction.Y);
        var dx = Direction.X / length;
        var dz = Direction.Y / length;
        var center = new Vector3(Origin.X + dx * 5f * .5f, position.Y, Origin.Y + dz * 5f * .5f);
        return new RectangleGeometry(center, MathF.Atan2(dx, dz), 1, 5);
    }
}
