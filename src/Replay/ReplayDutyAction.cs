namespace MasterPlayer.Replay;

public sealed record ReplayDutyAction(ushort Slot, uint ActionId, byte CurrentCharges, byte MaxCharges);
