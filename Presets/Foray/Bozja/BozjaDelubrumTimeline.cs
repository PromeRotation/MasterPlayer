using MasterPlayer.Scripting;

namespace MasterPlayer.Mechanics.Bozja;

// Delubrum's actual cast/condition chains. Expected durations describe the
// timeline; only Timeout and overdue component conditions advance by time.
internal sealed partial class BozjaDelubrumTimeline
{
    private readonly BozjaEncounter encounter;
    private readonly List<(uint Id, float Expected, string Name, Func<bool> Ready,
        bool Overdue, uint[] Actions, Action? Enter, Action? Exit, ScriptTimelineHint Hints, float MaxOverdue)> steps = [];
    private int index;
    private DateTime entered;
    internal bool Running { get; private set; }
    internal uint? StateId => Running ? steps[index].Id : null;
    internal List<string> Diagnostics { get; } = [];

    internal BozjaDelubrumTimeline(BozjaEncounter encounter)
    {
        this.encounter = encounter;
        if (encounter.IsLions) Add(0, 600, "???", () => false);
        else if (encounter.IsRhalgr) BuildRhalgr();
        else if (encounter.IsArt) BuildArt();
        else if (encounter.IsOwain) BuildOwain();
        else if (encounter.IsRaiden) BuildRaiden();
        else if (encounter.IsVirtue) BuildVirtue();
        else if (encounter.IsSavagePhantom) BuildPhantom();
        else BuildDahu();
    }

    private void BuildPhantom()
    {
        Cast(0, 7.1f, 4, [22461]);
        Add(2, .7f, "Raidwide", () => encounter.PhantomAgonyCount > 0, true,
            enter: () => encounter.ActivatePhantom("MaledictionOfAgony"),
            exit: () => encounter.DeactivatePhantom("MaledictionOfAgony"));
        Miasma(0x10000, 4.5f);
        Miasma(0x20000, 3);
        Cast(0x30000, .4f, 3, [22464]);
        Add(0x30010, 1, "Boss disappears", () => encounter.Primary?.Actor.Targetable == false,
            hints: ScriptTimelineHint.DowntimeStart);
        Add(0x30020, 3, "Adds appear", () => encounter.PhantomActiveBloody.Any(), true,
            enter: () =>
            {
                encounter.ActivatePhantom("BloodyWraith");
                encounter.ActivatePhantom("MistyWraith");
            });
        Start(0x30030, 2.1f, [22465]);
        Add(0x30040, 43, "Adds resolve", () => Elapsed >= 43);
        Add(0xFF0000, 100, "???", () => false);
    }

    private float Elapsed => (float)(encounter.Now - entered).TotalSeconds;

    internal void Begin()
    {
        index = 0;
        entered = encounter.Now;
        Running = true;
        if (encounter.IsSavagePhantom) encounter.ActivatePhantom("ArenaChange");
        steps[0].Enter?.Invoke();
    }

    internal void Stop()
    {
        if (Running) steps[index].Exit?.Invoke();
        Running = false;
        if (encounter.IsSavagePhantom) encounter.ClearPhantomComponents();
        else if (encounter.IsSavageDahu) encounter.ClearDahuComponents();
        else if (encounter.IsOwain) encounter.SetOwainImbas(false);
    }

    internal void Update()
    {
        if (!Running) return;
        if (encounter.Primary is not { Destroyed: false, Actor.Dead: false }
            || encounter.Primary.Actor.Hp == 0 && !(encounter.IsRaiden && encounter.RaidenPhase == 0))
        {
            Stop();
            return;
        }
        while (index + 1 < steps.Count)
        {
            var step = steps[index];
            var ready = step.Ready();
            if (!ready && (!step.Overdue || Elapsed < step.Expected + step.MaxOverdue)) break;
            if (!ready) Diagnostics.Add($"State {step.Id:X}: transition triggered because of overdue");
            if (step.Actions.Length > 0 && encounter.Primary?.Actor.Cast is { } cast
                && !step.Actions.Contains(cast.ActionId))
                Diagnostics.Add($"State {step.Id:X}: unexpected cast start: got {cast.ActionId}");
            step.Exit?.Invoke();
            ++index;
            entered = encounter.Now;
            steps[index].Enter?.Invoke();
        }
    }

    internal ScriptTimeline? Capture()
    {
        if (!Running) return null;
        var end = encounter.Now.AddSeconds(-Math.Min(Elapsed, steps[index].Expected));
        var entries = new List<ScriptTimelineEntry>();
        for (var i = index; i < Math.Min(steps.Count, index + 16); ++i)
        {
            var step = steps[i];
            end = end.AddSeconds(step.Expected);
            var hints = step.Hints;
            if (encounter.IsSavagePhantom && step.Id == 2) hints |= ScriptTimelineHint.Raidwide;
            if (encounter.IsSavagePhantom && step.Id == 0x30020) hints |= ScriptTimelineHint.DowntimeEnd;
            if (i + 1 < steps.Count && (step.Id & 0xFFFF0000) == (steps[i + 1].Id & 0xFFFF0000))
                hints |= ScriptTimelineHint.GroupWithNext;
            entries.Add(new(step.Id, step.Name, "", end, hints));
        }
        var phase = encounter.IsRaiden ? encounter.RaidenPhase : 0;
        var phaseName = (encounter.IsBaldesion || encounter.IsRhalgr) ? "Boss death" : encounter.Definition.Name;
        if (encounter.IsLions) phaseName = "Single phase";
        if (encounter.IsRaiden && phase == 0) phaseName = "P1";
        return new(phase, phaseName, Elapsed, encounter.Now, entries);
    }

    private void Add(uint id, float expected, string name, Func<bool> ready, bool overdue = false,
        uint[]? actions = null, Action? enter = null, Action? exit = null, ScriptTimelineHint hints = default,
        float maxOverdue = 1)
        => steps.Add((id, expected, name, ready, overdue, actions ?? [], enter, exit, hints, maxOverdue));

    private void Start(uint id, float expected, uint[] actions, Action? enter = null)
        => Add(id, expected, "", () => encounter.Primary?.Actor.Cast != null, actions: actions, enter: enter,
            hints: ScriptTimelineHint.BossCastStart);

    private void RawCast(uint id, float delay, float duration, uint[] actions, string name,
        ScriptTimelineHint hints = default, Action? enter = null)
    {
        Start(id, delay, actions);
        Add(id + 1, duration, name, () => encounter.Primary?.Actor.Cast == null,
            enter: enter, hints: hints | ScriptTimelineHint.BossCastEnd);
    }

    private void Cast(uint id, float delay, float duration, uint[] actions, Action? enter = null, string name = "", Action? exit = null)
    {
        Start(id, delay, actions);
        Add(id + 1, duration, name, () => encounter.Primary?.Actor.Cast == null, enter: enter, exit: exit,
            hints: ScriptTimelineHint.BossCastEnd);
    }

    private void Miasma(uint id, float delay)
    {
        Cast(id, delay, 3, [22450], () =>
        {
            encounter.ActivatePhantom("CreepingMiasma");
            encounter.ActivatePhantom("LingeringMiasma");
            encounter.ActivatePhantom("SwirlingMiasma");
            encounter.ActivatePhantom("MiasmaCounter");
        });
        Cast(id + 0x10, 7.1f, 9, [22451, 23022], name: "Miasma start");
        Add(id + 0x20, 22.2f, "Miasma resolve", () => encounter.PhantomMiasmaCount >= 8, true,
            exit: () =>
            {
                encounter.DeactivatePhantom("CreepingMiasma");
                encounter.DeactivatePhantom("LingeringMiasma");
                encounter.DeactivatePhantom("SwirlingMiasma");
                encounter.DeactivatePhantom("MiasmaCounter");
            });
    }

    private Action EnterDahu(params string[] names) => () =>
    {
        foreach (var name in names) encounter.ActivateDahu(name);
    };

    private Action ExitDahu(params string[] names) => () =>
    {
        foreach (var name in names) encounter.DeactivateDahu(name);
    };

    private void DahuCondition(uint id, float expected, string component, Func<bool> condition, string name = "",
        Action? enter = null, Action? exit = null, float maxOverdue = 1)
        => Add(id, expected, name, () => !encounter.DahuHas(component) || condition(), true,
            enter: enter, exit: exit, maxOverdue: maxOverdue);

    private void BuildDahu()
    {
        DahuCondition(0, 10.3f, "FallingRock", () => encounter.DahuCasters("FallingRock") > 0,
            "Rocks 1 bait", EnterDahu("FallingRock"));
        Cast(0x10, 9.5f, 3, [22387], EnterDahu("HotCharge"), "Charge 1", ExitDahu("HotCharge"));
        Cast(0x20, 1.8f, 3, [22387], EnterDahu("HotCharge"), "Charge 2", ExitDahu("HotCharge", "FallingRock"));
        Cast(0x30, 1.8f, 5, [22388], EnterDahu("Firebreathe"), "Cone", ExitDahu("Firebreathe"));
        DahuCondition(0x10000, 14.5f, "HeadDown", () => encounter.DahuCasters("HeadDown") > 0,
            "Add charges begin", EnterDahu("HeadDown"));
        DahuSpit(0x10100, 4.8f);
        DahuShockwave(0x10200, 3.2f);
        DahuCondition(0x10300, 1, "HeadDown", () => encounter.DahuCasters("HeadDown") == 0,
            "Add charges resolve", exit: ExitDahu("HeadDown"), maxOverdue: 5);
        Cast(0x10400, 1.6f, 5, [22375], EnterDahu("HuntersClaw", "FeralHowl"));
        DahuCondition(0x10410, 2.1f, "FeralHowl", () => encounter.DahuCount("FeralHowl") > 0,
            "Knockback", exit: ExitDahu("FeralHowl"));
        DahuCondition(0x10420, 1.4f, "HuntersClaw", () => encounter.DahuCount("HuntersClaw") > 0,
            "Add aoes", exit: ExitDahu("HuntersClaw"));
        DahuRotation(0x20000, 1.6f);
        Add(0x30000, 7.4f, "Adds appear", () => encounter.DahuCrowned.Any(a => a.Actor.Targetable), true);
        DahuCondition(0x40000, 37, "FallingRock", () => encounter.DahuCasters("FallingRock") > 0,
            "Rocks 1 bait", EnterDahu("FallingRock"));
        DahuCondition(0x40001, .2f, "HeadDown", () => encounter.DahuCasters("HeadDown") > 0,
            "Add charges begin", EnterDahu("HeadDown"));
        DahuShockwave(0x40100, 3.9f);
        DahuSpit(0x40200, 2.5f, ["FallingRock", "HeadDown"]);
        DahuCondition(0x40300, 1, "Burn", () => encounter.DahuBurns.Count > 0, enter: EnterDahu("Burn"));
        Cast(0x40400, .1f, 5, [22392], EnterDahu("HuntersClaw", "HystericAssault"));
        DahuCondition(0x40310, .9f, "HystericAssault", () => encounter.DahuCount("HystericAssault") > 0,
            "Knockback", exit: ExitDahu("HystericAssault"));
        DahuCondition(0x40320, 2.2f, "Burn", () => encounter.DahuCount("Burn") > 0,
            "Flares", exit: ExitDahu("Burn"));
        DahuCondition(0x40330, .4f, "HuntersClaw", () => encounter.DahuCount("HuntersClaw") > 0,
            "Add aoes", exit: ExitDahu("HuntersClaw"));
        DahuRotation(0x50000, 6.1f, true);
        Add(0xFF0000, 100, "???", () => false);
    }

    private void DahuSpit(uint id, float delay, string[]? extraExit = null)
    {
        Start(id, delay, [22390], EnterDahu("SpitFlame"));
        Add(id + 1, 8, "", () => encounter.Primary?.Actor.Cast == null, hints: ScriptTimelineHint.BossCastEnd);
        DahuCondition(id + 0x10, 3.7f, "SpitFlame", () => encounter.DahuCasters("SpitFlame") == 0,
            "Spits resolve", exit: ExitDahu(["SpitFlame", .. (extraExit ?? [])]));
    }

    private void DahuShockwave(uint id, float delay)
    {
        Cast(id, delay, 3, [22383, 22384], EnterDahu("Shockwave"), "Shockwave 1");
        Cast(id + 0x10, 1.6f, 1, [22385, 22386], name: "Shockwave 2", exit: ExitDahu("Shockwave"));
    }

    private void DahuRotation(uint id, float delay, bool withHeadDown = false)
    {
        Start(id, delay, [22379], EnterDahu("FirebreatheRotating"));
        Add(id + 1, 5, "", () => encounter.Primary?.Actor.Cast == null,
            enter: withHeadDown ? EnterDahu("HeadDown") : null, hints: ScriptTimelineHint.BossCastEnd);
        for (var count = 1; count <= 5; ++count)
        {
            var wanted = count;
            var name = "";
            if (count == 1) name = "Cone 1";
            if (count == 5) name = "Cone 5";
            DahuCondition(id + 0xFu + (uint)count, count == 1 ? .7f : 2,
                "FirebreatheRotating", () => encounter.NumDahuRotations >= wanted,
                name,
                exit: count == 5 ? ExitDahu(withHeadDown ? ["FirebreatheRotating", "HeadDown"] : ["FirebreatheRotating"]) : null);
        }
    }
}
