namespace MasterPlayer.Mechanics.Hunts;

internal sealed record HuntAglaopePendingEffect(ulong Actor, uint GlobalSequence, int TargetIndex,
    ulong Source, DateTime Expiration, uint StatusId, byte ExtraLo);
