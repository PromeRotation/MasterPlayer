namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed record PalaceDamagePrediction(ulong Source, ulong TargetId, DateTime Activation, string DamageType);
