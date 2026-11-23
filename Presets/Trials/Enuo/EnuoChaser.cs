using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EnuoChaser(EnuoActorReference Target, Vector3 Previous, float Distance,
    int Remaining, DateTime Activation, int Key);
