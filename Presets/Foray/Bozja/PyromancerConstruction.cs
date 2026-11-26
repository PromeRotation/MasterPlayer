namespace MasterPlayer.Mechanics.Bozja;

// One actual module construction, including a pending module. Its history is
// independent of both the selected module and other actors with the same OID.
internal sealed class PyromancerConstruction
{
    internal List<BozjaCast> Casts { get; } = [];
    internal bool ThermalActive = true;
    internal int NumCasts;
}
