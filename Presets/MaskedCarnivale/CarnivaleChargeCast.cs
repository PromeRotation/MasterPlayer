namespace MasterPlayer.Mechanics.Dungeons;

// Actor references preserve the source generation and freeze destroyed targets.
internal sealed record CarnivaleChargeCast(long Generation, AlphaActorState Source, AlphaActorState Target,
    DungeonPendingCast Cast);
