using MasterPlayer.Scripting;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaDelubrumTimeline
{
    private void ArtThrice(uint id, float delay)
        => RawCast(id, delay, 4, [14644], "Tankbuster", ScriptTimelineHint.Tankbuster);

    private void ArtRaidwide(uint id, float delay)
        => RawCast(id, delay, 4, [14645], "Raidwide", ScriptTimelineHint.Raidwide);

    private void ArtMyth(uint id, float delay, bool spread = false)
    {
        RawCast(id, delay, 2, [14631], "Spawn spears");
        if (spread)
        {
            RawCast(id + 0x10, 6.1f, 2.5f, [14640], "Spreads");
            RawCast(id + 0x20, 1.6f, 4.5f, [14632, 14633], "In/Out AOE");
            Add(id + 0x30, .4f, "Spreads resolve", () => !encounter.Spreads(
                encounter.Definition.Rules.Single(r => r.Component == "PiercingDark")).Any(), true);
            Add(id + 0x40, 3, "Spears repeat AOE", () => encounter.ArtLegendCount == 0, true);
        }
        else
        {
            RawCast(id + 0x10, 6, 4.5f, [14632, 14633], "In/Out AOE");
            Add(id + 0x20, 3, "Spears repeat AOE", () => encounter.ArtLegendCount == 0, true);
        }
    }

    private void ArtGeas(uint id, float delay)
    {
        RawCast(id, delay, 4, [14642], "Circle AOE + stop moving");
        Add(id + 0x10, 3, "Can move again + cross", () => encounter.ArtStay[0] == default, true);
        Add(id + 0x20, .5f, "Crosses", () => encounter.Casts.Any(c => c.Rule.Component == "DefilersDeserts"), true);
    }

    private void ArtGlory(uint id, float delay)
    {
        Add(id, delay, "Chasing AOE start", () => encounter.ArtChasers.Count > 0, true);
        Add(id + 0x10, 2.5f, "Proximity AOE start", () => encounter.Primary?.Actor.Cast != null,
            actions: [14639], hints: ScriptTimelineHint.BossCastStart);
        Add(id + 0x20, 5, "Proximity AOE resolve", () => encounter.Primary?.Actor.Cast == null,
            hints: ScriptTimelineHint.BossCastEnd);
        Add(id + 0x30, 3.7f, "Chasing AOE ends", () => encounter.ArtChasers.Count == 0, true);
    }

    private void BuildArt()
    {
        uint id = 0;
        ArtThrice(id, 10);
        RawCast(id + 0x10000, 3.5f, 4.5f, [14632, 14633], "In/Out AOE");
        RawCast(id + 0x10010, 4, 4.5f, [14632, 14633], "Inverse of previous AOE");
        ArtRaidwide(id + 0x20000, 4.1f);
        ArtMyth(id + 0x30000, 7);
        ArtRaidwide(id + 0x40000, 3.2f);
        ArtThrice(id + 0x50000, 3.6f);
        ArtMyth(id + 0x60000, 5);
        for (uint i = 0; i < 10; ++i)
        {
            var pid = i * 0x10000u;
            ArtGeas(id += 0x70000u + pid, 3.1f);
            ArtRaidwide(id += 0x80000u + pid, 4.5f);
            ArtGlory(id += 0x90000u + pid, 4.5f);
            ArtThrice(id += 0xA0000u + pid, 5);
            ArtRaidwide(id += 0xB0000u + pid, 3.2f);
            ArtMyth(id += 0xC0000u + pid, 7.8f);
            ArtThrice(id += 0xD0000u + pid, 3);
            ArtRaidwide(id += 0xE0000u + pid, 3.2f);
            ArtMyth(id += 0xF0000u + pid, 6, true);
        }
        Add(id + 0xFF0000u, 10, "???", () => false);
    }
}
