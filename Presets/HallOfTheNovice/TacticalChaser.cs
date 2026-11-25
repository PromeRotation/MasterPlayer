using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TacticalChaser(long Id, DungeonPendingCast Cast, DungeonActor Target, Vector3 Previous,
    DateTime Activation, float MoveDistance = 0, int Remaining = 5, bool Removed = false);
