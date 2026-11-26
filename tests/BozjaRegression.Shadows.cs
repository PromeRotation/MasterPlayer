using System.Numerics;
using System.Reflection;
using BossMod;
using static BossMod.Components.GenericAOEs;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private static void RunShadows(Action<string, Action> run)
    {
        run("Bozja native CE63 client all source AIDs exact fields and five components", ShadowsClient);
        run("Bozja native CE63 Stormcall exactly three RefSort first two fourth hidden promotion", ShadowsStorm);
        run("Bozja native CE63 Stormcall zero direction arbitrary status original generation scalar ID", ShadowsStormGeneration);
        run("Bozja native CE63 boss first four shadows triggering NPC clock hidden five then four", () => ShadowsSequence(false));
        run("Bozja native CE63 four shadows first null fill current pose triggering boss clock", () => ShadowsSequence(true));
        run("Bozja native CE63 duplicate boss ten outputs and mixed retained shadow shapes", ShadowsDuplicates);
        run("Bozja native CE63 destroyed ShadowsCast original reference missing historical cast no fabrication", ShadowsOriginal);
        run("Bozja native CE63 wrong caster Finish clears only matching state reset Load Director", ShadowsFinish);
        run("Bozja native CE63 actual source standard Fantod R3 raw Finish", () => ShadowsStandard());
        run("Bozja native CE63 current original caster dynamic damage actual24124 not effect24125", () => Damage(11));
        run("Bozja native CE63 actual Feed cold cast before true60 status and Pet callbacks", ShadowsFeed);
        run("Bozja native CE63 actual Feed cold shadow order old references five four nine queues", ShadowsReplayFeed);
        run("Bozja native CE63 five cached shadows never initialize and wrong boss Finish clears shape", ShadowsNotFour);
        run("Bozja native CE63 reciprocal multiplication exact ULP circle boundary", ShadowsReciprocal);
        run("Bozja native CE63 boss Finish between third fourth mixed shapes and duplicate shadow records", ShadowsMixed);
    }

    private static void ShadowsClient()
    {
        Initialize();
        Check(Service.LuminaRow<Lumina.Excel.Sheets.DynamicEvent>(28)?.Name.ToString()
            == BozjaCatalog.Definitions[11].Name, "CE63 actual client DynamicEvent28.");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.CriticalEngagement.CE63WornToShadow.AID>())
            Check(new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE63 real client extra " + action);
        Check(BozjaCatalog.Definitions[11].Rules.Select(r => r.Component).SequenceEqual(
            new[] { "Stormcall", "BladedBeak", "NihilitysSong", "Fantod", "Foreshadowing" }), "CE63 source activation order.");
    }

    private static void CompareShadowRaw(Fixture f, BossMod.Components.GenericAOEs source, string component, string step)
    {
        CompareMetalFoxBits(f, source, component, step);
        var raw = (List<AOEInstance>)source.GetType().GetField("_aoes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(source)!;
        var actual = f.M.Casts.Where(c => c.Rule.Component == component).ToArray();
        Check(raw.Select(a => (a.ActorID, a.Origin.ToVec3(), a.Rotation.Rad, a.Activation))
            .SequenceEqual(actual.Select(a => (a.Source.Actor.Id, a.Origin, a.Rotation, a.Activation))),
            component + "/" + step + " raw hidden queue exact order/clock/source.");
    }

    private static void ShadowGain(Fixture f, BossMod.Components.GenericAOEs source, ulong id, ushort extra)
    {
        var status = new ActorStatus(2234, extra, f.Now.AddSeconds(40), 0);
        source.OnStatusGain(f.W.Actors.Find(id)!, ref status);
        var previous = f.Actors.Single(a => a.Id == id).Statuses;
        f.Set(id, a => a with { Statuses = [.. previous, new(2234, extra, 0, f.Now.AddSeconds(40)) { SlotIndex = previous.Length }] });
        f.Sync();
    }

    private static void ShadowsStorm()
    {
        using var f = new Fixture(11);
        var source = f.Activate<BossMod.Components.GenericAOEs>("Stormcall");
        for (ulong id = 3; id <= 5; ++id) f.Add(id, 0x3239, f.Center + new Vector3(id, 0, id * 2));
        f.Sync();
        ShadowGain(f, source, 2, 0x49);
        ShadowGain(f, source, 3, 0x1E);
        ShadowGain(f, source, 4, 0x1E);
        CompareShadowRaw(f, source, "Stormcall", "third unstable sort");
        ShadowGain(f, source, 5, 0x1E);
        CompareShadowRaw(f, source, "Stormcall", "fourth append no sort");
        var spell = f.Start(source, 24122, 3, total: 50);
        CompareShadowRaw(f, source, "Stormcall", "correct first matched position clock without resort");
        source.OnEventCast(f.W.Actors.Find(3)!, PatriotEvent(24122)); f.M.Effect(3, 24122);
        CompareShadowRaw(f, source, "Stormcall", "Effect retained");
        source.OnCastFinished(f.W.Actors.Find(3)!, spell); f.M.Finish(f.M.Actors[3], f.M.Actors[3].Cast!);
        CompareShadowRaw(f, source, "Stormcall", "Finish first ID hidden promotion");
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CompareShadowRaw(f, source, "Stormcall", "no TTL");
    }

    private static void ShadowsStormGeneration()
    {
        using var f = new Fixture(11);
        var source = f.Activate<BossMod.Components.GenericAOEs>("Stormcall");
        ShadowGain(f, source, 100, 0);
        Check(f.M.Casts.Single().Origin == f.Center, "CE63 zero direction returns center, no NaN.");
        ShadowGain(f, source, 2, 0x1E);
        ShadowGain(f, source, 2, 0x49);
        CompareShadowRaw(f, source, "Stormcall", "arbitrary player status and duplicate source slots");
        f.Set(2, a => a with { Statuses = [], Dead = true }); f.W.Actors.Find(2)!.IsDead = true; f.Sync();
        CompareShadowRaw(f, source, "Stormcall", "loss and dead retain");
        f.Remove(2);
        CompareShadowRaw(f, source, "Stormcall", "destroy without cast retains");
        f.Add(2, 0x323A, f.Center + Vector3.UnitZ * 8); f.Sync();
        var spell = f.Start(source, 24122);
        CompareShadowRaw(f, source, "Stormcall", "new same ID modifies first old entry");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareShadowRaw(f, source, "Stormcall", "new same ID Finish consumes first old entry");
    }

    private static void ShadowsSequence(bool addsFirst)
    {
        foreach (var action in new uint[] { 24130, 24131, 24132 })
        {
            using var f = new Fixture(11);
            var source = f.Activate<BossMod.Components.GenericAOEs>("Foreshadowing");
            for (ulong id = 10; id < 14; ++id) f.Add(id, 0x31D1, f.Center + new Vector3(id - 7, 0, 7));
            f.Sync();
            if (!addsFirst) f.Start(source, action, 1, total: 3);
            for (ulong id = 10; id < 14; ++id)
            {
                if (id == 13) f.Move(10, f.Center + new Vector3(13.127f, 0, -7.831f));
                f.Start(source, 24129, id, total: id == 13 ? 8 : 5);
                CompareShadowRaw(f, source, "Foreshadowing", $"add{id} beforeBoss={addsFirst}");
            }
            if (addsFirst) f.Start(source, action, 1, total: 3);
            CompareShadowRaw(f, source, "Foreshadowing", "five raw / one visible");
            Check(f.M.Casts.Count(c => c.Rule.Kind == BozjaRuleKind.Foreshadowing) == 5
                && f.M.Visible().Count() == 1 && f.M.Navigation(f.NativeViewer).Count() == 1,
                "CE63 exactly five hides copies in all consumers.");
            var expected = START.AddSeconds(new ActorCastInfo { TotalTime = addsFirst ? 3 : 8 }.NPCRemainingTime + (double)11.1f);
            Check(f.M.Casts.Where(c => c.Source.Actor.Id == 10).Single().Activation == expected,
                "CE63 initializer callback clock uses fourth add or boss, including float11.1.");
            source.OnCastFinished(f.W.Actors.Find(1)!, f.W.Actors.Find(1)!.CastInfo!);
            f.M.Finish(f.M.Actors[1], f.M.Actors[1].Cast!);
            CompareShadowRaw(f, source, "Foreshadowing", "boss Finish shows all four");
            Check(f.M.Visible().Count() == 4 && f.M.Navigation(f.NativeViewer).Count() == 4,
                "CE63 four copies immediately visible after boss Finish.");
        }
    }

    private static void ShadowsDuplicates()
    {
        using var f = new Fixture(11);
        var source = f.Activate<BossMod.Components.GenericAOEs>("Foreshadowing");
        var pulse = f.Start(source, 24130, 1);
        for (ulong id = 10; id < 14; ++id)
        {
            f.Add(id, 0x31D1, f.Center + new Vector3(id - 9, 0, 4)); f.Sync();
            f.Start(source, 24129, id);
        }
        source.OnCastStarted(f.W.Actors.Find(1)!, pulse); f.M.Start(f.M.Actors[1]);
        CompareShadowRaw(f, source, "Foreshadowing", "duplicate direct boss callback ten raw ten visible");
        Check(f.M.Visible().Count() == 10, "CE63 count10 is not hidden.");
        var storm = new ActorCastInfo { Action = new(ActionType.Spell, 24131), Location = f.Center, Rotation = new(.4f), TotalTime = 3 };
        f.W.Actors.Find(1)!.CastInfo = storm;
        f.M.Actors[1].Cast = f.M.Actors[1].Cast! with { ActionId = 24131 };
        source.OnCastStarted(f.W.Actors.Find(1)!, storm); f.M.Start(f.M.Actors[1]);
        CompareShadowRaw(f, source, "Foreshadowing", "new boss shape preserves nonnull old shadow shapes");
        Check(f.M.Casts.Last().Shape is MasterPlayer.Spatial.SafeZone.DonutTemplate,
            "CE63 nonnull snapshots are not overwritten by new boss shape.");
    }

    private static void ShadowsOriginal()
    {
        using var f = new Fixture(11);
        var source = f.Activate<BossMod.Components.GenericAOEs>("Foreshadowing");
        f.Add(10, 0x31D1, f.Center + Vector3.UnitX * 4); f.Sync();
        f.Start(source, 24129, 10);
        f.Remove(10);
        f.Add(10, 0x31D1, f.Center + Vector3.UnitX * 20); f.Sync();
        for (ulong id = 11; id < 14; ++id)
        {
            f.Add(id, 0x31D1, f.Center + Vector3.UnitZ * (id - 9)); f.Sync(); f.Start(source, 24129, id);
        }
        f.Start(source, 24132, 1);
        CompareShadowRaw(f, source, "Foreshadowing", "destroyed original reference late boss fills null");
        Check(f.M.Casts.Single(c => c.Source.Actor.Id == 10).Origin
            == new WPos(f.Center.X + 4, f.Center.Z).Quantized().ToVec3(),
            "CE63 same ID new shadow cannot replace old original cached pose.");
        using var cold = new Fixture(11);
        var coldSource = cold.Activate<BossMod.Components.GenericAOEs>("Foreshadowing");
        cold.Start(coldSource, 24135);
        Check(!cold.M.Visible().Any() && coldSource.ActiveAOEs(0, cold.Viewer).Length == 0,
            "CE63 missing24129 history cannot be reconstructed from current replay spell.");
    }

    private static void ShadowsFinish()
    {
        using var f = new Fixture(11);
        var source = f.Activate<BossMod.Components.GenericAOEs>("Foreshadowing");
        f.Start(source, 24130, 1);
        for (ulong id = 10; id < 14; ++id)
        {
            f.Add(id, 0x31D1, f.Center + Vector3.UnitX * (id - 8)); f.Sync(); f.Start(source, 24129, id);
        }
        var wrong = new ActorCastInfo { Action = new(ActionType.Spell, 24135) };
        source.OnCastFinished(f.W.Actors.Find(2)!, wrong);
        f.M.Finish(f.M.Actors[2], f.M.Actors[1].Cast! with { ActionId = 24135 });
        CompareShadowRaw(f, source, "Foreshadowing", "wrong caster shadow Finish leaves AOEs clears all records");
        source.OnCastStarted(f.W.Actors.Find(1)!, f.W.Actors.Find(1)!.CastInfo!); f.M.Start(f.M.Actors[1]);
        CompareShadowRaw(f, source, "Foreshadowing", "after cleared records duplicate boss adds only one");
        Check(f.M.Visible().Count() == 6, "CE63 wrong-caster Finish cleared records even without matching AOE.");
        f.M.Director(0x40000005);
        Check(f.M.Casts.Count == 0 && !f.M.Active, "CE63 director releases public outputs.");
        foreach (var id in f.Actors.Select(a => a.Id).ToArray()) f.Set(id, a => a with { Cast = null });
        f.M.Clear(); f.Sync();
        Check(f.M.Casts.Count == 0, "CE63 reset cold input without four history records cannot invent predictions.");
    }

    private static void ShadowsStandard()
    {
        using var f = new Fixture(11);
        var source = f.Activate<BossMod.Components.GenericAOEs>("Fantod");
        var spell = f.Start(source, 24127);
        CompareMetalFoxBits(f, source, "Fantod", "CE63 R3");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareMetalFoxBits(f, source, "Fantod", "CE63 actual watched Finish");
    }

    private static void ShadowsFeed()
    {
        Initialize();
        using var session = new MechanicSession("CE63-cold-source-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(975);
        var model = region.Encounters.Single(e => e.Definition.PrimaryOid == 0x31D0);
        var center = model.Center;
        ReplayCast Spell(uint action) => new(action, 100, center + Vector3.UnitZ * 4, .4f, START, START.AddSeconds(4.3),
            false, true) { TotalTime = 4, IsSpell = true };
        var actors = new ReplayActorState[]
        {
            new() { Id = 1, Oid = 0x31D0, Kind = 2, SubKind = 5, Position = center, Hp = 1000, Targetable = true, InCombat = true },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
            new() { Id = 20, Oid = 0x3239, Kind = 2, SubKind = 5, Position = center + Vector3.UnitX * 8, Hp = 1000,
                Cast = Spell(24122), Statuses = [new(2234, 0x1E, 0, START.AddSeconds(30)) { SlotIndex = 59 }] },
            new() { Id = 21, Oid = 0x323A, Kind = 2, SubKind = 2, Position = center + Vector3.UnitZ * 8, Hp = 1000,
                Statuses = [new(2234, 0x49, 0, START.AddSeconds(30)) { SlotIndex = 0 }] },
        };
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Frame(double seconds)
        {
            var now = START.AddSeconds(seconds);
            session.BeginFrame(now);
            var input = actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray();
            feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, "")], [], input)); feed.Flush(); session.Update();
            region.Synchronize(actors, [new(0, "", 100)], 100, now, true);
            var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31D0);
            Check(source.Components.Count == 5 && model.Active, "CE63 actual Feed five active components.");
            var expected = source.Components.OfType<BossMod.Components.GenericAOEs>().First().ActiveAOEs(0, session.World.Actors.Find(100)!).ToArray();
            Check(expected.Select(a => (a.Origin.ToVec3(), a.Activation))
                .SequenceEqual(model.Visible().Select(a => (a.Cast.Origin, a.Cast.Activation))), "CE63 cold cast/status order or hot Pet mismatch.");
        }
        Frame(0);
        Check(model.Casts.First().Origin == center + Vector3.UnitX * 30
            && model.Casts.First().Activation == START.AddSeconds(9.7f), "CE63 cold Start-before-status must leave predicted radial zone.");
        actors[1] = actors[1] with { Statuses = [new(2234, 0x1E, 0, START.AddSeconds(30)) { SlotIndex = 59 }] };
        actors[3] = actors[3] with { Statuses = [new(2234, 0x1E, 0, START.AddSeconds(30)) { SlotIndex = 0 }] };
        Frame(1);
    }

    private static void ShadowsNotFour()
    {
        using var f = new Fixture(11);
        var source = f.Activate<BossMod.Components.GenericAOEs>("Foreshadowing");
        for (ulong id = 10; id < 15; ++id)
        {
            f.Add(id, 0x31D1, f.Center + Vector3.UnitX * (id - 8)); f.Sync();
            f.Start(source, 24129, id);
        }
        var boss = f.Start(source, 24130, 1);
        CompareShadowRaw(f, source, "Foreshadowing", "five cached shadows do not initialize");
        Check(f.M.Visible().Count() == 1, "CE63 must require exactly four cached shadows.");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(24130)); f.M.Effect(1, 24130);
        CompareShadowRaw(f, source, "Foreshadowing", "boss Effect does not clear shape or queue");
        source.OnCastFinished(f.W.Actors.Find(2)!, boss);
        f.M.Finish(f.M.Actors[2], f.M.Actors[1].Cast!);
        CompareShadowRaw(f, source, "Foreshadowing", "wrong boss Finish clears shape without removing unrelated AOE");
        var field = typeof(BozjaEncounter).GetField("shadowBossShape", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var sourceField = source.GetType().GetField("_bossShape", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Check(field.GetValue(f.M) == null && sourceField.GetValue(source) == null,
            "CE63 wrong boss Finish clears cached shape even when no AOE matches.");
    }

    private static void ShadowsReciprocal()
    {
        using var f = new Fixture(11);
        var source = f.Activate<BossMod.Components.GenericAOEs>("Stormcall");
        f.Add(10, 0x3239, new(-498.1258544921875f, 0, -707.086181640625f)); f.Sync();
        ShadowGain(f, source, 10, 0x1E);
        CompareShadowRaw(f, source, "Stormcall", "reciprocal multiplication ULP");
        var point = new WPos(-466.83001708984375f, -710.577880859375f);
        var expected = source.ActiveAOEs(0, f.Viewer)[0];
        Check(!expected.Check(point) && !f.M.Casts.Single().Contains(point.ToVec3()),
            "CE63 R35 edge must preserve source reciprocal-rounding result.");
    }

    private static void ShadowsMixed()
    {
        using (var f = new Fixture(11))
        {
            var source = f.Activate<BossMod.Components.GenericAOEs>("Foreshadowing");
            var spell = f.Start(source, 24130, 1);
            for (ulong id = 10; id < 14; ++id) f.Add(id, 0x31D1, f.Center + Vector3.UnitX * (id - 8));
            f.Sync();
            for (ulong id = 10; id < 13; ++id) f.Start(source, 24129, id);
            source.OnCastFinished(f.W.Actors.Find(1)!, spell); f.M.Finish(f.M.Actors[1], f.M.Actors[1].Cast!);
            f.Set(1, a => a with { Cast = null }); f.W.Actors.Find(1)!.CastInfo = null; f.Sync();
            f.Start(source, 24129, 13);
            f.Start(source, 24131, 1);
            CompareShadowRaw(f, source, "Foreshadowing", "three retained donut one null filled cone");
            Check(f.M.Casts.Count(c => c.Shape is MasterPlayer.Spatial.SafeZone.DonutTemplate) == 3
                && f.M.Casts.Count(c => c.Shape is MasterPlayer.Spatial.SafeZone.FanTemplate) == 2,
                "CE63 only the fourth null shape is replaced.");
        }
        using (var f = new Fixture(11))
        {
            var source = f.Activate<BossMod.Components.GenericAOEs>("Foreshadowing");
            f.Start(source, 24130, 1);
            var spell = f.Start(source, 24129, 2);
            for (var i = 1; i < 4; ++i)
            {
                source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            }
            CompareShadowRaw(f, source, "Foreshadowing", "four repeated same original shadow IDs");
            Check(f.M.Casts.Count == 5, "CE63 duplicate shadow records must not be deduplicated.");
            source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            CompareShadowRaw(f, source, "Foreshadowing", "fifth repeated shadow does not initialize again");
            source.OnCastFinished(f.W.Actors.Find(2)!, new() { Action = new(ActionType.Spell, 24133) });
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { ActionId = 24133 });
            CompareShadowRaw(f, source, "Foreshadowing", "real shadow Finish removes one duplicate reveals four");
            Check(f.M.Visible().Count() == 4, "CE63 only first duplicate ID is consumed.");
        }
    }

    private static void ShadowsReplayFeed()
    {
        Initialize();
        foreach (var addsFirst in new[] { false, true })
        {
            using var session = new MechanicSession("CE63-shadow-source-feed");
            var feed = new MechanicWorldFeed(session);
            var region = new BozjaRegion(975);
            var model = region.Encounters.Single(e => e.Definition.PrimaryOid == 0x31D0);
            var center = model.Center;
            ReplayCast Spell(uint action, double at, Vector3 position) => new(action, 100, position, .4f,
                START.AddSeconds(at), START.AddSeconds(at + 4.3), false, true) { TotalTime = 4, IsSpell = true };
            var boss = new ReplayActorState { Id = 1, Oid = 0x31D0, Kind = 2, SubKind = 5, Position = center,
                Hp = 1000, Targetable = true, InCombat = true, Cast = Spell(24130, -2, center) with
                { TotalTime = 7, FinishAt = START.AddSeconds(5.3) } };
            var shadows = Enumerable.Range(10, 4).Select(i => new ReplayActorState
            {
                Id = (ulong)i, Oid = 0x31D1, Kind = 2, SubKind = 5, Position = center + Vector3.UnitX * (i - 7),
                Hp = 1000, Cast = Spell(24129, -.5, center + Vector3.UnitZ * i) with
                { TotalTime = i - 8, FinishAt = START.AddSeconds(i - 8 + -.5 + .3) },
            }).ToList();
            var viewer = new ReplayActorState { Id = 100, Kind = 1, Position = center, Hp = 1000 };
            var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
            void Frame(double seconds, int rawCount)
            {
                var actors = addsFirst ? shadows.Concat([boss, viewer]).ToArray() : new[] { boss, viewer }.Concat(shadows).ToArray();
                var now = START.AddSeconds(seconds);
                session.BeginFrame(now);
                var input = actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray();
                feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, "")], [], input)); feed.Flush(); session.Update();
                region.Synchronize(actors, [new(0, "", 100)], 100, now, true);
                var module = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31D0);
                var source = module.Components.OfType<BossMod.Components.GenericAOEs>().Single(c => c.GetType().Name == "Foreshadowing");
                var expected = (List<AOEInstance>)source.GetType().GetField("_aoes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(source)!;
                Check(expected.Count == rawCount && model.Casts.Count == rawCount,
                    $"CE63 Feed {seconds} raw count coldAddsFirst={addsFirst} expected={expected.Count} actual={model.Casts.Count}.");
                Check(expected.Select(a => (a.ActorID, a.Origin.ToVec3(), a.Rotation.Rad, a.Activation))
                    .SequenceEqual(model.Casts.Select(a => (a.Source.Actor.Id, a.Origin, a.Rotation, a.Activation))),
                    $"CE63 Feed {seconds} raw queue identity pose exact clock coldAddsFirst={addsFirst}.");
                var visible = source.ActiveAOEs(0, session.World.Actors.Find(100)!).ToArray();
                Check(visible.Length == model.Visible().Count() && visible.Length == model.Navigation(model.Actors[100]).Count(),
                    "CE63 Feed hidden publication agrees in display and navigation.");
            }
            Frame(0, 5);
            boss = boss with { Cast = null }; Frame(1, 4);
            boss = boss with { Cast = Spell(24131, 2, center) }; Frame(2, 9);
            shadows.RemoveAt(0); Frame(3, 9);
            shadows.Add(new() { Id = 10, Oid = 0x31D1, Kind = 2, SubKind = 5,
                Position = center + Vector3.UnitZ * 20, Hp = 1000 });
            Frame(4, 9);
            boss = boss with { Cast = null }; Frame(5, 8);
            boss = boss with { Cast = Spell(24132, 6, center) }; Frame(6, 13);
            var expectedOld = new WPos(center.X + 3, center.Z).Quantized().ToVec3();
            Check(model.Casts.Where(c => c.Source.Actor.Id == 10).All(c => c.Origin == expectedOld),
                "CE63 Feed new same ID cannot rebind old shadow pose.");
            shadows[^1] = shadows[^1] with { Cast = Spell(24133, 7, center) }; Frame(7, 13);
            shadows[^1] = shadows[^1] with { Cast = null }; Frame(8, 12);
            boss = boss with { Cast = null }; Frame(9, 11);
            boss = boss with { Cast = Spell(24130, 10, center) }; Frame(10, 12);
        }
    }
}
