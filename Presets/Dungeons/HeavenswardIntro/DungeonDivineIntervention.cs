namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsDivineIntervention => territory == HeavenswardIntroCatalog.DIVINE_INTERVENTION;
    private bool DivinePairAlive => IsDivineIntervention && Active != null
        && _actors.Values.Any(a => a.Oid is 0x1010 or 0x1011 && !a.Dead);
}
