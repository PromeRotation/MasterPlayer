namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaTetherBait(long Generation, AlphaActorState Source, AlphaActorState Target,
    DateTime PreparedAt, DateTime Activation);
