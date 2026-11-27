using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;
using OwainSource = BossMod.Stormblood.Foray.BaldesionArsenal.BA1Owain;

internal static partial class BozjaRegression
{
    private static void RunOwain(Action<string, Action> run)
    {
        run("Bozja pending BA1Owain exact client manifest resolved arena ten permanent components", OwainManifest);
        foreach (var rule in BozjaOwainCatalog.Definition.Rules.Where(r => r.Component != "LegendaryImbas"
            && r.Kind is BozjaRuleKind.Aoe or BozjaRuleKind.Spread or BozjaRuleKind.Tankbuster or BozjaRuleKind.Raidwide or BozjaRuleKind.OwainCastHint))
            foreach (var action in rule.Actions)
                run($"Bozja pending BA1Owain real Feed standard {rule.Component}/{action}", () =>
                {
                    using var f = new CastrumFixture(owain: true); f.Frame(0);
                    f.Change(20, a => a with { Cast = f.Spell(action, 1) }); f.Frame(1);
                    f.Change(100, a => a with { Position = a.Position + Vector3.UnitZ * 5 }); f.Frame(2);
                    f.Frame(50); f.Change(20, a => a with { Cast = null }); f.Frame(51);
                });
        run("Bozja pending BA1Owain Elemental raw sixty slots lazy spears before boss FIFO no Finish", OwainElements);
        run("Bozja pending BA1Owain Spirit original icons draw nearest changes target StatusLose clears", OwainSpirit);
        run("Bozja pending BA1Owain inverted gaze all tether IDs original generations first living source", OwainGaze);
        run("Bozja pending BA1Owain Aero untargetable dead targets real petrification and cleave navigation", OwainAero);
        run("Bozja pending BA1Owain LegendaryImbas temporary cold activation raw dead Spread conditions", OwainImbas);
        run("Bozja pending BA1Owain all457 raw states exact names hints clocks permanent tail", OwainTimeline);
        run("Bozja pending BA1Owain separate T827 regions retain exact Art Owain ownership", OwainIsolation);
        run("Bozja pending BA1Owain cold raw slots and actor order affect Elemental start", OwainColdElements);
        run("Bozja pending BA1Owain Spirit ties dead original targets Pet Lose and current generation", OwainSpiritOriginals);
        run("Bozja pending BA1Owain gaze zero hot missing target cold and exact heading range", OwainGazeBoundaries);
        run("Bozja pending BA1Owain cleave raw hint origin differs from quantized navigation", OwainCleaveBoundary);
        run("Bozja pending BA1Owain Imbas overdue boundary repeated cold activation and death", OwainImbasBoundaries);
        run("Bozja pending BA1Owain cold nonzero tether with zero target retains null tuple", OwainColdNullTether);
    }

    private static void OwainManifest()
    {
        using var f = new CastrumFixture(owain: true); f.Frame(0);
        var name = Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(7970)?.Singular.ToString();
        Check(name == f.Native.Definition.Name, "Owain exact client name: " + name);
        foreach (var action in Enum.GetValues<OwainSource.AID>())
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "Owain source action " + action);
        Check(f.Native.ActiveRules.Count() == 10, "Owain ten permanent components");
        for (var x = 98f; x < 160; x += .371f)
            for (var z = 716f; z < 779; z += .419f)
                Check(f.Source!.Arena.InBounds(new(x, z)) == f.Native.Arena.Contains(new(x, 0, z), 0), "Owain actual clipped arena");
    }

    private static void OwainElements()
    {
        using var f = new CastrumFixture(owain: true); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x265E, f.Center + Vector3.UnitZ * 8) with
        {
            Dead = true, Targetable = false,
            Statuses = [new ReplayStatus(1783, 0x121, 0, START.AddSeconds(-1)) { SlotIndex = 59 }],
        });
        f.Actors.Add(Cll1Fixture.Actor(31, 0x265E, f.Center + Vector3.UnitX * 8) with
        {
            Statuses = [new ReplayStatus(1784, 0x122, 0, START.AddSeconds(40)) { SlotIndex = 0 }],
        });
        f.Frame(.5);
        f.Change(20, a => a with { Cast = f.Spell(14650, 1, total: .01f) }); f.Frame(1);
        Check(f.Native.OwainElements.Count == 2 && f.Native.OwainElements[0].Cast.Origin
            == MasterPlayer.Mechanics.Dungeons.DungeonEncounter.QuantizedDungeonPosition(f.Native.Actors[30].Actor.Position),
            "Owain dead untargetable expired-but-present Fire spear before boss");
        f.Change(20, a => a with { Cast = null }); f.Frame(2);
        f.Actors.RemoveAll(a => a.Id == 30); f.Frame(3);
        f.Change(20, a => a with { Cast = f.Spell(14651, 4) }); f.Frame(4);
        Check(f.Native.OwainElements.Count == 4, "Owain separate Ice append no element dedup");
        f.Effect(5, 14653, source: 1);
        Check(f.Native.OwainElements.Count == 3 && f.Native.OwainElements[0].Cast.Origin == f.Center, "Owain Ice Effect pops Fire FIFO head");
        f.Frame(80); f.Change(20, a => a with { Cast = null }); f.Frame(81);
        Check(f.Native.OwainElements.Count == 3, "Owain no timeout or Finish cleanup");
    }

    private static GenericStackSpread OwainSourceSpirit(CastrumFixture f)
        => f.Source!.Components.OfType<GenericStackSpread>().Single(c => c.GetType().Name == "Spiritcull");

    private static void OwainDraw(CastrumFixture f)
    {
        using (MechanicDrawSink.Install(new(new())))
            OwainSourceSpirit(f).DrawArenaForeground(0, f.Viewer);
        f.Native.ObserveOwainDrawing(f.NativeViewer);
        var source = OwainSourceSpirit(f).Stacks.Single();
        Check(source.Target.InstanceID == f.Native.OwainSpiritStack!.Target!.Actor.Id
            && source.Activation == f.Native.OwainSpiritStack.Activation, "Owain drawing mutates original target but not clock");
        f.Compare();
    }

    private static void OwainSpirit()
    {
        using var f = new CastrumFixture(owain: true); f.Frame(0);
        f.Frame(1, packet: new MechanicEventIcon(55, 107, 100), native: () => f.Region.Icon(100, 55, 107));
        f.Frame(1.1, packet: new MechanicEventIcon(55, 107, 101), native: () => f.Region.Icon(101, 55, 107));
        Check(f.Native.OwainSpiritStack!.Target == f.NativeViewer, "Owain no nearest target selection before draw");
        OwainDraw(f);
        Check(f.Native.OwainSpiritStack.Target!.Actor.Id == 101, "Owain participant foreground chooses other original Actor");
        f.Change(101, a => a with { Position = f.Center + Vector3.UnitZ * 13 }); f.Frame(2);
        f.Frame(2.1, packet: new MechanicEventIcon(55, 100, 100), native: () => f.Region.Icon(100, 55, 100));
        Check(f.Native.OwainSpiritTargets.Count == 3, "Owain duplicates not deduped");
        f.Change(107, a => a with { Statuses = [new ReplayStatus(1753, 0, 0, START.AddSeconds(3)) { SlotIndex = 59 }] }); f.Frame(3);
        f.Change(107, a => a with { Statuses = [] }); f.Frame(4);
        Check(f.Native.OwainSpiritStack == null && f.Native.OwainSpiritTargets.Count == 0, "Owain unrelated real StatusLose clears all");
    }

    private static void OwainGaze()
    {
        using var f = new CastrumFixture(owain: true); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x266B, f.Center + Vector3.UnitX * 20) with
        { Tethers = [new(999, 100)] }); f.Frame(1);
        f.Actors.Add(Cll1Fixture.Actor(31, 0x233C, f.Center + Vector3.UnitZ * 20) with
        { Tethers = [new(1, 100)] }); f.Frame(2);
        Check(f.Native.OwainEyes(f.NativeViewer).Single().SourceId == 30, "Owain any tether ID first source");
        f.Change(30, a => a with { Dead = true }); f.Frame(3);
        Check(f.Native.OwainEyes(f.NativeViewer).Single().SourceId == 31, "Owain skip dead source but not NPC type");
        var original = f.NativeViewer;
        var replacement = f.Actors.Single(a => a.Id == 100);
        f.Actors.RemoveAll(a => a.Id == 100); f.Frame(4, false);
        f.Actors.Add(replacement); f.Frame(5);
        Check(original.Destroyed && !f.Native.OwainEyes(f.NativeViewer).Any() && f.Native.OwainTethers.Count == 2,
            "Owain same ID replacement cannot inherit original target gaze");
        f.Change(31, a => a with { Tethers = [] }); f.Frame(6);
        Check(f.Native.OwainTethers.Count == 2, "Owain Untether current new target tuple does not remove old target tuple");
    }

    private static void OwainAero()
    {
        using var f = new CastrumFixture(owain: true); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x266B, f.Center) with { TargetId = 101, Targetable = false }); f.Frame(1);
        Check(f.Native.OwainCleaves().Count() == 1, "Owain untargetable Palm remains active cleave");
        f.Change(101, a => a with { Dead = true }); f.Frame(2);
        Check(f.Native.OwainCleaves().Count() == 1, "Owain dead target still resolves direction");
        f.Change(30, a => a with { Statuses = [new ReplayStatus(610, 0, 0, START.AddSeconds(-1)) { SlotIndex = 59 }] }); f.Frame(3);
        Check(!f.Native.OwainCleaves().Any(), "Owain present petrification filters despite expired clock");
        f.Change(30, a => a with { Statuses = [], TargetId = 100 }); f.Frame(4);
        f.Effect(5, 14657);
        Check(f.Native.OwainCleaves().Count() == 1, "Owain Aero Effect does not clear continuous cleave");
    }

    private static void OwainReach(CastrumFixture f, uint id)
    {
        for (var i = 0; i < 1000 && f.Native.OwainTimeline!.StateId != id; ++i)
        {
            var time = (f.Native.Now - START).TotalSeconds + 100;
            var start = f.Source!.StateMachine.ActiveState!.EndHint.HasFlag(StateMachine.StateHint.BossCastStart);
            f.Change(1, a => a with { Cast = start ? f.Spell(999, time) with { IsSpell = false } : null });
            f.Frame(time);
            OwainTimelineCompare(f);
        }
        Check(f.Native.OwainTimeline!.StateId == id, "Owain reaches state " + id.ToString("X"));
    }

    private static void OwainImbas()
    {
        using var f = new CastrumFixture(owain: true); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(14656, 1) }); f.Frame(1);
        Check(!f.Native.OwainImbasActive && !f.Native.Casts.Any(c => c.Rule.Component == "LegendaryImbas"), "Owain Imbas not phase permanent");
        OwainReach(f, 0x70000);
        var at = (f.Native.Now - START).TotalSeconds;
        f.Change(1, a => a with { Cast = f.Spell(14654, at) }); f.Frame(at);
        Check(f.Native.OwainImbasActive && f.Native.Casts.Count(c => c.Rule.Component == "LegendaryImbas") == 1,
            "Owain Imbas activated on CastEnd entry with existing cold caster");
        f.Change(20, a => a with { Cast = f.Spell(14655, at) });
        f.Change(100, a => a with { Dead = true });
        f.Change(1, a => a with { Cast = null }); f.Frame(at + .1);
        Check(f.Native.OwainTimeline!.StateId == 0x70010 && f.Native.OwainImbasActive, "Owain real Finish removed Imbas before condition");
        f.Change(20, a => a with { Cast = f.Spell(14656, at + .2) });
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, f.Center) with { Cast = f.Spell(14655, at + .2) });
        f.Frame(at + .2); OwainTimelineCompare(f);
        Check(!f.Native.OwainImbasActive && f.Native.OwainTimeline.StateId == 0x70020
            && !f.Native.Spreads(f.Native.Definition.Rules.Single(r => r.Component == "PiercingLight1")).Any(),
            "Owain Imbas deactivates on condition exit while dead raw Spread still blocks next condition");
        f.Change(21, a => a with { Cast = null }); f.Frame(at + .3); OwainTimelineCompare(f);
        Check(f.Native.OwainTimeline.StateId == 0x80000, "Owain only actual Finish releases dead raw Spread timeline");
    }

    private static void OwainTimelineCompare(CastrumFixture f)
    {
        var e = MasterPlayer.MechanicRuntime.MechanicTimeline.Capture(f.Source!)!;
        var a = f.Native.OwainTimeline!.Capture()!;
        Check(e.PhaseName == a.PhaseName && e.PhaseIndex == a.PhaseIndex && e.ElapsedInState == a.ElapsedInState
            && f.Source!.StateMachine.ActiveState!.ID == f.Native.OwainTimeline.StateId, "Owain timeline state outer fields");
        Check(e.Entries.Select(s => (s.StateId, s.Name, s.ExpectedEnd, (int)s.Hints))
            .SequenceEqual(a.Entries.Select(s => (s.StateId, s.Name, s.ExpectedEnd, (int)s.Hints))), "Owain exact 16-entry window");
    }

    private static void OwainTimeline()
    {
        using var f = new CastrumFixture(owain: true); f.Frame(0);
        var source = new List<StateMachine.State>();
        for (var s = f.Source!.StateMachine.Phases[0].InitialState; s != null; s = s.NextStates?.SingleOrDefault()) source.Add(s);
        var native = ((IEnumerable)typeof(BozjaDelubrumTimeline).GetField("steps", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(f.Native.OwainTimeline)!).Cast<ITuple>().ToArray();
        Check(source.Count == 457 && native.Length == 457 && source[^1].ID == 0x8910000, "Owain all457 states and true cumulative IDs");
        for (var i = 0; i < native.Length; ++i)
        {
            var hints = (int)(MasterPlayer.Scripting.ScriptTimelineHint)native[i][8]!;
            if (i + 1 < native.Length && ((uint)native[i][0]! >> 16) == ((uint)native[i + 1][0]! >> 16)) hints |= 1;
            Check(source[i].ID == (uint)native[i][0]! && source[i].Duration == (float)native[i][1]!
                && source[i].Name == (string)native[i][2]! && (int)source[i].EndHint == hints, "Owain source metadata " + i);
        }
        OwainReach(f, 0x8910000);
        f.Frame(200000); OwainTimelineCompare(f);
        Check(f.Native.OwainTimeline!.StateId == 0x8910000, "Owain permanent tail no invented timeout");
    }

    private static void OwainIsolation()
    {
        var art = new BozjaRegion(827, "BA1Art");
        var owain = new BozjaRegion(827, "BA1Owain");
        Check(art.Encounters.Single().IsArt && owain.Encounters.Single().IsOwain, "Owain independent same-territory production regions");
        Check(BozjaCatalog.Owns(827, 0x265A) && BozjaCatalog.Owns(827, 0x265D)
            && !BozjaCatalog.Owns(827, 0x2658), "Owain exact native primary ownership does not swallow remaining BA");
    }

    private static void OwainColdElements()
    {
        foreach (var spearFirst in new[] { false, true })
        {
            using var f = new CastrumFixture(combat: false, owain: true);
            var spear = Cll1Fixture.Actor(30, 0x265E, f.Center + Vector3.UnitZ * 9) with
            { Statuses = [new ReplayStatus(1783, 0, 0, START) { SlotIndex = 59 }] };
            if (spearFirst) f.Actors.Insert(0, spear); else f.Actors.Add(spear);
            f.Change(20, a => a with { Cast = f.Spell(14650, total: .01f) });
            f.Frame(0);
            f.Change(1, a => a with { InCombat = true }); f.Frame(1);
            Check(f.Native.OwainElements.Count == 2, "Owain cold Start sees all real slots regardless actor order");
            f.Change(20, a => a with { Cast = null }); f.Frame(2);
            f.Change(30, a => a with { Statuses = [new ReplayStatus(1784, 0, 0, START) { SlotIndex = 59 }] });
            f.Change(20, a => a with { Cast = f.Spell(14651, 3) }); f.Frame(3);
            Check(f.Native.OwainElements.Count == (spearFirst ? 4 : 3),
                "Owain hot Start observes actor submission order rather than future status state");
        }
    }

    private static void OwainSpiritOriginals()
    {
        using var f = new CastrumFixture(owain: true);
        f.Change(100, a => a with { Position = f.Center });
        f.Change(101, a => a with { Position = f.Center + Vector3.UnitX });
        f.Change(102, a => a with { Position = f.Center - Vector3.UnitX });
        f.Frame(0);
        foreach (var actor in new ulong[] { 100, 101, 102, 100 })
            f.Frame(1, packet: new MechanicEventIcon(55, 107, actor), native: () => f.Region.Icon(actor, 55, 107));
        OwainDraw(f);
        Check(f.Native.OwainSpiritStack!.Target!.Actor.Id == 101, "Owain equal distance retains first original target");
        f.Change(101, a => a with { Dead = true }); f.Frame(2);
        OwainDraw(f);
        Check(f.Native.OwainSpiritStack.Target.Actor.Id == 101, "Owain dead nearest stays selected but hides output");
        var replacement = f.Actors.Single(a => a.Id == 101) with { Dead = false, Position = f.Center + Vector3.UnitZ * 20 };
        f.Actors.RemoveAll(a => a.Id == 101); f.Frame(3);
        f.Actors.Add(replacement); f.Frame(4);
        OwainDraw(f);
        Check(f.Native.OwainSpiritStack.Target.Destroyed && f.Native.OwainSpiritTargets.Count == 4,
            "Owain same ID current generation does not replace destroyed historical target");
        f.Actors.Add(Cll1Fixture.Actor(30, 0x233C, f.Center) with
        { SubKind = 2, Statuses = [new ReplayStatus(1753, 0, 0, START) { SlotIndex = 59 }] });
        f.Frame(5);
        f.Change(30, a => a with { Statuses = [] }); f.Frame(6);
        Check(f.Native.OwainSpiritTargets.Count == 4, "Owain Pet Lose does not dispatch component status callback");
        f.Change(107, a => a with { Statuses = [new ReplayStatus(1753, 0, 0, START) { SlotIndex = 59 }] }); f.Frame(7);
        f.Change(107, a => a with { Statuses = [] }); f.Frame(8);
        Check(f.Native.OwainSpiritTargets.Count == 0, "Owain unrelated nonPet real Lose clears historical list");
    }

    private static void OwainGazeBoundaries()
    {
        using var f = new CastrumFixture(combat: false, owain: true);
        f.Change(100, a => a with { Position = f.Center });
        f.Actors.Add(Cll1Fixture.Actor(30, 0x266B, f.Center + Vector3.UnitZ * 10) with { Tethers = [new(0, 100)] });
        f.Frame(0);
        f.Change(1, a => a with { InCombat = true }); f.Frame(1);
        Check(f.Native.OwainTethers.Count == 0, "Owain cold ID0 tether is not replayed");
        f.Frame(2, packet: new MechanicEventTether(30, 0, 100), native: () => f.Region.Tether(30, 0, 100));
        Check(f.Native.OwainTethers.Count == 1, "Owain hot ID0 nonzero target tether dispatches");
        // ReplayActorState snapshots retain only nonzero IDs in the old Feed.
        // Keep a representable tether before the subsequent snapshot frames.
        f.Change(30, a => a with { Tethers = [new(999, 100)] });
        foreach (var heading in new[] { 0f, .5f, .8f, MathF.PI })
        {
            f.Change(100, a => a with { Rotation = heading }); f.Frame(3);
        }
        f.Change(30, a => a with { Position = f.Center + Vector3.UnitZ * 10001,
            Statuses = [new ReplayStatus(610, 0, 0, START) { SlotIndex = 59 }] }); f.Frame(4);
        Check(f.Native.OwainEyes(f.NativeViewer).Count() == 1 && !f.Native.GazeDirections(f.NativeViewer.Actor).Any()
            && f.Native.Hints(f.NativeViewer).Any(h => h.Text == "Face the hand to petrify it!"),
            $"Owain range limits navigation only and petrification does not hide eye or custom hint: eyes={f.Native.OwainEyes(f.NativeViewer).Count()} directions={f.Native.GazeDirections(f.NativeViewer.Actor).Count()} hints={string.Join("/", f.Native.Hints(f.NativeViewer).Select(h => h.Text))}");
        f.Frame(5, packet: new MechanicEventTether(30, 1, 999), native: () => f.Region.Tether(30, 1, 999));
        Check(f.Native.OwainTethers.Single().Target == null, "Owain missing world target stores null original tuple");
        f.Frame(6, packet: new MechanicEventTetherCancel(30), native: () => f.Region.Tether(30, 0, 0));
        Check(f.Native.OwainTethers.Count == 0, "Owain missing target exact tuple cancels");
    }

    private static void OwainCleaveBoundary()
    {
        using var f = new CastrumFixture(owain: true);
        var origin = new Vector3(129, 0, 748);
        f.Change(101, a => a with { Position = origin + Vector3.UnitZ * 10 });
        f.Actors.Add(Cll1Fixture.Actor(30, 0x266B, origin) with { TargetId = 101 });
        f.Frame(0);
        f.Change(100, a => a with { Position = new(129, 0, 754) }); f.Frame(1);
        for (var dz = 5.96f; dz <= 6.04f; dz += .002f)
        {
            f.Change(100, a => a with { Position = origin + Vector3.UnitZ * dz });
            f.Frame(1);
        }
    }

    private static void OwainImbasBoundaries()
    {
        using var f = new CastrumFixture(owain: true); f.Frame(0);
        OwainReach(f, 0x70000);
        var at = (f.Native.Now - START).TotalSeconds;
        f.Change(1, a => a with { Cast = f.Spell(14654, at) }); f.Frame(at);
        f.Change(1, a => a with { Cast = null }); f.Frame(at + .1);
        f.Frame(at + 1.199); OwainTimelineCompare(f);
        Check(f.Native.OwainImbasActive && f.Native.OwainTimeline!.StateId == 0x70010, "Owain before empty Imbas overdue retains component");
        f.Frame(at + 1.201); OwainTimelineCompare(f);
        Check(!f.Native.OwainImbasActive && f.Native.OwainTimeline!.StateId == 0x80000, "Owain overdue exits and clears Imbas");
        f.Change(20, a => a with { Cast = f.Spell(14656, at + 2) }); f.Frame(at + 2);
        OwainReach(f, 0x3F0010);
        var second = (f.Native.Now - START).TotalSeconds;
        f.Change(1, a => a with { Cast = f.Spell(14654, second) }); f.Frame(second);
        Check(f.Native.OwainImbasActive && f.Native.Casts.Count(c => c.Rule.Component == "LegendaryImbas") == 1,
            "Owain second temporary activation cold replays exactly once");
        f.Change(1, a => a with { Dead = true, Hp = 0 }); f.Frame(second + .1);
        Check(!f.Native.Active && !f.Native.OwainImbasActive && f.Native.Casts.Count == 0, "Owain boss death releases temporary component");
    }

    private static void OwainColdNullTether()
    {
        using var f = new CastrumFixture(combat: false, owain: true);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x266B, f.Center) with { Tethers = [new(999, 0)] });
        f.Frame(0);
        f.Change(1, a => a with { InCombat = true }); f.Frame(1);
        Check(f.Native.OwainTethers.Count == 1 && f.Native.OwainTethers[0].Target == null,
            "Owain cold ID condition retains null target tuple used by timeline");
    }
}
