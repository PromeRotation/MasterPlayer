using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class DungeonCapsuleGeometry(Vector3 position, float rotation, float radius, float length)
    : GeometryShape(position, rotation)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;

    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var direction = new Vector2(MathF.Sin(Rotation), MathF.Cos(Rotation));
        var delta = new Vector2(point.X - Position.X, point.Z - Position.Z);
        var projection = Math.Clamp(Vector2.Dot(delta, direction), 0, length);
        return Vector2.DistanceSquared(delta, direction * projection) <= (radius + epsilon) * (radius + epsilon);
    }

    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
    {
        var points = new List<Vector3>();
        var steps = Math.Max(8, segments / 2);
        var forward = new Vector3(MathF.Sin(Rotation), 0, MathF.Cos(Rotation));
        var right = new Vector3(MathF.Cos(Rotation), 0, -MathF.Sin(Rotation));
        for (var i = 0; i <= steps; ++i)
        {
            var angle = MathF.PI * i / steps;
            points.Add(Position + forward * length + radius * (right * MathF.Cos(angle) + forward * MathF.Sin(angle)));
        }
        for (var i = 0; i <= steps; ++i)
        {
            var angle = MathF.PI * i / steps;
            points.Add(Position + radius * (-right * MathF.Cos(angle) - forward * MathF.Sin(angle)));
        }
        return SingleContour(points);
    }
}
