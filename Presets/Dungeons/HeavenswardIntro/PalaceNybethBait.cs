namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record PalaceNybethBait(long Generation, AlphaActorState Source, AlphaActorState Target,
    DungeonPendingCast Cast);
