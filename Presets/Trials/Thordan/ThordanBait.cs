namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record ThordanBait(long Generation, ThordanComponent Component, ThordanActor Source,
    ThordanActor Target, DateTime Activation);
