using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaShock(long Generation, AlphaActorState Source, Vector3 Origin,
    DateTime PreparedAt, DateTime Activation);
