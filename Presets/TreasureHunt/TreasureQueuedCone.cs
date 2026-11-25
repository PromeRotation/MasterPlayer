using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TreasureQueuedCone(long Id, DungeonPendingCast Cast, int Order, Vector3 Origin,
    float Rotation, DateTime Activation);
