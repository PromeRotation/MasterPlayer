using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaThermalPattern(long Generation, DungeonPendingCast Cast,
    bool FireLeft, float Rotation, Vector3 Offset, DateTime PreparedAt, DateTime Activation);
