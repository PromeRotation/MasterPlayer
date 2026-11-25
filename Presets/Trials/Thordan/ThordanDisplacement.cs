using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

// Both source mechanics are radial, ignore immunity and do not stop at walls.
internal sealed record ThordanDisplacement(Vector3 Origin, float Distance, DateTime Activation)
{
    internal Vector3 Landing(Vector3 from)
    {
        var delta = new Vector2(from.X - Origin.X, from.Z - Origin.Z);
        var length = delta.Length();
        if (length == 0) return from;
        var direction = delta * (1f / length);
        return from + new Vector3(direction.X, 0, direction.Y) * Distance;
    }
}
