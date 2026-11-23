namespace MasterPlayer.Mechanics.Hunts;

internal sealed record HuntDefinition(uint Oid, string Name, HuntCastRule[] Casts);
