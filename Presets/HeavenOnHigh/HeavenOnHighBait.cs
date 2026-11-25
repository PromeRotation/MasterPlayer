namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record HeavenOnHighBait(long Generation, AlphaActorState Source, AlphaActorState Target,
    DungeonPendingCast Cast, DateTime Activation);
