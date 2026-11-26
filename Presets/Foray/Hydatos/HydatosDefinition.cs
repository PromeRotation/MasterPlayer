namespace MasterPlayer.Mechanics.Hydatos;

internal sealed record HydatosDefinition(string Name, uint PrimaryOid, uint[] DrawOids, HydatosRule[] Rules);
