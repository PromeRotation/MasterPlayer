using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record CarnivaleSpawnAoe(long Generation, AlphaActorState Trigger, DateTime CreatedAt,
    DateTime Activation, Vector3 Position, float Radius);
