using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record CarnivaleSpawnKnockback(long Generation, AlphaActorState Trigger, DateTime CreatedAt,
    DateTime Activation, Vector3 Origin, float Distance);
