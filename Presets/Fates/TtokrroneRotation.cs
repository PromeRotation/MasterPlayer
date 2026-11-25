using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TtokrroneRotation(DungeonPendingCast Cast, Vector3 Origin, float Rotation,
    float Increment, DateTime NextActivation, int Remaining, int Total, int Consumed = 0)
{
    internal const double INTERVAL = (double)2.6f;
}
