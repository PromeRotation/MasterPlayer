using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TreasureConcentricStrike(long Id, ulong Source, DateTime StartedAt,
    Vector3 Origin, float Rotation, DateTime Activation, int Order = 0);
