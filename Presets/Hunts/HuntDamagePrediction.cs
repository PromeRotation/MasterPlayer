namespace MasterPlayer.Mechanics.Hunts;

internal sealed record HuntDamagePrediction(ulong Source, ulong TargetId, DateTime Activation, string DamageType);
