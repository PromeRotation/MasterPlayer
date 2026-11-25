using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record CarnivaleBallastSequence(long Generation, DungeonPendingCast VisualCast,
    Vector3 Origin, float Rotation, DateTime Activation, int Order = 0);
