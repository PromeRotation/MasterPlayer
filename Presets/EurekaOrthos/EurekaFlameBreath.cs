using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaFlameBreath(long Generation, ulong Source, Vector3 CasterPosition,
    float CasterRotation, DateTime PreparedAt, Vector3 Origin, DateTime Activation);
