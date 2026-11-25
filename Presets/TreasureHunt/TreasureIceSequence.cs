using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TreasureIceSequence(long Id, DungeonPendingCast Cast, Vector3 Origin,
    DateTime Activation, int Order = 0);
