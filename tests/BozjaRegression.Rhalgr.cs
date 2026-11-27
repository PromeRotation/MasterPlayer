using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private static void RunRhalgr(Action<string, Action> run)
    {
        foreach (var rule in AllianceTrashCatalog.Rhalgr.Rules.Where(r => r.Component is "LightningBolt" or "DestructiveStatic"))
            run($"Alliance trash source Rhalgr {rule.Component} hot cold raw Finish", () =>
                AllianceStandard(AllianceTrashCatalog.Rhalgr, rule, rule.Actions[0]));
        run("Alliance trash source Rhalgr exact clipped arena identity default HP0 lifecycle", RhalgrLifecycle);
        run("Alliance trash source Rhalgr Map repeated locked double cone real Effect no TTL", RhalgrCharge);
        run("Alliance trash source Rhalgr cold missing Map and counter no invented raidwide", RhalgrCold);
        run("Alliance trash source Rhalgr Boltloop duplicate origins wrong stage inclusive axis real Finish", RhalgrBoltloop);
        run("Alliance trash source Rhalgr Strike original targets Finish retained Effect source ID counter", RhalgrStrike);
        run("Alliance trash source Rhalgr Strike destroyed target replacement dead source and self facing", RhalgrStrikeReferences);
        run("Alliance trash source Rhalgr all32 state metadata actual transitions overdue reset order permanent tail", RhalgrTimeline);
        run("Alliance trash source Rhalgr early map counters puddles exact state reset cold Boltloop", RhalgrTimelineEarly);
        run("Alliance trash source Rhalgr complete natural action Effect Map chain all resets without overdue", RhalgrNatural);
        run("Alliance trash source Rhalgr HP0 and Director at Reset exit release current Boltloop and Strike", RhalgrExitCleanup);
    }

    private static void RhalgrLifecycle()
    {
        using var f = new CastrumFixture(false, alliance: AllianceTrashCatalog.Rhalgr); f.Frame(0);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(11274)?.Singular.ToString() == f.Native.Definition.Name
            && Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(866)?.TerritoryType.RowId == 1054,
            "Rhalgr actual client identity");
        f.Change(1, a => a with { InCombat = true }); f.Frame(1);
        for (var x = f.Center.X - 26; x <= f.Center.X + 26; x += .617f)
            for (var z = f.Center.Z - 26; z <= f.Center.Z + 26; z += .673f)
                Check(f.Source!.Arena.InBounds(new(x, z)) == f.Native.Arena.Contains(new(x, 0, z), 0), "Rhalgr exact clipped arena");
        foreach (var action in Enum.GetValues<BossMod.Endwalker.Alliance.A10RhalgrEmissary.AID>())
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == (action ==
                    BossMod.Endwalker.Alliance.A10RhalgrEmissary.AID.Boltloop ? 1 : 0), "Rhalgr action extra " + action);
        f.Change(1, a => a with { Hp = 0 }); f.Frame(2);
        Check(!f.Native.Active && f.Native.Completed && f.Native.RhalgrTimeline == null, "Rhalgr default HP0 completes and stops timeline");
    }

    private static void RhalgrMap(CastrumFixture f, double at, uint state = 0x00080004, byte index = 0x25)
        => f.Frame(at, packet: new MechanicEventMapEffect(index, state),
            native: () => f.Region.MapEffect(index, state));

    private static void RhalgrCharge()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Rhalgr); f.Frame(0);
        RhalgrMap(f, .1, 0x00020001); RhalgrMap(f, .2, index: 0x24);
        Check(f.Native.RhalgrChargeAoes == 0, "Rhalgr unrelated Map does not invent charge");
        f.Change(1, a => a with { Position = f.Center + new Vector3(1.237f, 0, 2.713f) });
        RhalgrMap(f, 1); RhalgrMap(f, 2, 0x00100004); RhalgrMap(f, 3);
        Check(f.Native.RhalgrChargeAoes == 6 && f.Native.Casts.Where(c => c.Rule.Component == "DestructiveCharge")
            .Select(c => c.Activation).SequenceEqual(new[] { 1d, 1, 2, 2, 3, 3 }.Select(t => START.AddSeconds(t).AddSeconds(16.1d))),
            "Rhalgr repeated Map appends both locked original NPC cones");
        f.Change(1, a => a with { Position = f.Center, Rotation = -1.7f }); f.Frame(100);
        Check(f.Native.RhalgrChargeAoes == 6, "Rhalgr no timeout or moving origin");
        f.Effect(101, 28890); Check(f.Native.RhalgrChargeAoes == 6, "Rhalgr visual does not consume");
        f.Effect(102, 28891); Check(f.Native.RhalgrChargeAoes == 0 && f.Native.RhalgrChargeCount == 1, "Rhalgr real helper Effect clears all");
        f.Effect(103, 28891); Check(f.Native.RhalgrChargeCount == 2, "Rhalgr empty Effect still counts");
    }

    private static void RhalgrCold()
    {
        using var f = new CastrumFixture(false, alliance: AllianceTrashCatalog.Rhalgr); f.Frame(0);
        RhalgrMap(f, 1); f.Effect(1.1, 28900);
        f.Change(20, a => a with { Cast = f.Spell(28893, at: 2, total: .02f) });
        f.Change(1, a => a with { InCombat = true }); f.Frame(2);
        Check(f.Native.RhalgrChargeAoes == 0 && f.Native.RhalgrBlueCount == 0
            && f.Native.Visible(f.NativeViewer).Single().Cast.Rule.Component == "Boltloop",
            "Rhalgr cold current Spell replay without historical Map Effect");
        f.Effect(3, 28900);
        Check(f.Native.RhalgrBlueCount == 1 && !f.Native.DamageForHost(f.NativeViewer).Any()
            && !f.Native.GlobalHints().Any(), "Rhalgr Blue CastCounter alone does not predict damage or add global hint");
        f.Actors.Add(Cll1Fixture.Actor(30, 0, f.Center) with { Kind = 1, SubKind = 4 });
        f.Effect(4, 28891, 30);
        Check(f.Native.RhalgrChargeCount == 0, "Rhalgr player Effect excluded by actual NPC dispatch");
    }

    private static void RhalgrBoltloop()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Rhalgr); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(28893, at: 1, total: .02f) }); f.Frame(1);
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, f.Center) with { Cast = f.Spell(28893, at: 1, total: .02f) }); f.Frame(1);
        f.Change(20, a => a with { Cast = null }); f.Frame(2);
        Check(f.Native.NumEarthCasts == 1 && f.Native.Visible().Count() == 2, "Rhalgr first source Finish advances only first duplicate");
        f.Change(21, a => a with { Cast = null }); f.Frame(3);
        foreach (var (action, at, point) in new[] {
            (28895u, 4d, f.Center + Vector3.UnitX * 9),
            (28894u, 6d, f.Center + new Vector3(1, 0, 1)),
            (28894u, 8d, f.Center),
            (28895u, 10d, f.Center), (28895u, 12d, f.Center) })
        {
            f.Change(20, a => a with { Cast = f.Spell(action, at: at, point: point) }); f.Frame(at);
            f.Change(20, a => a with { Cast = null }); f.Frame(at + 1);
        }
        Check(f.Native.NumEarthCasts == 7 && !f.Native.Visible().Any(),
            "Rhalgr mismatched nonempty stage counts inclusive axis match finishes two sequences");
        f.Change(20, a => a with { Cast = f.Spell(28894, at: 14) }); f.Frame(14);
        f.Change(20, a => a with { Cast = null }); f.Frame(15);
        Check(f.Native.NumEarthCasts == 7, "Rhalgr empty sequence Finish does not count");
    }

    private static void RhalgrStrike()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Rhalgr); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(28889, total: .02f) }); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, f.Center + Vector3.UnitZ) with { Cast = f.Spell(28889, at: 1, target: 101) }); f.Frame(1);
        f.Change(20, a => a with { Cast = null }); f.Change(21, a => a with { Cast = null }); f.Frame(2);
        Check(f.Native.DahuBaits.Count == 2 && f.Native.RhalgrStrikeCount == 0
            && f.Native.DamageForHost(f.NativeViewer).Single().Mask == 3, "Rhalgr Strike Finish retains both original baits and union target damage");
        f.Effect(3, 28889, 21);
        Check(f.Native.DahuBaits.Single().Source.Actor.Id == 20 && f.Native.RhalgrStrikeCount == 1,
            "Rhalgr Strike Effect matches source ID only and increments independent counter");
        f.Effect(4, 28889, 21); Check(f.Native.RhalgrStrikeCount == 2, "Rhalgr empty Strike source still counts");
        f.Effect(5, 28889); Check(!f.Native.DahuBaits.Any(), "Rhalgr final real Effect consumes remaining bait");
    }

    private static void RhalgrStrikeReferences()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Rhalgr); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(28889, target: 101) }); f.Frame(1);
        f.Actors.RemoveAll(a => a.Id == 101); f.Frame(2);
        Check(f.Native.DahuBaits.Single().Target.Destroyed, "Rhalgr destroyed target original bait remains");
        f.Actors.Add(Cll1Fixture.Actor(101, 0, f.Center) with { Kind = 1, SubKind = 4, Dead = true }); f.Frame(3);
        Check(f.Native.DahuBaits.Count == 1 && !f.Native.DahuBaits[0].Target.Actor.Dead,
            "Rhalgr same ID dead replacement does not replace original target");
        f.Change(20, a => a with { Dead = true }); f.Frame(4); Check(!f.Native.DahuBaits.Any(), "Rhalgr original source dead Update clears");
        f.Change(20, a => a with { Dead = false, Cast = null }); f.Frame(5);
        f.Change(20, a => a with { Cast = f.Spell(28889, at: 6, target: 20) }); f.Frame(6);
        Check(f.Native.DahuBaits.Single().Target == f.Native.DahuBaits.Single().Source, "Rhalgr self target uses original source facing");
    }

    private static void RhalgrTimelineCompare(CastrumFixture f)
    {
        var e = MechanicTimeline.Capture(f.Source!)!;
        var a = f.Native.RhalgrTimeline!.Capture()!;
        Check(e.PhaseName == a.PhaseName && e.PhaseIndex == a.PhaseIndex && e.ElapsedInState == a.ElapsedInState
            && f.Source!.StateMachine.ActiveState!.ID == f.Native.RhalgrTimeline.StateId, "Rhalgr timeline outer fields");
        Check(e.Entries.Select(x => (x.StateId, x.Name, x.ExpectedEnd, (int)x.Hints))
            .SequenceEqual(a.Entries.Select(x => (x.StateId, x.Name, x.ExpectedEnd, (int)x.Hints))), "Rhalgr exact next16 timeline");
    }

    private static void RhalgrReach(CastrumFixture f, uint id)
    {
        for (var i = 0; i < 100 && f.Native.RhalgrTimeline!.StateId != id; ++i)
        {
            var at = (f.Native.Now - START).TotalSeconds + 100;
            var start = f.Source!.StateMachine.ActiveState!.EndHint.HasFlag(StateMachine.StateHint.BossCastStart);
            f.Change(1, a => a with { Cast = start ? f.Spell(999, at) with { IsSpell = false } : null });
            f.Frame(at); RhalgrTimelineCompare(f); RhalgrCounters(f);
        }
        Check(f.Native.RhalgrTimeline!.StateId == id, "Rhalgr reaches state " + id.ToString("X"));
    }

    private static void RhalgrCounters(CastrumFixture f)
    {
        var counters = f.Source!.Components.OfType<CastCounter>().ToDictionary(c => c.GetType().Name);
        Check(counters["DestructiveCharge"].NumCasts == f.Native.RhalgrChargeCount
            && counters["BoltsFromTheBlue"].NumCasts == f.Native.RhalgrBlueCount
            && counters["DestructiveStrike"].NumCasts == f.Native.RhalgrStrikeCount
            && counters["Boltloop"].NumCasts == f.Native.NumEarthCasts, "Rhalgr independent counters");
    }

    private static void RhalgrRawCompare(CastrumFixture f)
    {
        RhalgrCounters(f);
        var expected = f.Source!.Components.OfType<GenericBaitAway>().Single().CurrentBaits;
        var actual = f.Native.DahuBaits;
        Check(expected.Count == actual.Count, "Rhalgr full original Strike bait list");
        for (var i = 0; i < actual.Count; ++i)
        {
            var e = expected[i]; var a = actual[i];
            Check(e.Source.InstanceID == a.Source.Actor.Id && e.Target.InstanceID == a.Target.Actor.Id
                && e.Source.IsDestroyed == a.Source.Destroyed && e.Target.IsDestroyed == a.Target.Destroyed
                && e.Source.IsDead == a.Source.Actor.Dead && e.Target.IsDead == a.Target.Actor.Dead
                && e.Source.Position.ToVec3() == a.Source.Actor.Position with { Y = 0 }
                && e.Target.Position.ToVec3() == a.Target.Actor.Position with { Y = 0 }
                && e.Activation == a.Activation, "Rhalgr Strike raw original refs states position clock " + i);
        }
    }

    private static void RhalgrTimeline()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Rhalgr); f.Frame(0);
        var source = new List<StateMachine.State>();
        for (var s = f.Source!.StateMachine.Phases[0].InitialState; s != null; s = s.NextStates?.SingleOrDefault()) source.Add(s);
        var native = ((IEnumerable)typeof(BozjaDelubrumTimeline).GetField("steps", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(f.Native.RhalgrTimeline)!).Cast<ITuple>().ToArray();
        Check(source.Count == 32 && native.Length == 32, "Rhalgr complete source state count");
        for (var i = 0; i < native.Length; ++i)
            Check(source[i].ID == (uint)native[i][0]! && source[i].Duration == (float)native[i][1]!
                && source[i].Name == (string)native[i][2]!, "Rhalgr all state metadata " + i);
        RhalgrTimelineCompare(f);
        RhalgrReach(f, 0xFF0000); f.Frame(100000); RhalgrTimelineCompare(f);
        Check(f.Native.RhalgrTimeline!.StateId == 0xFF0000
            && f.Native.ActiveRules.Select(r => r.Component).SequenceEqual(new[] {
                "LightningBolt", "DestructiveStrike", "DestructiveStatic", "Boltloop", "BoltsFromTheBlue", "DestructiveCharge" }),
            "Rhalgr permanent tail and final Reset append order");
    }

    private static void RhalgrTimelineEarly()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Rhalgr); f.Frame(0);
        f.Change(1, a => a with { Cast = f.Spell(28896, at: .1) }); f.Frame(.1); RhalgrTimelineCompare(f);
        f.Change(1, a => a with { Cast = null }); f.Frame(.2);
        Check(f.Native.RhalgrTimeline!.StateId == 0x10000, "Rhalgr early cast start end no expected-time gate");
        RhalgrMap(f, .3); Check(f.Native.RhalgrTimeline.StateId == 0x10010, "Rhalgr early Map count condition");
        f.Change(1, a => a with { Cast = f.Spell(28897, at: .4) }); f.Frame(.4);
        f.Effect(.5, 28891); RhalgrTimelineCompare(f);
        Check(f.Native.RhalgrTimeline.StateId == 0x10030 && f.Native.RhalgrChargeCount == 0, "Rhalgr actual early count triggers Exit Reset");
        f.Change(1, a => a with { Cast = null }); f.Frame(.6);
        f.Change(20, a => a with { Cast = f.Spell(28898, at: .7) }); f.Frame(.7);
        Check(f.Native.RhalgrTimeline.StateId == 0x10050, "Rhalgr early puddle count observes live component");
        f.Change(20, a => a with { Cast = null }); f.Frame(.8);
        Check(f.Native.RhalgrTimeline.StateId == 0x20000, "Rhalgr empty puddles resolve immediately");
        RhalgrReach(f, 0x40030);
        var at = (f.Native.Now - START).TotalSeconds;
        f.Change(20, a => a with { Cast = f.Spell(28893, at: at + .1) }); f.Frame(at + .1);
        var old = f.Source!.Components.Single(c => c.GetType().Name == "Boltloop");
        f.Frame(at + 4); RhalgrTimelineCompare(f);
        Check(!ReferenceEquals(old, f.Source.Components.Single(c => c.GetType().Name == "Boltloop"))
            && f.Native.NumEarthCasts == 0 && f.Native.Visible().Count(a => a.Cast.Rule.Component == "Boltloop") == 1,
            "Rhalgr overdue Exit Reset cold replays current NPC circle once without history");
        RhalgrCounters(f);
    }

    private static void RhalgrNatural()
    {
        using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Rhalgr); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, f.Center + Vector3.UnitX * 3));
        var at = 0d;
        void Frame()
        {
            f.Frame(at += .1); RhalgrTimelineCompare(f);
        }
        void BossCast(uint action)
        {
            f.Change(1, a => a with { Cast = action == 0 ? null : f.Spell(action, at + .1) }); Frame();
        }
        void Puddles(bool cast)
        {
            f.Change(20, a => a with { Cast = cast ? f.Spell(28898, at + .1) : null }); Frame();
        }
        void Charge(bool lightning)
        {
            RhalgrMap(f, at += .1); RhalgrTimelineCompare(f);
            BossCast(lightning ? 28897u : 28896u);
            f.Effect(at += .1, 28891); RhalgrTimelineCompare(f);
            BossCast(0);
            if (lightning) { Puddles(true); Puddles(false); }
        }
        void Blue()
        {
            BossCast(28899); BossCast(0);
            f.Effect(at += .1, 28900); RhalgrTimelineCompare(f);
            Check(f.Native.RhalgrBlueCount == 0, "Rhalgr natural Blue Reset clears consumed Effect count");
        }
        BossCast(28896); BossCast(0); Charge(true); Blue(); Charge(false);
        BossCast(28892); BossCast(0);
        foreach (var action in new uint[] { 28893, 28894, 28895 })
        {
            f.Change(20, a => a with { Cast = f.Spell(action, at + .1) });
            f.Change(21, a => a with { Cast = f.Spell(action, at + .1, point: f.Center + Vector3.UnitX * 3) }); Frame();
            f.Change(20, a => a with { Cast = null }); Frame();
            f.Change(21, a => a with { Cast = null }); Frame();
        }
        Check(f.Native.NumEarthCasts == 0 && f.Native.RhalgrTimeline!.StateId == 0x50000,
            "Rhalgr six natural concentric Finishes Reset component and clear cache");
        BossCast(28889); BossCast(0);
        f.Effect(at += .1, 28889, 1); RhalgrTimelineCompare(f);
        Blue(); Charge(true);
        Check(f.Native.RhalgrTimeline!.StateId == 0xFF0000 && f.Native.RhalgrTimeline.Diagnostics.Count == 0,
            "Rhalgr complete normal chain has no overdue or unexpected cast diagnostics");
    }

    private static void RhalgrExitCleanup()
    {
        foreach (var director in new[] { false, true })
        {
            using var f = new CastrumFixture(alliance: AllianceTrashCatalog.Rhalgr); f.Frame(0);
            RhalgrReach(f, 0x40030);
            var at = (f.Native.Now - START).TotalSeconds;
            f.Change(20, a => a with { Cast = f.Spell(28889, at + .1) });
            f.Actors.Add(Cll1Fixture.Actor(21, 0x233C, f.Center) with { Cast = f.Spell(28893, at + .1) });
            f.Frame(at + .1);
            Check(f.Native.DahuBaits.Count == 1 && f.Native.Visible().Any(), "Rhalgr Reset boundary has current bait and sequence");
            if (director)
                f.Frame(at + .2, packet: new MechanicEventDirectorUpdate(0, 0x40000005, 0, 0, 0, 0),
                    native: () => f.Region.Director(0x40000005));
            else
            {
                f.Change(1, a => a with { Hp = 0 }); f.Frame(at + .2);
            }
            Check(!f.Native.Active && f.Native.RhalgrTimeline == null && !f.Native.ActiveRules.Any()
                && !f.Native.DahuBaits.Any() && !f.Native.Casts.Any() && !f.Native.Visible().Any()
                && !f.Native.Navigation(f.NativeViewer).Any(), "Rhalgr Stop Reset cannot leak rebuilt current cast output");
        }
    }
}
