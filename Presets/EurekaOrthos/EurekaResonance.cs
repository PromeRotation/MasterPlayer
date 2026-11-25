namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaResonance(long Generation, AlphaActorState Source, AlphaActorState Target,
    DungeonPendingCast Cast);
