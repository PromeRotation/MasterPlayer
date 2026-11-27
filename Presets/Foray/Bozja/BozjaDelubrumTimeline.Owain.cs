using MasterPlayer.Scripting;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaDelubrumTimeline
{
    private void OwainThrice(uint id, float delay)
        => RawCast(id, delay, 5, [14661], "Tankbuster", ScriptTimelineHint.Tankbuster);

    private void OwainRaidwide(uint id, float delay)
        => RawCast(id, delay, 5, [14662], "Raidwide", ScriptTimelineHint.Raidwide);

    private void OwainMyth(uint id, float delay)
    {
        RawCast(id, delay, 2, [14646], "Spawn spears");
        RawCast(id + 0x10, 2.2f, 2, [14647], "Switch elements");
        RawCast(id + 0x20, 6.3f, 5, [14651, 14650], "Circle AOEs");
    }

    private void OwainShift(uint id, float delay)
    {
        RawCast(id, delay, 2, [14647], "Switch elements");
        RawCast(id + 0x10, 6.4f, 5, [14651, 14650], "Circle AOEs");
    }

    private void OwainSpirit(uint id, float delay)
    {
        RawCast(id, delay, 3, [14654], "Dorito stacks appear", enter: () => encounter.SetOwainImbas(true));
        Add(id + 0x10, .1f, "Spreads appear", () => encounter.Casts.Any(c => c.Rule.Component == "LegendaryImbas"),
            true, hints: ScriptTimelineHint.Raidwide, exit: () => encounter.SetOwainImbas(false));
        Add(id + 0x20, 5, "Spreads and dorito stacks resolve", () => !encounter.Casts.Any(c => c.Rule.Component == "PiercingLight1"), true);
    }

    private void OwainLight(uint id, float delay)
    {
        Add(id, delay, "Spreads appear", () => encounter.Casts.Any(c => c.Rule.Component == "PiercingLight2"), true);
        Add(id + 0x10, 1, "Proximity AOE", () => encounter.Primary?.Actor.Cast != null,
            actions: [14669], hints: ScriptTimelineHint.BossCastStart);
        Add(id + 0x20, 3.8f, "Spreads resolve", () => !encounter.Casts.Any(c => c.Rule.Component == "PiercingLight2"), true);
        Add(id + 0x30, 1, "Proximity AOE resolves", () => encounter.Primary?.Actor.Cast == null,
            hints: ScriptTimelineHint.BossCastEnd);
    }

    private void OwainShiftSpirit(uint id, float delay)
    {
        RawCast(id, delay, 2, [14647], "Switch elements");
        RawCast(id + 0x10, 6.3f, 3, [14654], "Dorito stacks appear", enter: () => encounter.SetOwainImbas(true));
        Add(id + 0x20, 1.1f, "Spreads appear", () => encounter.Casts.Any(c => c.Rule.Component == "LegendaryImbas"),
            true, hints: ScriptTimelineHint.Raidwide, exit: () => encounter.SetOwainImbas(false));
        Add(id + 0x30, 2, "Circle AOEs start", () => encounter.Primary?.Actor.Cast != null,
            actions: [14651, 14650], hints: ScriptTimelineHint.BossCastStart);
        Add(id + 0x40, 3, "Spreads resolve", () => !encounter.Casts.Any(c => c.Rule.Component == "PiercingLight1"), true);
        Add(id + 0x50, 2, "Circles resolve", () => encounter.OwainElements.Count == 0, true);
    }

    private void OwainIvory(uint id, float delay)
    {
        Add(id, delay, "Hands spawn", () => encounter.OwainTethers.Count != 0, true);
        RawCast(id + 0x10, 6.4f, 2, [14647], "Switch elements");
        RawCast(id + 0x20, 6.3f, 5, [14651, 14650], "Circle AOEs");
        OwainThrice(id + 0x30, 6);
        Add(id + 0x40, 5, "Hands soft enrage", () => !encounter.Casts.Any(c => c.Rule.Component == "IvoryPalmExplosion"), true);
    }

    private void BuildOwain()
    {
        uint id = 0;
        OwainThrice(id, 10);
        OwainRaidwide(id + 0x10000, 7.1f);
        OwainMyth(id + 0x20000, 7.9f);
        OwainThrice(id + 0x30000, 10);
        OwainRaidwide(id + 0x40000, 7);
        OwainShift(id + 0x50000, 7.5f);
        OwainThrice(id + 0x60000, 11);
        OwainSpirit(id + 0x70000, 8.3f);
        for (uint i = 0; i < 12; ++i)
        {
            var pid = i * 0x10000u;
            OwainThrice(id += 0x80000u + pid, 5.1f);
            OwainRaidwide(id += 0x90000u + pid, 6);
            OwainLight(id += 0xA0000u + pid, 6.1f);
            OwainRaidwide(id += 0xB0000u + pid, 9.7f);
            OwainRaidwide(id += 0xC0000u + pid, 5.1f);
            OwainShiftSpirit(id + 0xD0000u + pid, 8.3f);
            OwainThrice(id += 0xE0000u + pid, 9.3f);
            OwainRaidwide(id += 0xF0000u + pid, 17.2f);
            OwainIvory(id += 0x100000u + pid, 1.2f);
            OwainSpirit(id += 0x110000u + pid, 5.1f);
        }
        Add(id + 0xFF0000u, 10, "???", () => false);
    }
}
