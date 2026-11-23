using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record BardamChaser(ulong Source, ulong Target, Vector3 Previous, float MoveDistance, int Remaining, DateTime Activation);
