using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record DungeonQuestTower(long Id, DungeonPendingCast Cast, Vector3 Position, DateTime Activation, int ForbiddenSlot = -1);
