using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class VanaspatiMisdirectionPolicy
{
    internal static bool ShouldMove(Vector3 position, Vector3 destination, float tolerance, float arrow)
    {
        var delta = destination - position; delta.Y = 0;
        if (!float.IsFinite(delta.X) || !float.IsFinite(delta.Z) || !float.IsFinite(arrow)
            || delta.LengthSquared() <= Math.Max(.01f, tolerance * tolerance)) return false;
        return MathF.Abs(MathF.IEEERemainder(MathF.Atan2(delta.X, delta.Z) - arrow, MathF.Tau)) <= MathF.PI / 12;
    }
}
