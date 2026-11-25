using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record HeavenOnHighKnockback(DungeonPendingCast Cast, Vector3 Origin,
    float Distance = 30, bool IgnoreImmunes = true, bool StopAtWall = true);
