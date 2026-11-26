namespace MasterPlayer.Mechanics.Bozja;

// Preserve empty masks and component ordering for diagnostics.
internal sealed record BozjaDamage(ulong Mask, DateTime Activation, string Type);
