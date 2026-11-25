using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record KefkaDisplacement(Vector3 Origin, float Distance, float Rotation,
    DateTime Activation, bool Forward, bool StopAtWall = false, bool IgnoreImmunes = false)
{
    internal Vector3 Landing(Vector3 point)
    {
        var (sin, cos) = Math.SinCos(Rotation);
        if (Forward) return point + Distance * new Vector3((float)sin, 0, (float)cos);
        var direction = point - Origin;
        direction.Y = 0;
        var length = MathF.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
        if (length == 0) return point;
        var inverse = 1f / length;
        return point + Distance * (direction * inverse);
    }
}
