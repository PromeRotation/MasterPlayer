using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;
using VirtueSource = BossMod.Stormblood.Foray.BaldesionArsenal.BA3AbsoluteVirtue;

internal static partial class BozjaRegression
{
    private static void RunVirtue(Action<string, Action> run)
    {
        run("Bozja pending BA3AbsoluteVirtue exact client manifest ten components true arena", VirtueManifest);
        foreach (var rule in BozjaVirtueCatalog.Definition.Rules.Where(r =>
            r.Kind is BozjaRuleKind.Aoe or BozjaRuleKind.Raidwide or BozjaRuleKind.TimedCastHint))
            foreach (var action in rule.Actions)
                run($"Bozja pending BA3AbsoluteVirtue real Feed standard {rule.Component}/{action}", () =>
                {
                    using var f = new CastrumFixture(virtue: true); f.Frame(0);
                    f.Change(20, a => a with { Cast = f.Spell(action, 1) }); f.Frame(1);
                    f.Frame(50); f.Change(20, a => a with { Cast = null }); f.Frame(51);
                });
        run("Bozja pending BA3AbsoluteVirtue Aurora caster real slots Bright cross-source FIFO Dark sticky", VirtueAurora);
        run("Bozja pending BA3AbsoluteVirtue Rays Primary real slots four actions locked shape any Finish clears", VirtueRays);
        run("Bozja pending BA3AbsoluteVirtue Wind raw moving target Dead and old generation ordinary Finish", VirtueWind);
        run("Bozja pending BA3AbsoluteVirtue Wind first dead caster retained prediction clock and duplicate Start", VirtueWindClock);
        run("Bozja pending BA3AbsoluteVirtue Explosion all tether IDs original source duplicate default AOE", VirtueExplosion);
        run("Bozja pending BA3AbsoluteVirtue towers raw Created20 EAnim exact position no timeout", VirtueTowers);
        run("Bozja pending BA3AbsoluteVirtue tower Allowed original target viewer based damage Update timing", VirtueTowerAllowed);
        run("Bozja pending BA3AbsoluteVirtue independent opposite tower lists and raw tether mutations", VirtueTwinTowers);
        run("Bozja pending BA3AbsoluteVirtue overlapping safe towers no internal boundary segment or contour", VirtueOverlappingTowers);
        run("Bozja pending BA3AbsoluteVirtue unresolved tower source exception explicit diagnostics retained null", VirtueTowerMissing);
        run("Bozja pending BA3AbsoluteVirtue cold real slots cast tether and twenty second creation clock", VirtueCold);
        run("Bozja pending BA3AbsoluteVirtue all92 state metadata rawcasts overdue persistent final state", VirtueTimeline);
        run("Bozja pending BA3AbsoluteVirtue exact Effect counters fresh activation and current Exit", VirtueCounters);
        run("Bozja pending BA3AbsoluteVirtue overcount three does not satisfy exact two before overdue Stop Exit", VirtueCounterOverflow);
        run("Bozja pending BA3AbsoluteVirtue death HP0 Destroy clears outputs and timeline", VirtueDeath);
    }

    private static void VirtueManifest()
    {
        using var f = new CastrumFixture(virtue: true);
        f.Change(100, a => a with { Position = f.Center + Vector3.UnitX * 200 }); f.Frame(0);
        Check(f.Native.Active && f.Native.ActiveRules.Count() == 10, "Virtue no Art Owain100 pull restriction");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(7976)?.Singular.ToString() == f.Native.Definition.Name,
            "Virtue actual client name " + Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(7976)?.Singular.ToString());
        foreach (var action in Enum.GetValues<VirtueSource.AID>())
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "Virtue client action " + action);
        Check(f.Native.Center == new Vector3(-174.8375f, 0, 313.95f), "Virtue actual resolved arena center");
        for (var x = -208f; x < -141; x += .431f)
            for (var z = 281f; z < 347; z += .517f)
                Check(f.Source!.Arena.InBounds(new(x, z)) == f.Native.Arena.Contains(new(x, 0, z), 0), "Virtue exact arena contour");
    }

    private static ReplayStatus VirtueStatus(uint id, int slot = 59)
        => new(id, 0, 1, START.AddSeconds(10000)) { SlotIndex = slot };

    private static void VirtueAurora()
    {
        using var f = new CastrumFixture(virtue: true); f.Frame(0);
        f.Change(20, a => a with { Statuses = [VirtueStatus(1711)], Cast = f.Spell(14218, 1) }); f.Frame(1);
        Check(f.Native.VirtueCasters("BrightDarkAurora") == 0, "Virtue hot Start sees old real status slots");
        f.Change(20, a => a with { Cast = null }); f.Frame(2);
        f.Change(20, a => a with { Cast = f.Spell(14231, 3) }); f.Frame(3);
        var first = f.Native.Casts.Single();
        f.Change(20, a => a with { Position = f.Center + Vector3.UnitX * 15, Cast = null, Statuses = [] }); f.Frame(4);
        Check(f.Native.Casts.Single() == first, "Virtue Dark Finish does not clear or retarget locked rectangle");
        f.Actors.Add(Cll1Fixture.Actor(21, 0x2628, f.Center) with { Cast = f.Spell(14217, 5) }); f.Frame(5);
        Check(f.Native.VirtueCasters("BrightDarkAurora") == 1, "Virtue no status Bright does not add");
        f.Change(21, a => a with { Cast = null }); f.Frame(6);
        Check(f.Native.VirtueCasters("BrightDarkAurora") == 0, "Virtue Bright unrelated source clears first Dark");
        f.Change(20, a => a with { Statuses = [VirtueStatus(1710, 58)] }); f.Frame(7);
        f.Change(20, a => a with { Cast = f.Spell(14230, 8) }); f.Frame(8);
        f.Frame(100);
        Check(f.Native.VirtueCasters("BrightDarkAurora") == 1, "Virtue no timeout");
        f.Change(20, a => a with { Cast = null }); f.Frame(101);
    }

    private static void VirtueRays()
    {
        foreach (var (status, action, radius) in new[] { (1711u, 14222u, 16f), (1711u, 14223u, 16f),
            (1710u, 14221u, 16f), (1710u, 14220u, 16f), (1710u, 14222u, 8f), (0u, 14221u, 8f) })
        {
            using var f = new CastrumFixture(virtue: true);
            f.Change(1, a => a with { Statuses = status == 0 ? [] : [VirtueStatus(status)] }); f.Frame(0);
            f.Change(20, a => a with { Cast = f.Spell(action, 1, point: f.Center + Vector3.UnitX * 4) }); f.Frame(1);
            Check(((MasterPlayer.Spatial.SafeZone.CircleTemplate)f.Native.Casts.Single().Shape).Radius == radius,
                "Virtue Rays Primary status mapping " + action);
            f.Actors.Add(Cll1Fixture.Actor(21, 0x2628, f.Center) with { Cast = f.Spell(14220, 2) }); f.Frame(2);
            f.Change(1, a => a with { Statuses = [VirtueStatus(1711)] }); f.Frame(3);
            Check(f.Native.VirtueCasters("AstralUmbralRays") == 2, "Virtue Rays status changes do not rewrite shape");
            f.Change(21, a => a with { Cast = null }); f.Frame(4);
            Check(f.Native.VirtueCasters("AstralUmbralRays") == 0, "Virtue Rays any ordinary Finish clears all sources");
        }
    }

    private static void VirtueWind()
    {
        using var f = new CastrumFixture(virtue: true); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(14234, 1, target: 101) }); f.Frame(1);
        var original = f.Native.Casts.Single().Target;
        f.Change(101, a => a with { Position = f.Center + new Vector3(1.237f, 0, 2.713f), Dead = true }); f.Frame(2);
        Check(ReferenceEquals(f.Native.Casts.Single().Target, original), "Virtue Wind Dead target remains original raw reference");
        f.Effect(3, 14234);
        Check(f.Native.Casts.Count == 1, "Virtue Wind Effect does not finish bait");
        f.Actors.RemoveAll(a => a.Id == 101); f.Frame(4);
        f.Actors.Add(Cll1Fixture.Actor(101, 0, f.Center + Vector3.UnitX * 25) with { Kind = 1, SubKind = 4 }); f.Frame(5);
        Check(!ReferenceEquals(f.Native.Actors[101], original) && original!.Destroyed,
            "Virtue same ID new target does not replace old bait reference");
        f.Change(20, a => a with { Cast = null }); f.Frame(6);
        Check(f.Native.Casts.Count == 0, "Virtue ordinary Finish clears first source ID bait");
    }

    private static void VirtueWindClock()
    {
        using var f = new CastrumFixture(virtue: true); f.Frame(0);
        f.Change(20, a => a with { Cast = f.Spell(14234, 1, target: 101) }); f.Frame(1);
        var clock = f.Native.Casts.Single().Activation;
        f.Actors.Add(Cll1Fixture.Actor(21, 0x2628, f.Center) with { Cast = f.Spell(14234, 2, target: 102) });
        f.Change(20, a => a with { Dead = true }); f.Frame(2);
        Check(f.Native.Damage(f.NativeViewer).Single().Activation == clock, "Virtue active second bait retains first raw clock");
        var source = f.Source!.Components.OfType<BaitAwayCast>().Single();
        source.OnCastStarted(f.Session.World.Actors.Find(21)!, f.Session.World.Actors.Find(21)!.CastInfo!);
        f.Native.Start(f.Native.Actors[21]); f.Compare();
        f.Change(21, a => a with { Cast = null }); f.Frame(3);
        Check(f.Native.Casts.Count == 2, "Virtue repeated Start then one Finish deletes only first match");
    }

    private static void VirtueExplosion()
    {
        using var f = new CastrumFixture(virtue: true); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(21, 0x25DF, f.Center)); f.Frame(.5);
        f.Change(20, a => a with { Tethers = [new(77, 100)] }); f.Frame(1);
        f.Change(21, a => a with { Tethers = [new(88, 100)] }); f.Frame(2);
        var empty = f.Native.Visible(f.NativeViewer).Single();
        Check(empty.Cast.Shape == null && !empty.Risky && f.Native.VirtueDiagnostics(f.NativeViewer).Count() == 1,
            "Virtue repeated viewer targets preserve default AOE and explicit diagnostic");
        var component = f.Source!.Components.Single(c => c.GetType().Name == "BrightDarkAuroraExplosion");
        var threw = false;
        using (MechanicDrawSink.Install(new(new())))
            try { component.DrawArenaBackground(0, f.Viewer); }
            catch (NullReferenceException) { threw = true; }
        Check(threw, "Virtue exact source duplicate default drawing exception");
        f.Change(21, a => a with { Tethers = [new(88, 101)] }); f.Frame(3);
        Check(f.Native.Visible(f.NativeViewer).Single().Risky == false, "Virtue own any tether suppresses others Risky");
        f.Actors.RemoveAll(a => a.Id == 20); f.Frame(4);
        Check(f.Native.Visible(f.NativeViewer).Single().Risky, "Virtue Untether on destruction removes original tuple");
    }

    private static void VirtueTowers()
    {
        using var f = new CastrumFixture(virtue: true); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x1EAA4A, f.Center + new Vector3(.123f, 0, 2.345f)));
        f.Frame(1);
        var tower = f.Native.Towers.Single();
        Check(tower.Origin == f.Actors.Single(a => a.Id == 30).Position && tower.Activation == START.AddSeconds(21)
            && f.Native.Soakers(tower).Count == 0, "Virtue exact raw tower creation plus20 with empty Allowed");
        f.Frame(100);
        Check(f.Native.Towers.Length == 1, "Virtue tower no twenty second timeout");
        f.Change(30, a => a with { Position = a.Position + Vector3.UnitX * .001f }); f.Frame(101);
        f.Frame(102, packet: new MechanicEventEObjAnimation(30, 4, 8), native: () => f.Region.EAnim(30, 0x40008));
        Check(f.Native.Towers.Length == 1, "Virtue EAnim requires exact saved position");
        f.Change(30, a => a with { Position = tower.Origin }); f.Frame(103);
        f.Frame(104, packet: new MechanicEventEObjAnimation(30, 4, 8), native: () => f.Region.EAnim(30, 0x40008));
        Check(f.Native.Towers.Length == 0, "Virtue EAnim clears first exact position");
    }

    private static void VirtueTowerAllowed()
    {
        using var f = new CastrumFixture(virtue: true); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x1EAA4A, f.Center));
        f.Change(20, a => a with { Tethers = [new(1, 100)] }); f.Frame(1);
        f.Change(101, a => a with { Position = f.Center }); f.Frame(2);
        Check(f.Native.Damage(f.NativeViewer).Single().Mask == 2, "Virtue allowed viewer predicts nonAllowed member in tower");
        var other = f.Native.Actors[101];
        var ai = new AIHints();
        foreach (var c in f.Source!.Components) c.AddAIHints(1, f.Session.World.Actors.Find(101)!, default, ai);
        Check(ai.PredictedDamage.Single().Players.Raw == 0 && f.Native.Damage(other).Single().Mask == 0,
            "Virtue nonAllowed viewer gets zero damage mask despite member inside");
        var old = f.NativeViewer;
        f.Actors.RemoveAll(a => a.Id == 100); f.Frame(3, compare: false);
        f.Actors.Add(Cll1Fixture.Actor(100, 0, f.Center) with { Kind = 1, SubKind = 4 }); f.Frame(4);
        f.Change(20, a => a with { Tethers = [] }); f.Frame(5);
        Check(f.Native.Soakers(f.Native.Towers.Single()).Contains(old)
            && !f.Native.Soakers(f.Native.Towers.Single()).Contains(f.NativeViewer),
            "Virtue Untether reFind current generation cannot remove old target tuple");
    }

    private static void VirtueTowerMissing()
    {
        using var f = new CastrumFixture(virtue: true); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x1EAA4A, f.Center));
        f.Change(20, a => a with { Tethers = [new(1, 999)] }); f.Frame(1, compare: false);
        var tower = f.Native.Towers.Single();
        Check(f.Native.Soakers(tower).Contains(null!) && f.Native.VirtueInvalidTower(tower.Rule),
            "Virtue null original target preserved in Allowed");
        var source = f.Source!.Components.OfType<GenericTowersOpenWorld>().Single(c => c.GetType().Name == "DarkAuroraTether");
        var threw = false;
        try { source.AddAIHints(0, f.Viewer, default, new()); }
        catch (NullReferenceException) { threw = true; }
        Check(threw && f.Native.VirtueDiagnostics(f.NativeViewer).Single().Contains("目标未解析"),
            "Virtue source null lookup exception explicit component diagnostic");
        Check(!f.Native.Navigation(f.NativeViewer).Any(z => z.Component == "DarkAuroraTether")
            && !f.Native.Damage(f.NativeViewer).Any()
            && f.Native.Navigation(f.NativeViewer).Any(z => z.Component == "BrightDarkAuroraExplosion"),
            "Virtue invalid component does not publish fabricated tower or damage");
        Check(f.Native.TowerViews(f.NativeViewer).Single().Background, "Virtue forbidden viewer retains valid short circuit tower background");
        using (MechanicDrawSink.Install(new(new()))) source.DrawArenaBackground(0, f.Viewer);
        f.Change(100, a => a with { Position = f.Center }); f.Frame(1.1, compare: false);
        var sourceHints = new BossComponent.TextHints();
        source.AddHints(0, f.Viewer, sourceHints);
        Check(sourceHints.Single().Item1 == "GTFO from tower!"
            && f.Native.Hints(f.NativeViewer).Any(h => h.Text == "GTFO from tower!"),
            "Virtue invalid Allowed still retains legal forbidden inside hint");
        f.Actors.Add(Cll1Fixture.Actor(21, 0x25DF, f.Center) with { Tethers = [new(1, 100)] }); f.Frame(1.2, compare: false);
        threw = false;
        using (MechanicDrawSink.Install(new(new())))
            try { source.DrawArenaForeground(0, f.Viewer); }
            catch (NullReferenceException) { threw = true; }
        Check(threw && f.Native.VirtueTowerSource(f.NativeViewer, tower.Rule) == null,
            "Virtue invalid mixed Allowed foreground failure does not publish unreachable line suffix");
        f.Change(20, a => a with { Tethers = [] }); f.Frame(2);
        Check(!f.Native.VirtueInvalidTower(tower.Rule), "Virtue current missing null Untether removes original null tuple");
    }

    private static void VirtueTwinTowers()
    {
        using var f = new CastrumFixture(virtue: true); f.Frame(0);
        f.Actors.Add(Cll1Fixture.Actor(30, 0x1EAA4A, f.Center));
        f.Actors.Add(Cll1Fixture.Actor(31, 0x1EAA49, f.Center + Vector3.UnitX * 4));
        f.Actors.Add(Cll1Fixture.Actor(21, 0x25DF, f.Center));
        f.Change(20, a => a with { Tethers = [new(1, 100)] });
        f.Change(21, a => a with { Tethers = [new(2, 101)] }); f.Frame(1);
        Check(f.Native.Towers.Length == 2 && f.Native.Damage(f.NativeViewer).Count() == 2
            && f.Native.Navigation(f.NativeViewer).Count() == 2,
            "Virtue two independent tower lists do not share target sets");
        var join = f.Native.Navigation(f.NativeViewer).Single(z => z.Join != null);
        var geometry = f.Native.ConvertNavigation(join, f.Center, f.Native.SampleRadius).Single();
        Check(!join.Convert(f.Center, f.Native.SampleRadius).Any(),
            "Virtue actual legacy sampler rejects mostly filled tower complement");
        for (var x = f.Center.X - 29; x < f.Center.X + 29; x += .137f)
            for (var z = f.Center.Z - 29; z < f.Center.Z + 29; z += .193f)
            {
                var point = new Vector3(x, 0, z);
                if (BozjaEncounter.DistanceSquared(point, f.Center) > 900) continue;
                Check(geometry.Contains(point, 0) == (join.Distance(x, z) <= 0), "Virtue direct host geometry preserves raw tower SDF sign");
            }
        f.Change(100, a => a with { Position = f.Center }); f.Frame(2);
        f.Change(20, a => a with { Tethers = [new(2, 100)] }); f.Frame(3);
        Check(f.Native.Soakers(f.Native.Towers.Single(t => t.Rule.Component == "DarkAuroraTether")).Count == 0
            && f.Native.Soakers(f.Native.Towers.Single(t => t.Rule.Component == "BrightAuroraTether")).Count == 2,
            "Virtue tether change Untether first then new rule and shared set Update");
    }

    private static void VirtueOverlappingTowers()
    {
        var center = new Vector3(-174.8375f, 0, 313.95f);
        var join = new BozjaZone("overlap", "DarkAuroraTether", START, null, center, 0,
            [center - Vector3.UnitX, center + Vector3.UnitX], 2);
        var geometry = new BozjaVirtueTowerGeometry(join, center, 30);
        var start = center - Vector3.UnitX * 1.2f;
        var end = center - Vector3.UnitX * .8f;
        Check(!geometry.Contains(start) && !geometry.Contains(end), "Virtue overlap endpoints safe");
        Check(!geometry.IntersectsSegment(start, end), "Virtue overlap no internal false segment boundary");
        foreach (var contour in geometry.GetContours())
            foreach (var point in contour)
                Check(join.Distance(point.X, point.Z) < .01f,
                    "Virtue overlap no contour vertex deep inside another safe tower");
        Check(geometry.IntersectsSegment(center - Vector3.UnitX * 4, center + Vector3.UnitX * 4),
            "Virtue overlap segment through danger remains blocked");
        Check(!geometry.IntersectsSegment(center - Vector3.UnitX * 2.5f, center + Vector3.UnitX * 2.5f),
            "Virtue overlapping union covers entire segment");
        Check(ReferenceEquals(geometry.GetContours(), geometry.GetContours()), "Virtue immutable default contour cache");
    }

    private static void VirtueCold()
    {
        using var f = new CastrumFixture(false, virtue: true); f.Frame(0);
        f.Change(20, a => a with { Statuses = [VirtueStatus(1711)], Cast = f.Spell(14218),
            Tethers = [new(1, 100)] });
        f.Actors.Add(Cll1Fixture.Actor(30, 0x1EAA4A, f.Center)); f.Frame(1);
        f.Change(1, a => a with { InCombat = true }); f.Frame(2);
        Check(f.Native.VirtueCasters("BrightDarkAurora") == 1 && f.Native.Towers.Single().Activation == START.AddSeconds(22),
            "Virtue percomponent cold replay current slots casts tethers and current creation clock");
        Check(f.Native.Soakers(f.Native.Towers.Single()).Contains(f.NativeViewer), "Virtue cold tether allowed after Update");
    }

    private static void VirtueTimelineCompare(CastrumFixture f)
    {
        var expected = MechanicTimeline.Capture(f.Source!)!;
        var actual = f.Native.VirtueTimeline!.Capture()!;
        Check(expected.PhaseName == actual.PhaseName && expected.PhaseIndex == actual.PhaseIndex
            && expected.ElapsedInState == actual.ElapsedInState
            && f.Source!.StateMachine.ActiveState!.ID == f.Native.VirtueTimeline.StateId, "Virtue timeline outer fields");
        Check(expected.Entries.Select(e => (e.StateId, e.Name, e.ExpectedEnd, (int)e.Hints))
            .SequenceEqual(actual.Entries.Select(e => (e.StateId, e.Name, e.ExpectedEnd, (int)e.Hints))), "Virtue exact next16 timeline");
    }

    private static void VirtueReach(CastrumFixture f, uint id)
    {
        for (var i = 0; i < 200 && f.Native.VirtueTimeline!.StateId != id; ++i)
        {
            var at = (f.Native.Now - START).TotalSeconds + 100;
            var start = f.Source!.StateMachine.ActiveState!.EndHint.HasFlag(StateMachine.StateHint.BossCastStart);
            f.Change(1, a => a with { Cast = start ? f.Spell(999, at) with { IsSpell = false } : null });
            f.Frame(at); VirtueTimelineCompare(f);
        }
        Check(f.Native.VirtueTimeline!.StateId == id, "Virtue reaches state " + id.ToString("X"));
    }

    private static void VirtueTimeline()
    {
        using var f = new CastrumFixture(virtue: true); f.Frame(0);
        var source = new List<StateMachine.State>();
        for (var s = f.Source!.StateMachine.Phases[0].InitialState; s != null; s = s.NextStates?.SingleOrDefault()) source.Add(s);
        var native = ((IEnumerable)typeof(BozjaDelubrumTimeline).GetField("steps", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(f.Native.VirtueTimeline)!).Cast<ITuple>().ToArray();
        Check(source.Count == 92 && native.Length == 92, "Virtue all92 states");
        for (var i = 0; i < native.Length; ++i)
        {
            var hints = (int)(MasterPlayer.Scripting.ScriptTimelineHint)native[i][8]!;
            if (i + 1 < native.Length && ((uint)native[i][0]! >> 16) == ((uint)native[i + 1][0]! >> 16)) hints |= 1;
            Check(source[i].ID == (uint)native[i][0]! && source[i].Duration == (float)native[i][1]!
                && source[i].Name == (string)native[i][2]! && (int)source[i].EndHint == hints, "Virtue state metadata " + i);
        }
        VirtueReach(f, 0x250020);
        f.Frame(100000); VirtueTimelineCompare(f);
        Check(f.Native.Active && f.Native.VirtueTimeline!.StateId == 0x250020, "Virtue final condition has no successor");
    }

    private static void VirtueCounters()
    {
        using var f = new CastrumFixture(virtue: true); f.Frame(0); VirtueReach(f, 0xF0000);
        Check(f.Native.ActiveRules.Last().Component == "BrightDarkAuroraCounter" && f.Native.VirtueCount("BrightDarkAuroraCounter") == 0,
            "Virtue counter fresh activation on condition Enter");
        var at = (f.Native.Now - START).TotalSeconds;
        f.Change(20, a => a with { Cast = f.Spell(14227, at) }); f.Frame(at + .1);
        f.Change(20, a => a with { Cast = null }); f.Frame(at + .2);
        Check(f.Native.VirtueTimeline!.StateId == 0xF0020, "Virtue counter condition waits after real group Finish");
        f.Effect(at + .3, 14218); f.Effect(at + .4, 14217);
        Check(f.Native.VirtueTimeline.StateId == 0xF0030, "Virtue precise two Effects advances early");
        f.Effect(at + .5, 14231); f.Effect(at + .6, 14230);
        Check(f.Native.VirtueTimeline.StateId == 0xF0040, "Virtue precise four Effects");
        f.Effect(at + .7, 14218); f.Effect(at + .8, 14217);
        Check(f.Native.VirtueTimeline.StateId == 0xF0050
            && !f.Native.ActiveRules.Any(r => r.Kind == BozjaRuleKind.VirtueCounter), "Virtue six Effects executes Exit deactivation");
        VirtueReach(f, 0x190010);
        Check(f.Native.VirtueCount("BrightDarkAuroraCounter") == 0, "Virtue later activation resets previous counter");
        VirtueReach(f, 0x250001);
        Check(f.Native.ActiveRules.Last().Component == "MeteorEnrageCounter", "Virtue enrage counter starts at CastEnd entry");
        at = (f.Native.Now - START).TotalSeconds;
        f.Change(1, a => a with { Cast = null }); f.Frame(at + .1);
        f.Effect(at + .2, 14703);
        Check(f.Native.VirtueTimeline!.StateId == 0x250020, "Virtue enrage actual Effect1 advances");
        f.Native.VirtueTimeline.Stop();
        Check(!f.Native.VirtueTimeline.Running, "Virtue Stop ends state updates");
    }

    private static void VirtueDeath()
    {
        foreach (var death in new[] { 0, 1, 2 })
        {
            using var f = new CastrumFixture(virtue: true); f.Frame(0);
            f.Change(20, a => a with { Cast = f.Spell(14234, 1) }); f.Frame(1);
            if (death == 2) f.Actors.RemoveAll(a => a.Id == 1);
            else f.Change(1, a => a with { Dead = death == 0, Hp = death == 1 ? 0u : a.Hp });
            f.Frame(2);
            Check(!f.Native.Active && f.Native.Casts.Count == 0 && f.Native.VirtueTimeline == null, "Virtue full DeathPhase cleanup");
        }
    }

    private static void VirtueCounterOverflow()
    {
        using var f = new CastrumFixture(virtue: true); f.Frame(0); VirtueReach(f, 0xF0000);
        var at = (f.Native.Now - START).TotalSeconds;
        f.Change(20, a => a with { Cast = f.Spell(14227, at) }); f.Frame(at);
        f.Change(20, a => a with { Cast = null }); f.Frame(at + .1);
        MechanicFeedEvent Hit(uint action) => new MechanicEventActionEffect(20, 1, action, 0, 0, default, 0, 0, 0, []);
        f.Frame(at + .2, packets: [Hit(14218), Hit(14217), Hit(14231)],
            native: () =>
            {
                f.Region.Effect(20, 14218); f.Region.Effect(20, 14217); f.Region.Effect(20, 14231);
            });
        Check(f.Native.VirtueCount("BrightDarkAuroraCounter") == 3 && f.Native.VirtueTimeline!.StateId == 0xF0020,
            "Virtue ==2 cannot accept3");
        f.Frame(at + 9.1);
        Check(f.Native.VirtueTimeline!.StateId == 0xF0030, "Virtue exact expected plus1 overdue");
        f.Effect(at + 9.2, 14230);
        Check(f.Native.VirtueTimeline.StateId == 0xF0040, "Virtue exact4 advances");
        f.Source!.StateMachine.ActiveState!.Exit?.Invoke();
        f.Native.VirtueTimeline.Stop();
        Check(!f.Native.ActiveRules.Any(r => r.Component == "BrightDarkAuroraCounter"),
            "Virtue Stop invokes current state Exit deactivation");
    }

    private static void VirtueRawCompare(CastrumFixture f)
    {
        foreach (var component in f.Source!.Components)
        {
            var rule = f.Native.ActiveRules.Single(r => r.Component == component.GetType().Name);
            if (rule.Kind is BozjaRuleKind.Tower or BozjaRuleKind.VirtueExplosion)
            {
                var sourceType = rule.Kind == BozjaRuleKind.Tower ? component.GetType().BaseType! : component.GetType();
                var expected = ((IEnumerable)sourceType.GetField("tetherByActor", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(component)!).Cast<ITuple>().ToArray();
                var dict = (IDictionary)typeof(BozjaEncounter).GetField("virtueTethers", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(f.Native)!;
                var actual = dict.Contains(rule) ? ((IEnumerable)dict[rule]!).Cast<ITuple>().ToArray() : [];
                Check(expected.Length == actual.Length, "Virtue raw ordered tether tuple count " + rule.Component);
                for (var i = 0; i < actual.Length; ++i)
                {
                    var e = (Actor)expected[i][0]!; var a = (BozjaActor)actual[i][0]!;
                    Check(e.InstanceID == a.Actor.Id && e.IsDestroyed == a.Destroyed && e.Position.ToVec3() == a.Actor.Position with { Y = 0 },
                        "Virtue raw original source tether " + i);
                    if (rule.Kind == BozjaRuleKind.VirtueExplosion)
                        Check((ulong)expected[i][1]! == (ulong)actual[i][2]!, "Virtue raw all ID tether target " + i);
                    else
                    {
                        var et = (Actor?)expected[i][1]; var at = (BozjaActor?)actual[i][1];
                        Check(et?.InstanceID == at?.Actor.Id && et?.IsDestroyed == at?.Destroyed,
                            "Virtue raw original nullable target tether " + i);
                    }
                }
            }
            if (component is BaitAwayCast wind)
            {
                var native = f.Native.Casts.Where(c => c.Rule == rule).ToArray();
                Check(wind.CurrentBaits.Count == native.Length, "Virtue raw wind count");
                for (var i = 0; i < native.Length; ++i)
                {
                    var e = wind.CurrentBaits[i]; var a = native[i];
                    Check(e.Source.InstanceID == a.Source.Actor.Id && e.Source.IsDead == a.Source.Actor.Dead
                        && e.Target.InstanceID == a.Target!.Actor.Id && e.Target.Position.ToVec3() == a.Target.Actor.Position with { Y = 0 }
                        && e.Target.IsDestroyed == a.Target.Destroyed && e.Target.IsDead == a.Target.Actor.Dead
                        && e.Activation == a.Activation, "Virtue raw wind original source target clock " + i);
                }
            }
            if (component is GenericTowersOpenWorld towers)
            {
                var actual = f.Native.Towers.Where(t => t.Rule == rule).ToArray();
                Check(towers.Towers.Count == actual.Length, "Virtue raw tower count");
                for (var i = 0; i < actual.Length; ++i)
                {
                    var e = towers.Towers[i]; var a = actual[i];
                    Check(e.Position.ToVec3() == a.Origin && e.Activation == a.Activation
                        && e.AllowedSoakers!.Select(x => x == null ? (0UL, false) : (x.InstanceID, x.IsDestroyed)).ToHashSet()
                            .SetEquals(f.Native.Soakers(a).Select(x => x == null ? (0UL, false) : (x.Actor.Id, x.Destroyed))),
                        "Virtue raw tower position clock exact original Allowed references");
                }
            }
            if (rule.Kind == BozjaRuleKind.VirtueCounter)
                Check((int)component.GetType().GetField("NumCasts")!.GetValue(component)! == f.Native.VirtueCount(rule.Component),
                    "Virtue exact Effect counter " + rule.Component);
        }
    }
}
