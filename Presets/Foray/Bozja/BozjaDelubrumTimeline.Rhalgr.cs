using MasterPlayer.Scripting;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaDelubrumTimeline
{
    private void RhalgrChargeLightning(uint id, float delay)
    {
        Add(id, delay, "", () => encounter.RhalgrChargeAoes != 0, true);
        Start(id + 0x10, 6.7f, [28897]);
        Add(id + 0x20, 2.4f, "Diagonal cleaves", () => encounter.RhalgrChargeCount != 0, true,
            exit: () => encounter.ResetRhalgr("DestructiveCharge"));
        Add(id + 0x30, .6f, "", () => encounter.Primary?.Actor.Cast == null, hints: ScriptTimelineHint.BossCastEnd);
        Add(id + 0x40, 1, "", () => encounter.RhalgrLightningCasters != 0, true);
        Add(id + 0x50, 7, "Puddles resolve", () => encounter.RhalgrLightningCasters == 0, true);
    }

    private void RhalgrBlue(uint id, float delay)
    {
        RawCast(id, delay, 5, [28899], "");
        Add(id + 2, 1, "Raidwide", () => encounter.RhalgrBlueCount != 0, true,
            exit: () => encounter.ResetRhalgr("BoltsFromTheBlue"), hints: ScriptTimelineHint.Raidwide);
    }

    private void BuildRhalgr()
    {
        RawCast(0, 5.4f, 8, [28896], "Frontal cleave");
        RhalgrChargeLightning(0x10000, 14.4f);
        RhalgrBlue(0x20000, 2.2f);
        Add(0x30000, 12.4f, "", () => encounter.RhalgrChargeAoes != 0, true);
        Start(0x30010, 3.9f, [28896]);
        Add(0x30020, 5.2f, "Diagonal cleaves", () => encounter.RhalgrChargeCount != 0, true,
            exit: () => encounter.ResetRhalgr("DestructiveCharge"));
        Add(0x30030, 2.8f, "Frontal cleave", () => encounter.Primary?.Actor.Cast == null, hints: ScriptTimelineHint.BossCastEnd);
        RawCast(0x40000, 6.2f, 2, [28892], "");
        Add(0x40010, 1.1f, "Concentric AOE 1", () => encounter.NumEarthCasts >= 2, true);
        Add(0x40020, 2, "Concentric AOE 2", () => encounter.NumEarthCasts >= 4, true);
        Add(0x40030, 2, "Concentric AOE 3", () => encounter.NumEarthCasts >= 6, true,
            exit: () => encounter.ResetRhalgr("Boltloop"));
        RawCast(0x50000, 4.1f, 5, [28889], "Tankbuster", ScriptTimelineHint.Tankbuster);
        RhalgrBlue(0x60000, 6.2f);
        RhalgrChargeLightning(0x70000, 13.5f);
        Add(0xFF0000, 10, "???", () => false);
    }
}
