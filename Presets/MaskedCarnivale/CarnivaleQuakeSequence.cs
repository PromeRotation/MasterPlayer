using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record CarnivaleQuakeSequence(long Generation, DungeonPendingCast StartCast, Vector3 Origin,
    DateTime Activation, int Order = 0);
