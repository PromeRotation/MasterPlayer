using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record BabilChaser(ulong Source, DungeonActor Target, Vector3 Previous, float MoveDistance, int Remaining, DateTime Activation);
