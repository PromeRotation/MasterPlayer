namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaSteelClaw(long Generation, DungeonPendingCast Cast, AlphaActorState Source,
    AlphaActorState Target, int Slot);
