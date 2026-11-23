using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record DarknessChaser(long Serial, ulong Source, DungeonActor Target, Vector3 Previous,
    float Distance, int Remaining, DateTime Activation);
