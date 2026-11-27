using MasterPlayer.Scripting;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaDelubrumTimeline
{
    private void VirtueMeteor(uint id, float delay)
        => RawCast(id, delay, 4, [14233], "Raidwide", ScriptTimelineHint.Raidwide);

    private void VirtueEidos(uint id, float delay)
        => RawCast(id, delay, 2, [14214, 14215], "Change element");

    private void VirtueMedusa(uint id, float delay)
        => RawCast(id, delay, 3, [14235], "Cone AOE");

    private void VirtueWind(uint id, float delay)
        => RawCast(id, delay, 5, [14234], "Tankbuster", ScriptTimelineHint.Tankbuster);

    private void VirtueWyvern(uint id, float delay)
        => RawCast(id, delay, 3, [14232], "Adds spawn");

    private void VirtueTowers(uint id, float delay)
        => Add(id, delay, "Towers + tethers appear", () => encounter.VirtueCasters("DarkAuroraTether") != 0, true);

    private void VirtueCounter(uint id, float delay, int count, string name, bool last = false)
        => Add(id, delay, name, () => encounter.VirtueCount("BrightDarkAuroraCounter") == count, true,
            exit: last ? () => encounter.SetVirtueCounter("BrightDarkAuroraCounter", false) : null);

    private void VirtueAspect(uint id, float delay)
    {
        Add(id, delay, "Circle AOEs appear", () => encounter.Primary?.Actor.Cast != null,
            actions: [14219], hints: ScriptTimelineHint.BossCastStart);
        Add(id + 0x10, 7.4f, "Proximity AOE appears", () => encounter.VirtueCasters("ExplosiveImpulse") != 0,
            true, enter: () => encounter.SetVirtueCounter("BrightDarkAuroraCounter", true));
        Add(id + 0x20, 2, "Circle AOEs resolve", () => encounter.Primary?.Actor.Cast == null,
            hints: ScriptTimelineHint.BossCastEnd);
        Add(id + 0x30, 4.4f, "Proximity AOE resolves", () => encounter.VirtueCasters("ExplosiveImpulse") == 0, true);
        VirtueEidos(id + 0x40, 6.3f);
        VirtueCounter(id + 0x50, 1.9f, 2, "Half room cleaves 1");
        VirtueCounter(id + 0x60, 4.8f, 4, "Half room cleaves 2", true);
    }

    private void VirtueRelatives(uint id, float delay)
    {
        Add(id, delay, "Proximity AOEs appear", () => encounter.VirtueCasters("ExplosiveImpulse") != 0,
            true, enter: () => encounter.SetVirtueCounter("BrightDarkAuroraCounter", true));
        Add(id + 0x10, 5, "Proximity AOEs resolve", () => encounter.VirtueCasters("ExplosiveImpulse") == 0, true);
        VirtueCounter(id + 0x20, 8, 2, "Half room cleaves 1");
        VirtueCounter(id + 0x30, 5.6f, 4, "Half room cleaves 2");
        VirtueCounter(id + 0x40, 4.4f, 6, "Half room cleaves 3", true);
        Add(id + 0x50, 1.3f, "Proximity AOE appears", () => encounter.Primary?.Actor.Cast != null,
            actions: [14228], hints: ScriptTimelineHint.BossCastStart);
        Add(id + 0x60, 5, "Proximity AOE resolves", () => encounter.Primary?.Actor.Cast == null,
            hints: ScriptTimelineHint.BossCastEnd);
    }

    private void BuildVirtue()
    {
        VirtueMeteor(0, 11.1f);
        VirtueEidos(0x10000, 4.2f);
        RawCast(0x20000, 4.1f, 8, [14219], "Circle AOEs");
        VirtueMedusa(0x30000, 2.7f);
        VirtueEidos(0x40000, 5.2f);
        RawCast(0x50000, 4, 3, [14216], "Half room cleaves");
        VirtueWind(0x60000, 6.4f);
        VirtueEidos(0x70000, 12);
        RawCast(0x80000, 3.8f, 8, [14219], "Circle AOEs");
        VirtueMeteor(0x90000, 14);
        VirtueTowers(0xA0000, 10);
        VirtueMedusa(0xB0000, 5.8f);
        VirtueWind(0xC0000, 3.5f);
        VirtueMeteor(0xD0000, 4.1f);
        VirtueMeteor(0xE0000, 5.1f);
        VirtueRelatives(0xF0000, 10);
        VirtueMedusa(0x100000, 3.2f);
        VirtueWind(0x110000, 7);
        VirtueMeteor(0x120000, 4.2f);
        VirtueWyvern(0x130000, 6.5f);
        VirtueTowers(0x140000, 6.4f);
        VirtueMedusa(0x150000, 5);
        VirtueWind(0x160000, 3.1f);
        VirtueMeteor(0x170000, 13);
        VirtueEidos(0x180000, 4.2f);
        VirtueAspect(0x190000, 5);
        VirtueMedusa(0x1A0000, 8.2f);
        VirtueMeteor(0x1B0000, 3.2f);
        VirtueWyvern(0x1C0000, 12.3f);
        VirtueTowers(0x1D0000, 6.2f);
        VirtueMedusa(0x1E0000, 5);
        VirtueWind(0x1F0000, 3.2f);
        VirtueMeteor(0x200000, 13.2f);
        VirtueEidos(0x210000, 4.2f);
        VirtueAspect(0x220000, 3.5f);
        VirtueMedusa(0x230000, 8);
        VirtueMeteor(0x240000, 3);
        RawCast(0x250000, 3.1f, 10, [14700], "Enrage",
            enter: () => encounter.SetVirtueCounter("MeteorEnrageCounter", true));
        Add(0x250010, 3, "Enrage repeat 1", () => encounter.VirtueCount("MeteorEnrageCounter") == 1, true);
        Add(0x250020, 5, "Enrage repeat 2", () => encounter.VirtueCount("MeteorEnrageCounter") == 2, true);
    }
}
