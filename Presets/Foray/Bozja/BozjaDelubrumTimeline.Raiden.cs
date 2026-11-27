using MasterPlayer.Scripting;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaDelubrumTimeline
{
    private void RaidenCondition(uint id, float duration, string component, Func<int, bool> condition, string name,
        Action? exit = null)
        => Add(id, duration, name, () => condition(encounter.RaidenCasters(component)), true, exit: exit);

    private void RaidenTargetable(uint id, bool targetable, float delay)
        => Add(id, delay, "", () => encounter.Primary?.Actor.Targetable == targetable,
            hints: targetable ? ScriptTimelineHint.DowntimeEnd : ScriptTimelineHint.DowntimeStart);

    private void RaidenCastStart(uint id, uint[] actions, float delay, string name)
        => Add(id, delay, name, () => encounter.Primary?.Actor.Cast != null,
            actions: actions, hints: ScriptTimelineHint.BossCastStart);

    private void RaidenCastEnd(uint id, float duration, string name, Action? exit = null)
        => Add(id, duration, name, () => encounter.Primary?.Actor.Cast == null,
            exit: exit, hints: ScriptTimelineHint.BossCastEnd);

    private void RaidenSpirits(uint id, float delay)
        => RawCast(id, delay, 4, [14458], "Raidwide", ScriptTimelineHint.Raidwide);

    private void RaidenShingan(uint id, float delay)
        => RawCast(id, delay, 4, [14459], "Tankbuster", ScriptTimelineHint.Tankbuster);

    private void RaidenAme(uint id, float delay)
    {
        RaidenCondition(id, delay, "AmeNoSakahoko", n => n != 0, "Circle AOE");
        RaidenCondition(id + 0x10, 7.5f, "AmeNoSakahoko", n => n == 0, "AOE resolve");
    }

    private void RaidenWhirling(uint id, float delay)
        => RawCast(id, delay, 5.5f, [14442], "Donut AOE");

    private void RaidenLateral(uint id, float delay)
    {
        RaidenCondition(id, delay, "Shock", n => n != 0, "Baited circles");
        RaidenCastStart(id + 0x10, [14443, 14444], .8f, "Half room cleave starts");
        RaidenCondition(id + 0x20, 2.1f, "Shock", n => n == 0, "Circles resolve");
        RaidenCastEnd(id + 0x30, 4.3f, "Cleave resolves");
    }

    private void RaidenUltimate(uint id, float delay)
    {
        RaidenCondition(id, delay, "LancingBlowSpread", n => n != 0, "Spread markers appear");
        RaidenCondition(id + 0x10, 4.7f, "LancingBlowSpread", n => n == 0, "Adds spawn");
        Add(id + 0x20, 1, "AOEs around adds", () => encounter.RaidenCount("LancingBlowAOE") != 0, true);
        RaidenTargetable(id + 0x30, false, 2);
        RaidenTargetable(id + 0x40, true, 4);
        RaidenCastStart(id + 0x50, [14456], 1.6f, "Enrage start");
        RaidenCastEnd(id + 0x60, 18, "Enrage", () => encounter.ResetRaiden("LancingBlowSpread"));
    }

    private void RaidenLateralUltimate(uint id, float delay)
    {
        RaidenTargetable(id, false, delay);
        RaidenCondition(id + 0x10, 3.3f, "LancingBlowSpread", n => n != 0, "Spread markers appear");
        RaidenCondition(id + 0x20, 1.1f, "Shock", n => n != 0, "Baited circles");
        RaidenCondition(id + 0x30, 3, "Shock", n => n == 0, "Circles resolve");
        RaidenCondition(id + 0x40, 1.1f, "LancingBlowSpread", n => n == 0, "Adds spawn");
        RaidenCastStart(id + 0x50, [14443, 14444], .1f, "Half room cleave starts");
        Add(id + 0x60, 1, "AOEs around adds", () => encounter.RaidenCount("LancingBlowAOE") != 0, true);
        RaidenCondition(id + 0x70, .3f, "SilentLevin", n => n != 0, "Baited circles 1");
        RaidenCondition(id + 0x80, 1.9f, "SilentLevin", n => n > 3, "Baited circles 2");
        RaidenCondition(id + 0x90, 2.7f, "SilentLevin", n => encounter.RaidenCount("SilentLevin") >= 3 && n > 3, "Baited circles 3");
        RaidenCastEnd(id + 0xA0, 1.6f, "Cleave resolves");
        RaidenTargetable(id + 0xB0, true, 4.7f);
        RaidenCastStart(id + 0xC0, [14456], 2.1f, "Enrage start");
        RaidenCastEnd(id + 0xD0, 18, "Enrage", () =>
        {
            encounter.ResetRaiden("LancingBlowSpread");
            encounter.ResetRaiden("SilentLevin");
        });
    }

    private void RaidenCloudEnd(uint id, float duration)
        => Add(id, duration, "Exaflares end", () => encounter.Exaflares.Count == 0, true, exit: () =>
        {
            encounter.ResetRaiden("SilentLevin");
            encounter.ResetRaiden("BitterBarbs");
        });

    private void RaidenCloudSilent(uint id, float delay, bool booming)
    {
        var next = id;
        if (booming)
        {
            RawCast(next, delay, 4, [14461], "Circle AOE");
            next += 0x10;
            delay = 8;
        }
        Add(next, delay, "Exaflares start", () => encounter.Exaflares.Count != 0, true);
        RaidenCondition(next + 0x10, 15, "SilentLevin", n => n != 0, "Baited circles 1");
        Add(next + 0x20, .5f, "Chains", () => encounter.RaidenTethersAssigned, true);
        RaidenCondition(next + 0x30, 1.2f, "SilentLevin", n => n > 3, "Baited circles 2");
        RaidenCondition(next + 0x40, 2, "SilentLevin", n => encounter.RaidenCount("SilentLevin") >= 3 && n > 3, "Baited circles 3");
        RaidenCloudEnd(next + 0x50, 6.2f);
    }

    private void RaidenCloudWhorl(uint id, float delay)
    {
        Add(id, delay, "Exaflares start", () => encounter.Exaflares.Count != 0, true);
        RawCast(id + 0x10, 6.3f, 8, [14446], "Raidwide", ScriptTimelineHint.Raidwide);
        Add(id + 0x20, 13.4f, "Exaflares end", () => encounter.Exaflares.Count == 0, true);
    }

    private void RaidenSilent(uint id, float delay)
    {
        RaidenCondition(id, delay, "SilentLevin", n => n != 0, "Baited circles 1");
        RaidenCondition(id + 0x10, 1.1f, "SilentLevin", n => n > 3, "Baited circles 2");
        RaidenCondition(id + 0x20, 2.1f, "SilentLevin", n => n > 6 || encounter.RaidenCount("SilentLevin") >= 3 && n > 3, "Baited circles 3");
        RaidenCondition(id + 0x30, 2.7f, "SilentLevin", n => n == 0, "Baits end", () => encounter.ResetRaiden("SilentLevin"));
    }

    private void RaidenWhirlingSilent(uint id, float delay)
    {
        RaidenCastStart(id, [14442], delay, "Donut AOE");
        RaidenCondition(id + 0x10, 5.1f, "SilentLevin", n => n != 0, "Baited circles 1");
        RaidenCastEnd(id + 0x20, .4f, "Donut AOE");
        RaidenCondition(id + 0x30, 1.6f, "SilentLevin", n => n > 3, "Baited circles 2");
        RaidenCondition(id + 0x40, 2.5f, "SilentLevin", n => n > 6 || encounter.RaidenCount("SilentLevin") >= 3 && n > 3, "Baited circles 3");
        RaidenCastStart(id + 0x50, [14461], 1.1f, "");
        RaidenCastEnd(id + 0x51, 4, "Circle AOE", () => encounter.ResetRaiden("SilentLevin"));
    }

    private void BuildRaiden()
    {
        if (encounter.RaidenPhase == 0)
        {
            RaidenSpirits(0, 7.8f);
            RaidenShingan(0x10000, 6);
            RawCast(0x20000, 8, 3, [14463], "Arena wall");
            RaidenAme(0x30000, 9.4f);
            RaidenWhirling(0x40000, 1.1f);
            RaidenSpirits(0x50000, 9);
            RaidenAme(0x60000, 6.3f);
            RaidenWhirling(0x70000, 1.1f);
            RaidenTargetable(0x80000, false, 3.6f);
            return;
        }
        const uint p = 0x1000000;
        RaidenLateral(p, 9);
        RaidenTargetable(p + 0x10000, true, 4.3f);
        RaidenSpirits(p + 0x20000, 2.6f);
        RaidenUltimate(p + 0x30000, 12.8f);
        RaidenSpirits(p + 0x40000, 6);
        RaidenCloudSilent(p + 0x50000, 2.4f, true);
        RaidenWhirling(p + 0x60000, 2.6f);
        RaidenSpirits(p + 0x70000, 6.1f);
        RaidenCloudWhorl(p + 0x80000, 13);
        RaidenAme(p + 0x90000, 5.4f);
        RaidenWhirling(p + 0xA0000, 1.1f);
        RaidenSilent(p + 0xB0000, 2);
        RaidenSpirits(p + 0xC0000, 5.3f);
        RaidenLateralUltimate(p + 0xD0000, 8.2f);
        RaidenSpirits(p + 0xE0000, 5.9f);
        RaidenShingan(p + 0xF0000, 1.9f);
        RaidenAme(p + 0x100000, 7.1f);
        RawCast(p + 0x110000, 1.1f, 4.5f, [14460], "Circle AOE");
        RaidenWhirlingSilent(p + 0x120000, 2.1f);
        RaidenSpirits(p + 0x130000, 7.8f);
        RaidenSpirits(p + 0x140000, 2.1f);
        RaidenCloudSilent(p + 0x150000, 16.2f, false);
        RaidenWhirling(p + 0x160000, .2f);
        RaidenSpirits(p + 0x170000, 6.2f);
        RaidenCloudWhorl(p + 0x180000, 13.1f);
        RaidenAme(p + 0x190000, 1.3f);
        RaidenWhirling(p + 0x1A0000, 1.2f);
        Add(p + 0xFF0000, 10, "???", () => false);
    }
}
