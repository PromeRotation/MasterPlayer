using C = MasterPlayer.Mechanics.Dungeons.ThordanComponent;
using MasterPlayer.Scripting;

namespace MasterPlayer.Mechanics.Dungeons;

// Native encounter timeline. No predicted cast end advances a raw-cast state.
// Phase checks run before state checks; knight caching remains in the encounter's
// post-timeline update so newly found knights cannot skip the Add phase early.
internal sealed class ThordanTimeline
{
    private readonly ThordanEncounter encounter;
    private readonly List<ThordanTimelineStep>[] phases = [[], [], []];
    private int index;
    private DateTime entered;
    internal int Phase { get; private set; } = -1;
    internal uint? StateId => Phase is >= 0 and < 3 ? phases[Phase][index].Id : null;
    internal IReadOnlyList<ThordanTimelineStep> Steps(int phase) => phases[phase];

    internal ThordanTimeline(ThordanEncounter encounter)
    {
        this.encounter = encounter;
        BuildPhase1();
        BuildAddPhase();
        BuildPhase2();
        foreach (var step in phases.SelectMany(p => p))
            ThordanTimelineMetadata.Apply(step);
    }

    internal ScriptTimeline? Capture(DateTime now)
    {
        if (Phase is < 0 or >= 3) return null;
        var steps = phases[Phase];
        var elapsed = (float)(now - entered).TotalSeconds;
        var end = now.AddSeconds(-Math.Min(elapsed, steps[index].Expected));
        var entries = new List<ScriptTimelineEntry>();
        foreach (var step in steps.Skip(index).Take(16))
        {
            end = end.AddSeconds(step.Expected);
            entries.Add(new(step.Id, step.Name, step.Comment, end, step.Hints));
        }
        return new(Phase, ThordanTimelineMetadata.PhaseName(Phase), elapsed, now, entries);
    }

    internal void Start(DateTime now)
    {
        Phase = 0;
        index = 0;
        entered = now;
        Apply(phases[0][0].Enter);
    }

    internal void Update(DateTime now)
    {
        while (Phase is >= 0 and < 3 && PhaseFinished())
        {
            Apply(phases[Phase][index].Exit);
            encounter.ClearComponents();
            if (Phase == 0) encounter.ReducedArena = true;
            ++Phase;
            index = 0;
            entered = now;
            if (Phase < 3) Apply(phases[Phase][0].Enter);
            else encounter.FinishTimeline();
        }
        if (Phase is < 0 or >= 3) return;
        var steps = phases[Phase];
        while (index + 1 < steps.Count)
        {
            var state = steps[index];
            var ready = state.Ready();
            if (!ready && (!state.Timeout || (float)(now - entered).TotalSeconds < state.Expected + state.Overdue))
                break;
            if (state.ConditionComponent is { } component && !encounter.Has(component))
                encounter.ReportTimelineDiagnostic($"State {state.Id:X}: component {component} needed for condition is missing");
            else if (!ready)
                encounter.ReportTimelineDiagnostic($"State {state.Id:X}: transition triggered because of overdue");
            if (state.ExpectedAction != 0 && encounter.Primary?.Cast is { } cast && cast.Action != state.ExpectedAction)
                encounter.ReportTimelineDiagnostic($"State {state.Id:X}: unexpected cast start: got {cast.Action}, expected {state.ExpectedAction}");
            Apply(state.Exit);
            ++index;
            entered = now;
            Apply(steps[index].Enter);
        }
    }

    internal void Stop()
    {
        if (Phase is >= 0 and < 3) Apply(phases[Phase][index].Exit);
        encounter.ClearComponents();
        Phase = -1;
        index = 0;
        entered = default;
    }

    private bool PhaseFinished() => Phase switch
    {
        0 => encounter.Primary is { Destroyed: true } || encounter.Primary?.Actor.Targetable == false,
        1 => encounter.Primary is { Destroyed: true } || encounter.KnightDead(encounter.Janlenoux) && encounter.KnightDead(encounter.Adelphel),
        2 => encounter.Primary is { Destroyed: true } || encounter.Primary?.Actor.Dead == true || encounter.Primary?.Actor.Hp == 0,
        _ => false,
    };

    private void Apply(IEnumerable<(C Component, bool Activate)> changes)
    {
        foreach (var (component, activate) in changes)
        {
            if (activate) encounter.Activate(component);
            else encounter.Deactivate(component);
        }
    }

    private ThordanTimelineStep Q(int phase, uint id, float expected, C component, Func<bool> condition, float overdue = 1)
    {
        var step = new ThordanTimelineStep(id, expected, () => !encounter.Has(component) || condition(), true)
        {
            Overdue = overdue,
            ConditionComponent = component,
        };
        phases[phase].Add(step);
        return step;
    }

    private ThordanTimelineStep N(int phase, uint id, float expected, C component, int count = 1)
        => Q(phase, id, expected, component, () => encounter.Count(component) >= count);

    private ThordanTimelineStep S(int phase, uint id, float expected)
    {
        var step = new ThordanTimelineStep(id, expected, () => encounter.Primary?.Cast != null, false);
        phases[phase].Add(step);
        return step;
    }

    private ThordanTimelineStep E(int phase, uint id, float expected)
    {
        var step = new ThordanTimelineStep(id, expected, () => encounter.Primary?.Cast == null, false);
        phases[phase].Add(step);
        return step;
    }

    private ThordanTimelineStep Cast(int phase, uint id, float delay, float duration)
    {
        S(phase, id, delay);
        return E(phase, id + 1, duration);
    }

    private void Targetable(int phase, uint id, float expected, bool targetable)
        => phases[phase].Add(new(id, expected, () => encounter.Primary?.Actor.Targetable == targetable, false));

    private void Might(int phase, uint id, float delay, bool arena = false)
    {
        var state = N(phase, id, delay, C.AscalonsMight).AddEnter(C.AscalonsMight).AddExit(C.AscalonsMight);
        if (arena) state.AddEnter(C.ArenaChange);
    }

    private ThordanTimelineStep Mercy(int phase, uint id, float delay)
        => Cast(phase, id, delay, 3).AddEnter(C.AscalonsMercy, C.AscalonsMercyHelper).AddExit(C.AscalonsMercy, C.AscalonsMercyHelper);

    private void Lightning(int phase, uint id, float delay)
    {
        S(phase, id, delay).AddEnter(C.LightningStorm);
        E(phase, id + 1, 3.5f);
        Q(phase, id + 2, .7f, C.LightningStorm, () => !encounter.ComponentActive(C.LightningStorm)).AddExit(C.LightningStorm);
    }

    private ThordanTimelineStep Rage(int phase, uint id, float delay)
        => Cast(phase, id, delay, 5).AddEnter(C.DragonsRage).AddExit(C.DragonsRage);

    private void BuildPhase1()
    {
        Might(0, 0, 6.1f);
        S(0, 0x10000, 7.1f);
        E(0, 0x10001, 2.7f).AddEnter(C.Meteorain);
        N(0, 0x10002, .3f, C.Meteorain).AddExit(C.Meteorain);
        Mercy(0, 0x20000, 2);
        Might(0, 0x30000, 3.5f);
        Cast(0, 0x40000, 2.1f, 3);
        Cast(0, 0x40010, 7.2f, 3).AddEnter(C.DragonsGaze).AddExit(C.DragonsGaze);
        Might(0, 0x50000, 5.2f);
        Lightning(0, 0x60000, 5.1f);
        Rage(0, 0x70000, 1.4f);
        Cast(0, 0x80000, 2.2f, 3);
        Might(0, 0x90000, 6.2f);
        Cast(0, 0xA0000, 2.2f, 4);
        Might(0, 0xB0000, 2.1f, true);
        Targetable(0, 0xC0000, 2, false);
    }

    private void BuildAddPhase()
    {
        const uint p = 0x01000000;
        Q(1, p, 18.3f, C.Heavensflame, () => encounter.ComponentActive(C.Heavensflame)).AddEnter(C.Heavensflame);
        Q(1, p + 0x10, 2, C.Conviction, () => encounter.ComponentActive(C.Conviction)).AddEnter(C.BurningChains, C.Conviction);
        Q(1, p + 0x20, 4, C.Heavensflame, () => !encounter.ComponentActive(C.Heavensflame)).AddExit(C.Heavensflame, C.BurningChains);
        Q(1, p + 0x30, 3, C.Conviction, () => !encounter.ComponentActive(C.Conviction)).AddExit(C.Conviction);
        Q(1, p + 0x10000, 3, C.SerZephirin, () => encounter.ComponentActive(C.SerZephirin)).AddEnter(C.SerZephirin);
        Q(1, p + 0x10010, 20.1f, C.SerZephirin, () => !encounter.ComponentActive(C.SerZephirin))
            .AddEnter(C.SpiralThrust1).AddExit(C.SerZephirin);
        N(1, p + 0x10020, 6, C.SpiralThrust1).AddExit(C.SpiralThrust1);
        Q(1, p + 0x20000, 7, C.SwordShieldOfTheHeavens, () => encounter.ComponentActive(C.SwordShieldOfTheHeavens))
            .AddEnter(C.SwordShieldOfTheHeavens, C.HeavenlySlashAdelphel, C.HeavenlySlashJanlenoux);
        Q(1, p + 0x21000, 300, C.SwordShieldOfTheHeavens, () => !encounter.ComponentActive(C.SwordShieldOfTheHeavens))
            .AddEnter(C.HoliestOfHoly, C.SkywardLeap)
            .AddExit(C.HoliestOfHoly, C.SkywardLeap, C.HeavenlySlashAdelphel, C.HeavenlySlashJanlenoux, C.SwordShieldOfTheHeavens);
    }

    private void BuildPhase2()
    {
        const uint p = 0x02000000;
        Q(2, p, 5, C.SpiralPierce, () => encounter.ComponentActive(C.SpiralPierce), 100).AddEnter(C.SpiralPierce);
        Q(2, p + 0x10, 2.1f, C.DimensionalCollapse, () => encounter.ComponentActive(C.DimensionalCollapse)).AddEnter(C.DimensionalCollapse);
        Q(2, p + 0x20, .9f, C.HiemalStormSpread, () => encounter.ComponentActive(C.HiemalStormSpread)).AddEnter(C.HiemalStormSpread);
        N(2, p + 0x30, 3.2f, C.SpiralPierce).AddEnter(C.HiemalStormVoidzone)
            .AddExit(C.HiemalStormSpread).ActivateOnExit(C.FaithUnmoving).AddExit(C.SpiralPierce);
        N(2, p + 0x40, 1.8f, C.DimensionalCollapse).AddExit(C.DimensionalCollapse);
        N(2, p + 0x50, 3.1f, C.FaithUnmoving).AddExit(C.FaithUnmoving);
        Q(2, p + 0x1000, 3.4f, C.MeteorCircle, () => encounter.ComponentActive(C.MeteorCircle))
            .AddEnter(C.CometCircle, C.MeteorCircle, C.HeavyImpact);
        Q(2, p + 0x1010, 52.2f, C.MeteorCircle, () => !encounter.ComponentActive(C.MeteorCircle), 100);
        // Both source loops query CometCircle. Do not silently correct its OID.
        Q(2, p + 0x1020, 10, C.MeteorCircle, () => encounter.AllCometsDead)
            .AddExit(C.HeavyImpact, C.CometCircle, C.MeteorCircle, C.HiemalStormVoidzone);
        N(2, p + 0x10000, 6.3f, C.BossReappear).AddEnter(C.BossReappear, C.LightOfAscalon);
        N(2, p + 0x10010, 10.7f, C.LightOfAscalon).AddExit(C.BossReappear);
        N(2, p + 0x10020, 8.2f, C.LightOfAscalon, 7).AddExit(C.LightOfAscalon);
        N(2, p + 0x10030, 9.6f, C.UltimateEnd).AddEnter(C.UltimateEnd).AddExit(C.UltimateEnd);
        Targetable(2, p + 0x20000, 4.5f, true);
        Trio1(p + 0x30000);
        Cast(2, p + 0x40000, 9.2f, 3);
        Trio2(p + 0x50000);
        Might(2, p + 0x60000, 5.2f);
        Trio3(p + 0x70000);
        Cast(2, p + 0x80000, 7.2f, 4);
        Might(2, p + 0x90000, 2.1f);
        Trio4(p + 0xA0000);
        Trio5(p + 0xB0000);
        Cast(2, p + 0xC0000, 4.3f, 3);
        Cast(2, p + 0xD0000, 5.2f, 4);
        N(2, p + 0xE0000, 2.1f, C.AscalonsMight).AddEnter(C.AscalonsMight);
        foreach (var (offset, delay, count) in new[] { (1u, 5.2f, 2), (2u, 3.1f, 3), (3u, 3.1f, 4), (4u, 3.1f, 5) })
            N(2, p + 0xE0000 + offset, delay, C.AscalonsMight, count);
        N(2, p + 0xE0005, 3.1f, C.AscalonsMight, 6).AddExit(C.AscalonsMight);
        for (uint i = 0; i < 100; ++i) Cast(2, p + 0xF0000 + 2 * i, 2.1f, 10);
    }

    private void Trio1(uint id)
    {
        Cast(2, id, 3.2f, 3).AddEnter(C.DragonsGaze);
        Cast(2, id + 0x10, 7.2f, 3);
        Might(2, id + 0x20, 5.2f);
        N(2, id + 0x30, 6, C.HolyShieldBash).AddEnter(C.HolyShieldBash);
        Q(2, id + 0x40, 3.1f, C.HolyShieldBash, () => encounter.WildChargeSource != null);
        S(2, id + 0x41, 5);
        N(2, id + 0x42, 1, C.HolyShieldBash, 2).AddExit(C.HolyShieldBash);
        E(2, id + 0x43, 3);
        Cast(2, id + 0x50, 2.1f, 3).AddExit(C.DragonsGaze);
    }

    private void Trio2(uint id)
    {
        Cast(2, id, 2.2f, 3).AddEnter(C.DragonsGaze);
        Cast(2, id + 0x10, 7.2f, 3);
        Q(2, id + 0x20, 7.2f, C.Conviction, () => encounter.ComponentActive(C.Conviction)).AddEnter(C.Conviction);
        Cast(2, id + 0x30, 1, 3).AddEnter(C.HeavyImpact).AddExit(C.DragonsGaze);
        N(2, id + 0x40, 1.1f, C.HeavyImpact);
        N(2, id + 0x50, 2, C.HeavyImpact, 2).AddExit(C.Conviction);
        Q(2, id + 0x60, 1, C.DimensionalCollapse, () => encounter.ComponentActive(C.DimensionalCollapse)).AddEnter(C.DimensionalCollapse);
        N(2, id + 0x70, 1, C.HeavyImpact, 3);
        N(2, id + 0x80, 2, C.HeavyImpact, 4).AddEnter(C.FaithUnmoving).AddExit(C.HeavyImpact);
        N(2, id + 0x90, 3, C.DimensionalCollapse).AddExit(C.DimensionalCollapse);
        S(2, id + 0xA0, .2f).AddEnter(C.DragonsRage);
        N(2, id + 0xA1, 3, C.FaithUnmoving).AddExit(C.FaithUnmoving);
        E(2, id + 0xA2, 2).AddExit(C.DragonsRage);
    }

    private void Trio3(uint id)
    {
        Cast(2, id, 6.2f, 3);
        Cast(2, id + 0x10, 7.2f, 3).AddEnter(C.SpiralThrust2);
        Lightning(2, id + 0x20, 3.2f);
        Q(2, id + 0x30, 1.9f, C.SpiralPierce, () => encounter.ComponentActive(C.SpiralPierce)).AddEnter(C.SpiralPierce, C.SkywardLeap);
        S(2, id + 0x31, 2.9f);
        N(2, id + 0x32, 3.1f, C.SpiralPierce).AddEnter(C.DragonsRage)
            .AddExit(C.SpiralThrust2, C.SpiralPierce, C.SkywardLeap);
        E(2, id + 0x33, 1.9f).AddExit(C.DragonsRage);
    }

    private void Trio4(uint id)
    {
        Cast(2, id, 7.2f, 3).AddEnter(C.DragonsGaze);
        Cast(2, id + 0x10, 7.2f, 3);
        Might(2, id + 0x20, 6.2f);
        S(2, id + 0x30, 2.1f);
        Q(2, id + 0x31, 1, C.Heavensflame, () => encounter.ComponentActive(C.Heavensflame)).AddEnter(C.Heavensflame);
        E(2, id + 0x32, 2).AddEnter(C.BurningChains).AddExit(C.DragonsGaze);
        Q(2, id + 0x40, .9f, C.HiemalStormSpread, () => encounter.ComponentActive(C.HiemalStormSpread))
            .AddEnter(C.HiemalStormSpread).AddExit(C.BurningChains);
        Mercy(2, id + 0x50, 1.4f).AddEnter(C.HiemalStormVoidzone).AddExit(C.Heavensflame, C.HiemalStormSpread);
        Cast(2, id + 0x1000, 5.1f, 3);
        Cast(2, id + 0x2000, 2.1f, 4);
        Cast(2, id + 0x3000, 2.1f, 3).AddExit(C.HiemalStormVoidzone);
    }

    private void Trio5(uint id)
    {
        Cast(2, id, 2.2f, 3).AddEnter(C.DragonsGaze);
        Cast(2, id + 0x10, 7.2f, 3);
        Might(2, id + 0x20, 5.2f);
        N(2, id + 0x30, 5, C.HoliestOfHoly).AddEnter(C.HoliestOfHoly).AddExit(C.HoliestOfHoly);
        Might(2, id + 0x40, 5.2f);
        N(2, id + 0x50, 6.1f, C.HeavenswardLeap).AddEnter(C.HeavenswardLeap);
        N(2, id + 0x51, 3, C.HeavenswardLeap, 2);
        N(2, id + 0x52, 3, C.HeavenswardLeap, 3).AddExit(C.HeavenswardLeap);
        Q(2, id + 0x1000, 7.7f, C.SerZephirin, () => encounter.ComponentActive(C.SerZephirin)).AddEnter(C.SerZephirin);
        N(2, id + 0x1010, 6.1f, C.PureOfSoul).AddEnter(C.PureOfSoul).AddExit(C.PureOfSoul);
        N(2, id + 0x1020, 10.9f, C.AbsoluteConviction).AddEnter(C.AbsoluteConviction).AddExit(C.AbsoluteConviction);
        Cast(2, id + 0x1030, 2.3f, 3).AddExit(C.DragonsGaze);
        Q(2, id + 0x1040, 2.8f, C.SerZephirin, () => !encounter.ComponentActive(C.SerZephirin)).AddExit(C.SerZephirin);
    }
}
