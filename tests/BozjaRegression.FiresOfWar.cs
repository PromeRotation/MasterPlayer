using System.Numerics;
using System.Reflection;
using BossMod;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static partial class BozjaRegression
{
    private static void RunFiresOfWar(Action<string, Action> run)
    {
        run("Bozja native CE23 client DynamicEvent9 source action extras HotTemper24 ordinary Finish", WarStandard);
        run("Bozja native CE23 R4 tower complete open world inputs geometry navigation damage", WarTower);
        run("Bozja native CE23 Enrage dynamic first caster raw Finish no damage", WarEnrage);
        run("Bozja native CE23 Countdown stage position callback clock Update cache", WarCountdown);
        run("Bozja native CE23 nine TooHot locked Mater strict furthest position and partial draw", WarTooHot);
        run("Bozja native CE23 missing Mater source output error native withdrawal and recovery", WarMissingMater);
        run("Bozja native CE23 NPC slot59 status original bomb Effect and weak goal", WarBomb);
        run("Bozja native CE23 participant source index error first output frame and retry", WarParticipants);
        run("Bozja native CE23 permanent current target Cleave quantized text outline native conversion", WarCleave);
        run("Bozja native CE23 duplicate furthest source partial draw default tail explicit diagnosis", WarDuplicateHot);
        run("Bozja native CE23 actual Feed eight components phase order cached original Mater trash HP0", WarFeedLifecycle);
        run("Bozja native CE23 actual Feed NPC true60 slots cold Pet correction loss old generation Effect", WarFeedStatuses);
        run("Bozja native CE23 actual Feed Countdown destruction replacement equal count version stale publication", WarFeedCountdown);
        run("Bozja native CE23 pre pull real draw initializes enemy order across dictionary slot reuse", WarPrePullEnemies);
        run("Bozja native CE23 CatchingFire dynamic NPC masks duplicate original reference Finish", () => Damage(9));
        run("Bozja native CE23 Countdown duplicate origins unmatched counted Finish first stage source independent", WarCountdownQueue);
    }

    private static void WarStandard()
    {
        Initialize();
        var name = data!.GetExcelSheet<Lumina.Excel.Sheets.DynamicEvent>()!.GetRow(9).Name.ToString();
        Check(name == BozjaCatalog.Definitions[9].Name, "CE23 actual Chinese client name.");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.CriticalEngagement.CE23FiresOfWar.AID>())
            Check(new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE23 client extra " + action);
        using var f = new Fixture(9);
        var source = f.Activate<BossMod.Components.GenericAOEs>("HotTemper");
        var spell = f.Start(source, 20668);
        CompareMetalFoxBits(f, source, "HotTemper", "CE23 real R24");
        Check(f.M.Visible().Single().Cast.Shape is CircleTemplate { Radius: 24 }, "CE23 ignore stale R30 comment.");
        f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CompareMetalFoxBits(f, source, "HotTemper", "CE23 no TTL");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Check(!f.M.Visible().Any(), "CE23 watched Finish.");
    }

    private static void WarTower()
    {
        using var f = new Fixture(9);
        var source = f.Activate<BossMod.Components.CastTowersOpenWorld>("Pyroplexy");
        var spell = f.Start(source, 20659);
        f.Move(100, f.M.Towers[0].Origin + Vector3.UnitX * 4);
        f.Move(101, f.Center + Vector3.UnitZ * 15);
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
            "CE23 exact4 tower hints.");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).ToArray(), "CE23 real R4 tower conversion");
        Check(((AOEShapeCircle)source.Towers[0].Shape).Radius == BozjaEncounter.TowerRadius(f.M.Towers[0]), "CE23 complete tower radius.");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Check(f.M.Towers.Length == 0, "CE23 tower Finish.");
    }

    private static void WarEnrage()
    {
        using var f = new Fixture(9);
        var source = f.Activate<BossMod.Components.CastHint>("SlowDeflagration");
        var spell = f.Start(source, 20669, total: 75);
        void Compare()
        {
            var hints = new BossComponent.GlobalHints(); source.AddGlobalHints(hints);
            Check(hints.SequenceEqual(f.M.GlobalHints()), "CE23 dynamic Enrage text.");
            Check(!f.M.Damage(f.NativeViewer).Any(), "CE23 Enrage no damage prediction.");
        }
        Compare();
        f.Now = START.AddSeconds(76); f.W.Frame.Timestamp = f.Now;
        f.W.Actors.Find(2)!.CastInfo!.ElapsedTime = 75;
        f.M.Advance(f.Now); Compare();
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Compare();
    }

    private static void WarCountdown()
    {
        using var f = new Fixture(9);
        var source = f.Activate<BossMod.Components.ConcentricAOEs>("ExplosiveCountdown");
        f.Start(source, 20665); source.Update();
        CompareMetalFoxBits(f, source, "ExplosiveCountdown", "CE23 initial outer donut");
        for (var order = 0; order < 3; ++order)
        {
            f.Now = START.AddSeconds(10 + order); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            var action = new uint[] { 20665, 20664, 20663 }[order];
            var sourceSpell = new ActorCastInfo { Action = new(ActionType.Spell, action),
                Location = f.W.Actors.Find(2)!.Position.ToVec3() + Vector3.UnitX, Rotation = new(2) };
            source.OnCastFinished(f.W.Actors.Find(2)!, sourceSpell);
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { ActionId = action, Location = sourceSpell.Location });
            CompareMetalFoxBits(f, source, "ExplosiveCountdown", "CE23 pre Update keeps publication " + order);
            f.Sync(); source.Update();
            CompareMetalFoxBits(f, source, "ExplosiveCountdown", "CE23 post Update stage " + order);
            Check(source.NumCasts == f.M.CountdownCasts, "CE23 Concentric NumCasts.");
        }
        Check(f.M.Countdowns.Count == 0 && f.M.PublishedCountdowns.Count == 0, "CE23 third stage removes.");
    }

    private static BossMod.Components.GenericAOEs AddNineHot(Fixture f)
    {
        var source = f.Activate<BossMod.Components.GenericAOEs>("TooHotToHandle");
        for (ulong id = 2; id <= 10; ++id)
        {
            if (id != 2) f.Add(id, 0x233C, f.Center + Vector3.UnitX * (id - 2));
            f.Start(source, 20667, id);
        }
        return source;
    }

    private static void WarTooHot()
    {
        using var f = new Fixture(9);
        f.Add(30, 0x2E74, f.Center); f.Sync();
        var source = AddNineHot(f);
        CompareMetalFoxBits(f, source, "TooHotToHandle", "CE23 nine filter");
        var original = f.M.FireHotViews().Select(c => c.Generation).ToArray();
        f.Move(30, f.Center + Vector3.UnitX * 100);
        Check(original.SequenceEqual(f.M.FireHotViews().Select(c => c.Generation)), "CE23 Mater position freezes.");
        CompareMetalFoxBits(f, source, "TooHotToHandle", "CE23 Mater moved retains first position");
        var nativeLast = f.M.Casts.Last(c => c.Rule.Kind == BozjaRuleKind.TooHot);
        var spell = f.W.Actors.Find(nativeLast.Source.Actor.Id)!.CastInfo!;
        source.OnCastFinished(f.W.Actors.Find(nativeLast.Source.Actor.Id)!, spell);
        f.M.Finish(nativeLast.Source, nativeLast.Source.Cast!);
        CompareMetalFoxBits(f, source, "TooHotToHandle", "CE23 eight shows all");
    }

    private static void WarMissingMater()
    {
        using var f = new Fixture(9);
        var source = AddNineHot(f);
        var threw = false;
        try { _ = source.ActiveAOEs(0, f.Viewer).Length; }
        catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "CE23 real source missing Mater fails output.");
        Check(!f.M.PrepareFireNavigation(f.NativeViewer) && f.M.FireNavigationError != null
            && !f.M.FireHotViews().Any() && f.M.Casts.Count(c => c.Rule.Kind == BozjaRuleKind.TooHot) == 9,
            "CE23 missing Mater explicit failure retains all history.");
        f.Add(50, 0x233C, f.Center); f.Sync();
        var raidwide = f.Activate<BossMod.Components.CastHint>("CatchingFire");
        f.Start(raidwide, 20666, 50);
        Check(f.Source.CalculateGlobalHints().Contains("Raidwide") && f.M.GlobalHints().Contains("Raidwide"),
            "CE23 missing Mater personal failure preserves another component global Raidwide.");
        f.Add(30, 0x2E74, f.Center); f.Sync();
        Check(f.M.PrepareFireNavigation(f.NativeViewer) && f.M.FireNavigationError == null, "CE23 Mater arrival retries next frame.");
        CompareMetalFoxBits(f, source, "TooHotToHandle", "CE23 Mater recovery");
    }

    private static void GiveWarBomb(Fixture f, BossMod.Components.GenericStackSpread source, ulong id, ushort param = 0)
    {
        var status = new ActorStatus(2056, param, START.AddSeconds(30), 0);
        source.OnStatusGain(f.W.Actors.Find(id)!, ref status);
        f.Set(id, a => a with { Statuses = [new(2056, param, 0, START.AddSeconds(30)) { SlotIndex = 59 }] });
        f.Sync();
    }

    private static void WarBomb()
    {
        using var f = new Fixture(9);
        f.Add(30, 0x2E76, f.Center + Vector3.UnitX * 2); f.Sync();
        var source = f.Activate<BossMod.Components.GenericStackSpread>("CantTouchThis");
        GiveWarBomb(f, source, 30);
        Check(source.Stacks.Count == 1 && f.M.Casts.Count(c => c.Rule.Kind == BozjaRuleKind.BombStack) == 1,
            "CE23 NPC nonParty slot59 gain.");
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
            "CE23 raw bomb Touch text.");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        CompareWarZones(f, ai, f.M.Navigation(f.NativeViewer).ToArray(), "CE23 bomb base stack consumed goal");
        Check(ai.PredictedDamage.Count == 0 && !f.M.Damage(f.NativeViewer).Any(), "CE23 no invented Shared prediction.");
        GiveWarBomb(f, source, 30, 1);
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(20661)); f.M.Effect(2, 20661);
        Check(source.Stacks.Count == 2 && f.M.Casts.Count(c => c.Rule.Kind == BozjaRuleKind.BombStack) == 2,
            "CE23 unrelated source cannot consume bomb.");
        source.OnEventCast(f.W.Actors.Find(30)!, PatriotEvent(20662)); f.M.Effect(30, 20662);
        Check(source.Stacks.Count == 1 && f.M.Casts.Count(c => c.Rule.Kind == BozjaRuleKind.BombStack) == 1,
            "CE23 exact original source Effect removes first.");
    }

    private static void WarParticipants()
    {
        using var f = new Fixture(9);
        f.Move(101, f.Center + Vector3.UnitX * 50);
        f.Add(30, 0x2E76, f.Center + Vector3.UnitX * 2);
        f.Add(31, 0x2E76, f.Center + Vector3.UnitX * 4); f.Sync();
        var source = f.Activate<BossMod.Components.GenericStackSpread>("CantTouchThis");
        GiveWarBomb(f, source, 30); GiveWarBomb(f, source, 31);
        var first = new AIHints(); source.AddAIHints(0, f.Viewer, default, first);
        Check(f.M.PrepareFireNavigation(f.NativeViewer), "CE23 first participant population uses old count0.");
        var threw = false;
        try { source.AddAIHints(0, f.Viewer, default, new()); }
        catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw && !f.M.PrepareFireNavigation(f.NativeViewer) && f.M.FireNavigationError?.Contains("stack=1/2") == true,
            "CE23 exact source index exception recorded with context.");
        Check(!f.M.Navigation(f.NativeViewer).Any(), "CE23 failing frame withdraws all navigation.");
        f.Viewer.HPMP = new(30001, 40000, 0, 0, 0);
        f.Set(100, a => a with { Hp = 30001 }); f.Sync();
        var recovered = new AIHints(); source.AddAIHints(0, f.Viewer, default, recovered);
        Check(recovered.GoalZones.Count == 1 && f.M.PrepareFireNavigation(f.NativeViewer), "CE23 highHP recovery leaves weak goal unexecuted.");
    }

    private static void WarCleave()
    {
        using var f = new Fixture(9);
        f.Set(1, a => a with { TargetId = 101 }); f.W.Actors.Find(1)!.TargetID = 101; f.Sync();
        var source = f.Activate<BossMod.Components.Cleave>("ScaldingScolding");
        for (var i = 0; i < 8; ++i)
        {
            f.Move(100, f.Center + new Vector3(-i, 0, i * .31f));
            var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
                "CE23 independently quantized cleave text.");
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).ToArray(), "CE23 constant cleave real conversion");
        }
        Check(!f.M.GlobalHints().Any() && !f.M.Damage(f.NativeViewer).Any(), "CE23 no inherited bait Tankbuster output.");
    }

    private static void CompareWarZones(Fixture f, AIHints hints, BozjaZone[] zones, string step)
    {
        Check(hints.ForbiddenZones.Count == zones.Length, step + " exact raw count.");
        for (var i = 0; i < zones.Length; ++i)
        {
            var (distance, clock, _) = hints.ForbiddenZones[i];
            var goal = MasterPlayer.MechanicHost.MechanicHintsConvert.TryConvertComplement(distance, 0, out var complement);
            var expected = goal ? complement
                : MasterPlayer.MechanicHost.MechanicHintsConvert.ConvertForbiddenZone(distance, 0, f.Center, f.M.SampleRadius, out _);
            var actual = zones[i].Convert(f.Center, f.M.SampleRadius).ToArray();
            Check(zones[i].Activation == clock && zones[i].Goal == goal && expected.Count == actual.Length,
                step + " actual complement/hazard conversion clock count.");
            for (var x = -21f; x < 21; x += 1.37f)
                for (var z = -21f; z < 21; z += 1.53f)
                {
                    var point = f.Center + new Vector3(x, 0, z);
                    Check(expected.Any(p => p.Template.Resolve(p.Position, p.Rotation, null).Contains(point, 0))
                        == actual.Any(p => p.Contains(point, 0)), step + " actual converted geometry.");
                }
        }
    }

    private static void WarDuplicateHot()
    {
        using var f = new Fixture(9);
        f.Add(30, 0x2E74, f.Center); f.Sync();
        var source = f.Activate<BossMod.Components.GenericAOEs>("TooHotToHandle");
        for (ulong id = 2; id <= 10; ++id)
        {
            var point = f.Center + Vector3.UnitX * (id < 4 ? 10 : id - 4);
            if (id == 2) f.Move(id, point);
            else f.Add(id, 0x233C, point);
            f.Start(source, 20667, id);
        }
        var raw = source.ActiveAOEs(0, f.Viewer).ToArray();
        Check(raw.Length == 8 && raw.Count(a => a.Shape == null) == 1, "CE23 source returns one default tail.");
        var valid = raw.Where(a => a.Shape != null).ToArray();
        var native = f.M.FireHotViews().ToArray();
        Check(valid.Length == 7 && native.Length == 7
            && valid.Select(a => a.Origin.ToVec3()).SequenceEqual(native.Select(a => a.Origin)),
            "CE23 preserve all seven valid source circles.");
        var capture = MechanicModuleDrawCapture.Capture(f.Source, 0, default);
        Check(capture.Errors.Any(e => e.Component == source && e.Stage == "background")
            && capture.Commands.OfType<MechanicDrawZoneCircle>().Count(c => c.Radius == 10 && !c.Outline) == 7,
            "CE23 true source draw keeps seven circles before null tail failure.");
        Check(f.M.FireDrawingError?.Contains("7个有效圆") == true, "CE23 explicit partial draw diagnosis.");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        CompareWarZones(f, ai, f.M.Navigation(f.NativeViewer).ToArray(), "CE23 partial valid circles keep navigation");
    }

    private sealed class WarFeed : IDisposable
    {
        internal readonly MechanicSession Session = new("CE23-source-feed");
        internal readonly BozjaRegion Region = new();
        internal readonly List<ReplayActorState> Actors = [];
        internal readonly Vector3 Center = BozjaCatalog.Definitions[9].Center;
        private readonly MechanicWorldFeed feed;
        private static readonly MethodInfo converter = typeof(ReplayFeedAdapter)
            .GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        internal BozjaEncounter Model => Region.Encounters[9];
        internal BossModule? Source => Session.BossModules.LoadedModules.SingleOrDefault(m => m.PrimaryActor.OID == 0x2E75);

        internal WarFeed()
        {
            Initialize();
            feed = new(Session);
            Actors.Add(Npc(1, 0x2E75));
            Actors.Add(new() { Id = 100, Kind = 1, Position = Center, Hp = 30001, MaxHp = 40000, ClassJob = 19 });
            Actors.Add(new() { Id = 101, Kind = 1, Position = Center + Vector3.UnitZ * 10, Hp = 100, ClassJob = 19 });
        }

        internal ReplayActorState Npc(ulong id, uint oid, ReplayCast? cast = null)
            => new() { Id = id, Oid = oid, Kind = 2, SubKind = 5, Position = Center,
                Hp = 1000, MaxHp = 1000, Targetable = true, InCombat = id == 1, Cast = cast, TargetId = 101 };

        internal ReplayCast Spell(uint action, Vector3 origin)
            => new(action, 100, origin, .4f, START, START.AddSeconds(4.3f), false, true)
                { TotalTime = 4, IsSpell = true };

        internal void Set(ulong id, Func<ReplayActorState, ReplayActorState> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }

        internal void Frame(double elapsed, params MechanicEventActionEffect[] events)
        {
            var now = START.AddSeconds(elapsed);
            Session.BeginFrame(now);
            var input = Actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray();
            feed.ApplyFrame(new(920, 735, 100, 0, [new(0, 100, 100, ""), new(1, 101, 101, "")], [], input));
            foreach (var e in events) feed.Enqueue(e);
            feed.Flush(); Session.Update();
            Region.Synchronize(Actors.ToArray(), [new(0, "", 100), new(1, "", 101)], 100, now, true, () =>
            {
                foreach (var e in events) Region.Effect(e.CasterId, e.ActionId, e.MainTargetId, e.TargetPos);
            });
            Check((Source?.StateMachine.ActiveState != null) == Model.Active, "CE23 actual Feed phase active equality.");
        }

        public void Dispose() => Session.Dispose();
    }

    private static void WarFeedLifecycle()
    {
        using (var f = new WarFeed())
        {
            f.Actors.Add(f.Npc(20, 0x2E72));
            f.Actors.Add(f.Npc(30, 0x2E74) with { Dead = true });
            f.Frame(0);
            Check(f.Model.Active && f.Source!.Components.Count == 8, "CE23 first dead Mater is cached after phase check.");
            var nextFrame = START.AddSeconds(.01);
            f.Session.BeginFrame(nextFrame);
            f.Session.Update();
            f.Region.Update(nextFrame);
            Check(!f.Model.Active && f.Model.Completed && f.Source == null,
                "CE23 cached dead Mater ends next Framework without reapplying any actor inputs.");
        }
        using (var f = new WarFeed())
        {
            f.Actors.Add(f.Npc(20, 0x2E72));
            f.Actors.Add(f.Npc(30, 0x2E74));
            f.Frame(0);
            f.Set(1, a => a with { Dead = true, Hp = 0 });
            f.Set(20, a => a with { Hp = 0 });
            f.Frame(1);
            Check(f.Model.Active, "CE23 dead Primary plus HP0 live Trash does not end.");
            f.Actors.RemoveAll(a => a.Id == 30); f.Frame(2);
            Check(f.Model.Active, "CE23 destroyed nondead cached Mater does not end.");
            f.Actors.Add(f.Npc(30, 0x2E74) with { Dead = true });
            f.Frame(3);
            Check(f.Model.Active, "CE23 new sameID dead Mater cannot replace cached original.");
            f.Set(20, a => a with { Dead = true }); f.Frame(4);
            Check(!f.Model.Active && f.Model.Completed, "CE23 all exact Trash OIDs dead ends.");
        }
        using (var f = new WarFeed())
        {
            f.Actors.Add(f.Npc(20, 0x2E72));
            f.Actors.Add(f.Npc(30, 0x2E74));
            f.Frame(0);
            f.Actors.RemoveAll(a => a.Id == 1); f.Frame(1);
            f.Actors.Add(f.Npc(1, 0x2E75)); f.Frame(2);
            Check(!f.Model.Primary!.Destroyed && f.Source!.PrimaryActor == f.Session.World.Actors.Find(1),
                "CE23 sameID Primary rebinds existing active module.");
            f.Set(30, a => a with { Dead = true }); f.Frame(3);
            f.Frame(4);
            Check(!f.Model.Active && f.Model.Primary == null && f.Source == null,
                "CE23 rebound Primary is not pending and cannot spuriously reactivate after Mater death.");
        }
    }

    private static void WarFeedStatuses()
    {
        using var f = new WarFeed();
        var status = new ReplayStatus(2056, 0xB9, 0, START.AddSeconds(30));
        f.Actors.Add(f.Npc(20, 0x2E76) with { Statuses = [status with { SlotIndex = 0 }, status with { SlotIndex = 59 }] });
        f.Actors.Add(f.Npc(21, 0x2E76) with { SubKind = 2, Statuses = [status with { SlotIndex = 59 }] });
        f.Frame(0);
        void Compare(string step)
        {
            var source = f.Source!.Components.OfType<BossMod.Components.GenericStackSpread>().Single();
            var native = f.Model.Casts.Where(c => c.Rule.Kind == BozjaRuleKind.BombStack).ToArray();
            Check(source.Stacks.Select(s => (s.Target.InstanceID, s.Activation))
                .SequenceEqual(native.Select(s => (s.Target!.Actor.Id, s.Activation))), step + " exact raw stacks.");
        }
        Compare("cold NPC slots and Pet");
        Check(f.Model.Casts.Count(c => c.Rule.Kind == BozjaRuleKind.BombStack) == 3, "CE23 two real NPC slots plus cold Pet.");
        f.Set(20, a => a with { Statuses = [status with { SlotIndex = 59 }] });
        f.Set(21, a => a with { Statuses = [status with { SlotIndex = 59, Param = 7 }] });
        f.Frame(1); Compare("Lose retains history and hot Pet ignored");
        f.Set(20, a => a with { Statuses = [status with { SlotIndex = 59, Param = 1 }] });
        f.Frame(2); Compare("same status extra correction adds");
        Check(f.Model.Casts.Count(c => c.Rule.Kind == BozjaRuleKind.BombStack) == 4, "CE23 corrected NPC slot adds once.");
        f.Set(20, a => a with { Statuses = [status with { SlotIndex = 59, Param = 1, ExpiresAt = START.AddSeconds(30.5) }] });
        f.Frame(2.5); Compare("at most one second expiry correction no Gain");
        Check(f.Model.Casts.Count(c => c.Rule.Kind == BozjaRuleKind.BombStack) == 4, "CE23 small expiry correction only updates slot.");
        f.Set(20, a => a with { Statuses = [status with { SlotIndex = 59, Param = 1, ExpiresAt = START.AddSeconds(32) }] });
        f.Frame(2.6); Compare("more than one second expiry refresh Gain");
        Check(f.Model.Casts.Count(c => c.Rule.Kind == BozjaRuleKind.BombStack) == 5, "CE23 true refresh adds new callback clock.");
        f.Frame(3, new MechanicEventActionEffect(20, 1, 20661, 100, 0, f.Center, 0, 0, 0, []));
        Compare("actual source Effect removes first original");
        var original = f.Model.Casts.First(c => c.Rule.Kind == BozjaRuleKind.BombStack).Target!;
        f.Actors.RemoveAll(a => a.Id == 20); f.Frame(4); Compare("destroy original retains remaining stacks");
        f.Actors.Add(f.Npc(20, 0x2E76)); f.Frame(5, new MechanicEventActionEffect(20, 1, 20662, 100, 0, f.Center, 0, 0, 0, []));
        Compare("sameID new source cannot consume old");
        Check(original.Destroyed, "CE23 retained stack original source generation.");
    }

    private static void WarFeedCountdown()
    {
        using var f = new WarFeed();
        f.Actors.Add(f.Npc(20, 0x233C, f.Spell(20665, f.Center)));
        f.Actors.Add(f.Npc(30, 0x2E78));
        f.Frame(0);
        var original = f.Model.PublishedCountdowns.Single();
        f.Actors.RemoveAll(a => a.Id == 30);
        f.Actors.Add(f.Npc(21, 0x233C, f.Spell(20665, f.Center + Vector3.UnitX * 10)));
        f.Frame(1);
        var source = f.Source!.Components.OfType<BossMod.Components.ConcentricAOEs>().Single();
        Check(source.Sequences.Count == 1 && source.NumCasts == 0 && f.Model.Countdowns.Count == 1,
            "CE23 clear then new sequence keeps count/version.");
        Check(source.ActiveAOEs(0, f.Session.World.Actors.Find(100)!)[0].Origin.ToVec3() == original.Origin
            && f.Model.PublishedCountdowns.Single() == original
            && f.Model.Countdowns.Single().Origin != original.Origin, "CE23 source stale publication preserved exactly.");
        f.Set(21, a => a with { Cast = null }); f.Frame(2);
        Check(source.NumCasts == 1 && f.Model.CountdownCasts == 1
            && source.ActiveAOEs(0, f.Session.World.Actors.Find(100)!)[0].Origin.ToVec3() == f.Model.PublishedCountdowns.Single().Origin
            && f.Model.PublishedCountdowns.Single().Action == 1, "CE23 later Finish changes version and republishes stage1.");
    }

    private static void WarPrePullEnemies()
    {
        using var f = new Fixture(9);
        f.Set(1, a => a with { InCombat = false }); f.M.Clear(); f.Sync();
        var source = f.Activate<BossMod.Components.Cleave>("ScaldingScolding");
        foreach (var id in new ulong[] { 30, 31, 32 })
        {
            var actor = f.Add(id, 0x2E75, f.Center + Vector3.UnitX * (id - 28));
            actor.TargetID = 100; f.Set(id, a => a with { TargetId = 100 }); f.Sync();
        }
        _ = MechanicModuleDrawCapture.Capture(f.Source, 0, default);
        f.M.ObserveFireDrawing();
        f.Remove(31);
        var next = f.Add(33, 0x2E75, f.Center + Vector3.UnitX * 8);
        next.TargetID = 100; f.Set(33, a => a with { TargetId = 100 });
        f.Set(1, a => a with { InCombat = true }); f.Sync();
        Check(source.OriginsAndTargets().Select(c => c.origin.InstanceID)
            .SequenceEqual(f.M.FireCleaves().Select(c => c.Source.Actor.Id)),
            "CE23 pre pull draw preserves source Enemy cache order after slot reuse.");
        Check(f.M.FireCleaves().Select(c => c.Source.Actor.Id).SequenceEqual(new ulong[] { 30, 32, 33 }),
            "CE23 not raw dictionary insertion-slot order.");
    }

    private static void WarCountdownQueue()
    {
        using var f = new Fixture(9);
        var source = f.Activate<BossMod.Components.ConcentricAOEs>("ExplosiveCountdown");
        var spell = f.Start(source, 20665);
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]); f.Sync(); source.Update();
        Check(source.Sequences.Count == 2 && f.M.Countdowns.Count == 2, "CE23 repeated Start preserves both sequences.");
        var unrelated = new ActorCastInfo { Action = new(ActionType.Spell, 20665), Location = f.Center + Vector3.UnitZ * 50 };
        source.OnCastFinished(f.W.Actors.Find(1)!, unrelated);
        f.M.Finish(f.M.Actors[1], f.M.Actors[2].Cast! with { Location = unrelated.Location });
        f.Sync(); source.Update();
        Check(source.NumCasts == 1 && f.M.CountdownCasts == 1
            && f.M.Countdowns.All(c => c.Action == 0), "CE23 unmatched valid order counted without advancing.");
        var matching = new ActorCastInfo { Action = new(ActionType.Spell, 20665), Location = spell.Location };
        source.OnCastFinished(f.W.Actors.Find(1)!, matching);
        f.M.Finish(f.M.Actors[1], f.M.Actors[2].Cast!);
        f.Sync(); source.Update();
        Check(f.M.Countdowns.Select(c => c.Action).SequenceEqual(new uint[] { 1, 0 }), "CE23 another source advances only first matching stage.");
        CompareMetalFoxBits(f, source, "ExplosiveCountdown", "CE23 duplicate queue exact published order");
    }
}
