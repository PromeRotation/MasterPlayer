namespace MasterPlayer.Mechanics.Bozja;

internal sealed record BozjaDelayedHit(BozjaActor Source, BozjaActor Target, int Slot, DateTime Activation);
