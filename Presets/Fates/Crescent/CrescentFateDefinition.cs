namespace MasterPlayer.Mechanics.Fates;

internal sealed record CrescentFateDefinition(string Name, string DisplayName, uint Territory, uint Cfc,
    uint FateId, uint PrimaryOid, bool ProtectedPot, CrescentFateRule[] Rules);
