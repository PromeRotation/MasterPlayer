using System.Numerics;
using PromeRotation.Helpers;

namespace MasterPlayer.Radar;

internal readonly record struct RadarProjection(Vector3 Center, Vector2 ScreenCenter, float Scale, float Rotation)
{
    public const float NorthUpRotation = MathF.PI;

    public Vector2 Project(Vector3 world)
        => ScreenCenter + GeometryHelper.WorldToLocal(world, Center, Rotation) * Scale;
}
