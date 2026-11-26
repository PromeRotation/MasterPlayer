using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private static void RunGuard(Action<string, Action> run)
    {
        run("Bozja native DRN3 client 47 actions exact clipped starting reduced arena", GuardClient);
        run("Bozja native DRN3 standard ordinary watched Finish duplicate no Effect TTL", GuardStandard);
        run("Bozja native DRN3 four global damage casts and four Enrages first raw NPC clock original casters", GuardDamageCasts);
        run("Bozja native DRN3 Arena feature lazy cache current EventState strict square tolerance Map permanent shrink", GuardArena);
        run("Bozja native DRN3 AboveBoard Created raw cache bombs original NPC clock no Finish TTL", GuardAboveBoard);
        run("Bozja native DRN3 TurretsTour exact half count narrow nearest original positions FIFO source", GuardTurrets);
        run("Bozja native DRN3 TurretsTour odd zero count narrow edge dead destroyed cache and retrigger", GuardTurretEdges);
        run("Bozja native DRN3 CoatOfArms actual prediction state Gain overwrite Lose retain and exact direction", GuardParry);
        run("Bozja native DRN3 CoatOfArms Ward active cache ID reused AI any actor zero entry", GuardParryGenerations);
        run("Bozja native DRN3 CoatOfArms real draw sink four arcs exact vertices old bridge consumption", GuardDraw);
        run("Bozja native DRN3 real 60 status slots duplicate Lose cold Pet hot filtering Destroy same ID", GuardStatusFeed);
        run("Bozja native DRN3 actual Feed ten components cold60 untargetable state Map Effect ordinary Finish", GuardFeed);
        run("Bozja native DRN3 real cold Feed Bolt Helper Warrior enumeration order and hot untargetable", GuardColdOrdering);
    }

    private static void GuardClient()
    {
        using var f = new Fixture(23);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(760)?.TerritoryType.RowId == 936
            && Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(9838)?.Singular.ToString() == f.M.Definition.Name
            && f.M.Definition.Rules.Length == 10, "DRN3 client name territory components");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.DelubrumReginae.DRN3QueensGuard.AID>())
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "DRN3 client action " + action);
        void Compare()
        {
            Check(f.Source.Arena.Center.ToVec3() == f.M.Center && f.Source.Arena.Bounds.Radius == f.M.SampleRadius,
                "DRN3 resolved center radius");
            for (var x = -31f; x <= 31; x += .37f)
                for (var z = -31f; z <= 31; z += .43f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), "DRN3 exact notched polygon");
                }
        }
        Compare();
        f.Activate<BossComponent>("ArenaChange").OnMapEffect(0x18, 0x00020001);
        f.M.MapEffect(0x18, 0x00020001);
        Compare();
    }

    private static void GuardStandard()
    {
        foreach (var rule in BozjaCatalog.Definitions[23].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
        {
            using var f = new Fixture(23);
            var source = f.Activate<GenericAOEs>(rule.Component);
            var spell = f.Start(source, rule.Actions[0]);
            source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            CompareMetalFoxBits(f, source, rule.Component, "DRN3 duplicate " + rule.Component);
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(rule.Actions[0])); f.M.Effect(1, rule.Actions[0]);
            f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareMetalFoxBits(f, source, rule.Component, "DRN3 no Effect/TTL");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, rule.Component, "DRN3 ordinary first Finish");
        }
    }

    private static void GuardArena()
    {
        using var f = new Fixture(23);
        var source = f.Activate<GenericAOEs>("ArenaChange");
        source.Update(); // force empty lazy cache before the feature is created
        f.Add(30, 0x1EA1A1, new(245.00002f, 0, -129));
        f.Sync(); source.Update();
        CompareMetalFoxBits(f, source, "ArenaChange", "DRN3 outside strict square");
        f.Move(30, new(245, 0, -128)); source.Update();
        CompareMetalFoxBits(f, source, "ArenaChange", "DRN3 inclusive corner");
        Check(f.M.Visible().Single().Cast.Activation == f.Now.AddSeconds(5), "DRN3 feature prediction");
        f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        source.Update();
        CompareMetalFoxBits(f, source, "ArenaChange", "DRN3 ring not TTL");
        source.OnMapEffect(0x18, 0x00020000); f.M.MapEffect(0x18, 0x00020000);
        Check(f.M.Visible().Count() == 1, "DRN3 unrelated Map retained");
        source.OnMapEffect(0x18, 0x00020001); f.M.MapEffect(0x18, 0x00020001);
        f.M.Update(f.Now); source.Update();
        CompareMetalFoxBits(f, source, "ArenaChange", "DRN3 shrink never retriggers");
    }

    private static void GuardDamageCasts()
    {
        foreach (var rule in BozjaCatalog.Definitions[23].Rules.Where(r =>
            r.Kind is BozjaRuleKind.Raidwide or BozjaRuleKind.Tankbuster or BozjaRuleKind.Enrage))
        {
            using var f = new Fixture(23);
            var source = f.Activate<BossComponent>(rule.Component);
            f.Add(3, 0x233C, f.Center + Vector3.UnitZ * 5); f.Sync();
            foreach (var action in rule.Actions)
            {
                var id = action == rule.Actions[0] ? 2UL : 3UL;
                if (f.W.Actors.Find(id)!.CastInfo is { } previous)
                    source.OnCastFinished(f.W.Actors.Find(id)!, previous);
                f.Start(source, action, id, target: id == 2 ? 100UL : 101UL);
                Compare();
            }
            var spell = f.W.Actors.Find(3)!.CastInfo!;
            source.OnCastStarted(f.W.Actors.Find(3)!, spell); f.M.Start(f.M.Actors[3]); Compare();
            f.Now = START.AddSeconds(10); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            foreach (var id in new ulong[] { 2, 3 })
                if (f.W.Actors.Find(id)!.CastInfo is { } raw) raw.ElapsedTime = f.M.Actors[id].Elapsed;
            Compare();
            source.OnEventCast(f.W.Actors.Find(3)!, PatriotEvent(rule.Actions[^1])); f.M.Effect(3, rule.Actions[^1]); Compare();
            source.OnCastFinished(f.W.Actors.Find(3)!, spell); f.M.Finish(f.M.Actors[3], f.M.Actors[3].Cast!); Compare();
            void Compare()
            {
                var global = new BossComponent.GlobalHints(); source.AddGlobalHints(global);
                Check(global.SequenceEqual(f.M.GlobalHints()), "DRN3 globals first raw caster " + rule.Component);
                var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
                Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                    .SequenceEqual(f.M.Damage(f.NativeViewer).Select(d => (d.Mask, d.Activation, d.Type))), "DRN3 exact damage " + rule.Component);
                Check(!f.M.Visible().Any() && !f.M.Navigation(f.NativeViewer).Any(), "DRN3 no fabricated damage geometry");
            }
        }
    }

    private static void GuardAboveBoard()
    {
        using var f = new Fixture(23);
        var source = f.Activate<GenericAOEs>("AboveBoard");
        f.Add(30, 0x30BD, f.Center + new Vector3(.123f, 0, 12.345f)); f.Sync();
        CompareMetalFoxBits(f, source, "AboveBoard", "DRN3 bolt creation");
        f.Move(30, f.Center + Vector3.UnitX * 20);
        CompareMetalFoxBits(f, source, "AboveBoard", "DRN3 original locked point");
        f.Add(31, 0x30BE, f.Center + new Vector3(5.123f, 0, 4.321f)); f.Sync();
        var spell = f.Start(source, 23187);
        CompareMetalFoxBits(f, source, "AboveBoard", "DRN3 burst replacement");
        f.Add(32, 0x30BE, f.Center + Vector3.UnitX * 8); f.Sync();
        f.Set(32, a => a with { Dead = true, Targetable = false }); f.W.Actors.Find(32)!.IsDead = true; f.Sync();
        f.Start(source, 23187); f.M.Start(f.M.Actors[2]);
        CompareMetalFoxBits(f, source, "AboveBoard", "DRN3 cached late dead untargetable burst");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CompareMetalFoxBits(f, source, "AboveBoard", "DRN3 no ordinary Finish TTL");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23403)); f.M.Effect(1, 23403);
        Check(f.M.Visible().Count() == 2, "DRN3 short effect does not clear");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23402)); f.M.Effect(1, 23402);
        CompareMetalFoxBits(f, source, "AboveBoard", "DRN3 long small clears all");
        f.Add(33, 0x30BD, f.Center); f.Sync();
        var warrior = f.Add(34, 0x30BC, f.Center); f.Sync();
        f.Set(34, a => a with { Targetable = false }); f.Sync();
        source.OnActorUntargetable(warrior);
        CompareMetalFoxBits(f, source, "AboveBoard", "DRN3 warrior untargetable clears all");
    }

    private static void GuardTurrets()
    {
        using var f = new Fixture(23);
        var source = f.Activate<GenericAOEs>("TurretsTour");
        var points = new[] { new Vector3(.123f, 0, 0), new Vector3(.123f, 0, 10.123f),
            new Vector3(.123f, 0, 20.123f), new Vector3(8, 0, 3) };
        for (var i = 0; i < points.Length; ++i) f.Add((ulong)(30 + i), 0x30C2, f.Center + points[i]);
        f.Sync();
        var spell = f.Start(source, 22541);
        CompareMetalFoxBits(f, source, "TurretsTour", "DRN3 before half turret count");
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        CompareMetalFoxBits(f, source, "TurretsTour", "DRN3 equality builds four raw turrets");
        Check(f.M.Visible().Count() == 6, "DRN3 exact half trigger count");
        f.Move(30, f.Center + Vector3.UnitX * 30);
        f.Remove(30);
        f.Add(30, 0x233C, f.Center + Vector3.UnitX * 25); f.Sync();
        CompareMetalFoxBits(f, source, "TurretsTour", "DRN3 generated old turret retained after Destroy and same ID helper");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CompareMetalFoxBits(f, source, "TurretsTour", "DRN3 locks raw origin length no Finish TTL");
        foreach (var id in new ulong[] { 2, 30, 2, 99, 31 })
        {
            if (id == 99) { f.Add(99, 0x233C, f.Center); f.Sync(); }
            source.OnEventCast(f.W.Actors.Find(id)!, PatriotEvent(22542)); f.M.Effect(id, 22542);
            CompareMetalFoxBits(f, source, "TurretsTour", "DRN3 first source effect " + id);
        }
        var gunner = f.Add(40, 0x30C1, f.Center); f.Sync();
        f.Set(40, a => a with { Targetable = false }); f.Sync();
        source.OnActorUntargetable(gunner);
        CompareMetalFoxBits(f, source, "TurretsTour", "DRN3 gunner untargetable");
    }

    private static void GuardCompareParry(Fixture f, DirectionalParry source, string step)
    {
        Check(source.ActorStates.OrderBy(k => k.Key).SequenceEqual(f.M.GuardParries.OrderBy(k => k.Key)), step + " state");
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
            step + " hints");
        var ai = new AIHints();
        foreach (var actor in f.W.Actors)
            if (actor.CharacterSpawnIndex is >= 0 and < AIHints.NumEnemies)
            {
                var enemy = new AIHints.Enemy(actor, 0, false);
                ai.Enemies[actor.CharacterSpawnIndex] = enemy;
                ai.PotentialTargets.Add(enemy);
            }
        source.AddAIHints(0, f.Viewer, default, ai);
        CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).Where(z => z.Component == "CoatOfArms").ToArray(), step);
        Check(ai.PotentialTargets.Where(t => t.Priority == -4).Select(t => t.Actor.InstanceID)
            .SequenceEqual(f.M.GuardForbiddenTargets(f.NativeViewer)), step + " raw forbidden priority intent");
    }

    private static void GuardTurretEdges()
    {
        foreach (var count in new[] { 0, 1, 2, 3, 4, 5, 6 })
        {
            using var f = new Fixture(23);
            var source = f.Activate<GenericAOEs>("TurretsTour");
            for (var i = 0; i < count; ++i)
                f.Add((ulong)(30 + i), 0x30C2, f.Center + new Vector3(i % 2 == 0 ? 0 : .1f, 0, i * 5f));
            f.Sync();
            var spell = f.Start(source, 22541);
            for (var n = 0; n < 4; ++n)
            {
                CompareMetalFoxBits(f, source, "TurretsTour", "DRN3 raw count=" + count + " repeat=" + n);
                source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            }
            if (count > 1)
            {
                f.Set(30, a => a with { Dead = true, Targetable = false });
                f.W.Actors.Find(30)!.IsDead = true; f.Sync();
                f.Remove(31);
                f.Add(31, 0x30C2, f.Center + new Vector3(.10001f, 0, 5f)); f.Sync();
                var gunner = f.Add(50, 0x30C1, f.Center); f.Sync();
                source.OnActorUntargetable(gunner); f.Set(50, a => a with { Targetable = false }); f.Sync();
                f.Start(source, 22541); f.M.Start(f.M.Actors[2]);
                CompareMetalFoxBits(f, source, "TurretsTour", "DRN3 changed enemy cache after clear");
            }
        }
    }

    private static void GuardParry()
    {
        using var f = new Fixture(23);
        var ward = f.Add(30, 0x30BB, f.Center); f.Sync();
        var source = f.Activate<DirectionalParry>("CoatOfArms");
        f.Set(100, a => a with { TargetId = 30 }); f.Viewer.TargetID = 30; f.Sync();
        f.Start(source, 22518, 30);
        GuardCompareParry(f, source, "DRN3 front back prediction");
        foreach (var rotation in new float[] { 0, .8575727343559265f, MathF.PI, -2.13f })
        {
            ward.PosRot = new(f.Center, rotation); f.Set(30, a => a with { Rotation = rotation }); f.Sync();
            foreach (var offset in new[] { Vector3.Zero, Vector3.UnitX * 10, Vector3.UnitZ * 10,
                new Vector3(7.071067f, 0, 7.071067f), new Vector3(-9.99999f, 0, -.001f) })
            {
                f.Move(100, f.Center + offset);
                GuardCompareParry(f, source, "DRN3 exact normalized direction " + rotation + "/" + offset);
            }
        }
        ActorStatus status = new(680, 12, START.AddSeconds(90), 0);
        source.OnStatusGain(ward, ref status);
        f.Set(30, a => a with { Statuses = [new(680, 12, 0, status.ExpireAt)] }); f.Sync();
        GuardCompareParry(f, source, "DRN3 Gain overwrites prediction");
        f.Start(source, 22518, 30); f.M.Start(f.M.Actors[30]);
        GuardCompareParry(f, source, "DRN3 active and opposite prediction");
        source.OnStatusLose(ward, ref status); f.Set(30, a => a with { Statuses = [] }); f.Sync();
        GuardCompareParry(f, source, "DRN3 Lose keeps predicted bits");
        source.OnEventCast(ward, PatriotEvent(22520)); f.M.Effect(30, 22520);
        source.OnCastFinished(ward, ward.CastInfo!); f.M.Finish(f.M.Actors[30], f.M.Actors[30].Cast!);
        GuardCompareParry(f, source, "DRN3 Counterplay ordinary Finish do not clear");
    }

    private static void GuardParryGenerations()
    {
        using var f = new Fixture(23);
        f.Add(30, 0x30BB, f.Center); f.Sync();
        var source = f.Activate<DirectionalParry>("CoatOfArms");
        f.Set(100, a => a with { TargetId = 30 }); f.Viewer.TargetID = 30; f.Sync();
        f.Start(source, 22519, 30); f.Move(100, f.Center + Vector3.UnitX * 10);
        GuardCompareParry(f, source, "DRN3 force active Ward cache");
        f.Remove(30);
        f.Add(30, 0x233C, f.Center); f.Sync();
        GuardCompareParry(f, source, "DRN3 same ID non Ward AI finds new actor text no Ward");
        Check(!f.M.Hints(f.NativeViewer).Any() && f.M.Navigation(f.NativeViewer).Count() == 2,
            "DRN3 current Find ID versus cached OID split");
        ActorStatus zero = new(680, 0, START.AddSeconds(99), 0);
        source.OnStatusGain(f.W.Actors.Find(30)!, ref zero);
        f.Set(30, a => a with { Statuses = [new(680, 0, 0, zero.ExpireAt)] }); f.Sync();
        GuardCompareParry(f, source, "DRN3 zero Gain keeps dictionary entry");
        source.OnStatusLose(f.W.Actors.Find(30)!, ref zero); f.Set(30, a => a with { Statuses = [] }); f.Sync();
        GuardCompareParry(f, source, "DRN3 zero Lose removes dictionary entry");
    }

    private static void GuardDraw()
    {
        using var f = new Fixture(23);
        f.Add(30, 0x30BB, f.Center + new Vector3(.123f, 0, 1.234f)); f.Sync();
        var source = f.Activate<DirectionalParry>("CoatOfArms");
        f.Start(source, 22518, 30);
        ActorStatus status = new(680, 12, START.AddSeconds(99), 0);
        source.OnStatusGain(f.W.Actors.Find(30)!, ref status);
        f.Set(30, a => a with { Statuses = [new(680, 12, 0, status.ExpireAt)] }); f.Sync();
        f.Start(source, 22518, 30); f.M.Start(f.M.Actors[30]);
        var sink = new MechanicDrawSink(new());
        using (MechanicDrawSink.Install(sink)) source.DrawArenaForeground(0, f.Viewer);
        var expected = sink.Commands.OfType<MechanicDrawPolyline>().ToArray();
        var actual = f.M.GuardArcs().ToArray();
        Check(expected.Length == 4 && actual.Length == expected.Length, "DRN3 all four source arcs");
        for (var i = 0; i < expected.Length; ++i)
        {
            Check(expected[i].Vertices.Select(v => v.ToVec3()).SequenceEqual(actual[i].Points)
                && (expected[i].Color == Colors.Enemy) == actual[i].Active, "DRN3 exact arc vertices/source colors");
            var shapes = new List<MechanicPreviewShape>(); var lines = new List<MechanicPreviewLine>(); var extras = new List<MechanicRadarExtra>();
            Check(MechanicDrawConvert.Convert([expected[i]], 0, shapes, lines, extras) == 0
                && lines.Count == actual[i].Points.Length - 1 && lines.All(l => !l.Safe),
                "DRN3 actual bridge maps active and imminent to danger line");
        }
    }

    private static void GuardFeed()
    {
        Initialize();
        using var session = new MechanicSession("DRN3-source-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(936);
        var model = region.Encounters.Single(m => m.IsGuard);
        var center = model.Center;
        ReplayCast Spell(uint action, Vector3 point, ulong target = 100) => new(action, target, point, .43f,
            START, START.AddSeconds(6.3f), false, false) { IsSpell = true, TotalTime = 6 };
        var statuses = Enumerable.Range(0, 60).Select(i => new ReplayStatus(i == 59 ? 680u : 0, 3, 0, START.AddSeconds(99))
            { SlotIndex = i }).ToArray();
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x30BA, Kind = 2, SubKind = 5, Position = center, Targetable = true, InCombat = true, Hp = 1000,
                Cast = Spell(22514, center) },
            new() { Id = 20, Oid = 0x233C, Kind = 2, Position = center + Vector3.UnitX * 10, Hp = 1000,
                Cast = Spell(23187, center) },
            new() { Id = 30, Oid = 0x30BB, Kind = 2, Position = center, Targetable = true, Hp = 1000,
                Cast = Spell(22519, center), Statuses = statuses },
            new() { Id = 31, Oid = 0x30BE, Kind = 2, Position = center + Vector3.UnitZ * 8, Hp = 1000 },
            new() { Id = 32, Oid = 0x30BD, Kind = 2, Position = center + Vector3.UnitX * 8, Hp = 1000 },
            new() { Id = 33, Oid = 0x1EA1A1, Kind = 7, Position = new(244, 0, -129), Hp = 1000 },
            new() { Id = 100, Kind = 1, Position = center + Vector3.UnitZ * 10, TargetId = 30, Hp = 1000 },
            new() { Id = 101, Kind = 1, Position = center, Hp = 1000 },
        };
        ReplayPartyMember[] party = [new(0, "", 100), new(1, "", 101)];
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Frame(double at, MechanicFeedEvent[]? events = null)
        {
            var now = START.AddSeconds(at);
            session.BeginFrame(now);
            feed.ApplyFrame(new(936, 760, 100, 0, party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            foreach (var e in events ?? []) feed.Enqueue(e);
            feed.Flush(); session.Update();
            region.Synchronize(actors.ToArray(), party, 100, now, true, () =>
            {
                foreach (var e in events ?? [])
                    switch (e)
                    {
                        case MechanicEventMapEffect map: region.MapEffect(map.Index, map.State); break;
                        case MechanicEventActionEffect effect: region.Effect(effect.CasterId, effect.ActionId, effect.MainTargetId); break;
                    }
            });
            var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x30BA);
            var viewer = session.World.Actors.Find(100)!; var native = model.Actors[100];
            Check(source.Components.Count == 10 && model.Active, "DRN3 real ten components");
            Check(source.Arena.Center.ToVec3() == model.Center && source.Arena.Bounds.Radius == model.SampleRadius, "DRN3 Feed arena");
            var expected = source.Components.OfType<GenericAOEs>().SelectMany(c => c.ActiveAOEs(0, viewer).ToArray()).ToArray();
            var actual = model.Visible(native).ToArray();
            Check(expected.Select(e => (e.Origin.ToVec3(), e.Rotation.Rad, e.Activation, e.Risky, e.Color))
                .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky, a.Danger ? Colors.Danger : 0))),
                "DRN3 Feed visible raw order geometry clock");
            for (var i = 0; i < actual.Length; ++i)
                for (var x = -29f; x < 29; x += 2.31f)
                    for (var z = -29f; z < 29; z += 2.67f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        Check(expected[i].Check(new(p.X, p.Z)) == actual[i].Cast.Contains(p), "DRN3 Feed raw shape");
                    }
            var ai = new AIHints();
            foreach (var c in source.Components) c.AddAIHints(0, viewer, default, ai);
            ai.Normalize();
            var zones = model.NavigationForHost(native);
            Check(ai.ForbiddenZones.Select(z => z.Item2).SequenceEqual(zones.Select(z => z.Activation)), "DRN3 Feed normalized clocks");
            for (var i = 0; i < zones.Length; ++i)
            {
                var converted = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, model.Center, model.SampleRadius, out _);
                var nativeShapes = zones[i].Convert(model.Center, model.SampleRadius).ToArray();
                Check(converted.Count == nativeShapes.Length, "DRN3 Feed conversion count");
                for (var x = -29f; x < 29; x += 2.31f)
                    for (var z = -29f; z < 29; z += 2.67f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        Check(converted.Any(s => s.Template.Resolve(s.Position, s.Rotation, null).Contains(p, 0))
                            == nativeShapes.Any(s => s.Contains(p, 0)), "DRN3 Feed navigation shapes");
                    }
            }
            Check(source.CalculateGlobalHints().SequenceEqual(model.GlobalHints())
                && source.CalculateHintsForRaidMember(0, viewer).Select(h => (h.Item1, h.Item2))
                    .SequenceEqual(model.Hints(native).Select(h => (h.Text, h.Risky))),
                "DRN3 Feed hint streams at " + at + " global source=" + string.Join("/", source.CalculateGlobalHints())
                + " MP=" + string.Join("/", model.GlobalHints()) + " personal source="
                + string.Join("/", source.CalculateHintsForRaidMember(0, viewer)) + " MP="
                + string.Join("/", model.Hints(native)));
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(model.DamageForHost(native).Select(d => (d.Mask, d.Activation, d.Type))), "DRN3 Feed damage");
            Check(source.Components.OfType<DirectionalParry>().Single().ActorStates.OrderBy(p => p.Key)
                .SequenceEqual(model.GuardParries.OrderBy(p => p.Key)), "DRN3 Feed true slot59");
        }
        Frame(0);
        actors[0] = actors[0] with { Cast = Spell(22515, center) };
        actors[1] = actors[1] with { Cast = Spell(22541, center + Vector3.UnitZ * 20) };
        actors.Add(new() { Id = 40, Oid = 0x30C2, Kind = 2, Hp = 1000, Position = center });
        actors.Add(new() { Id = 41, Oid = 0x30C2, Kind = 2, Hp = 1000, Position = center + Vector3.UnitZ * 15 });
        Frame(1, [new MechanicEventMapEffect(0x18, 0x00020001)]);
        actors[0] = actors[0] with { Cast = Spell(22522, center) };
        actors[1] = actors[1] with { Cast = null };
        actors[2] = actors[2] with { Statuses = [] };
        Frame(2, [new MechanicEventActionEffect(20, 1, 23402, 0, 0, default, 0, 0, 0, [])]);
        actors[0] = actors[0] with { Cast = Spell(22523, center, 101) };
        actors.Add(new() { Id = 50, Oid = 0x30C1, Kind = 2, Hp = 1000, Targetable = true, Position = center });
        Frame(3);
        actors[^1] = actors[^1] with { Targetable = false };
        actors[0] = actors[0] with { Cast = Spell(22521, center) };
        Frame(4);
        actors[0] = actors[0] with { Cast = null };
        Frame(100);
    }

    private static void GuardColdOrdering()
    {
        foreach (var order in new[] { "BHW", "HBW", "WHB", "HWB" })
        {
            Initialize();
            using var session = new MechanicSession("DRN3-cold-order-" + order);
            var feed = new MechanicWorldFeed(session);
            var model = new BozjaEncounter(BozjaCatalog.Definitions[23]);
            var center = model.Center;
            var bolt = new ReplayActorState { Id = 30, Oid = 0x30BD, Kind = 2, Position = center + Vector3.UnitX * 5, Hp = 1000 };
            var helper = new ReplayActorState { Id = 20, Oid = 0x233C, Kind = 2, Position = center, Hp = 1000,
                Cast = new(23187, 0, center, .4f, START, START.AddSeconds(6.3f), false, false) { IsSpell = true, TotalTime = 6 } };
            var warrior = new ReplayActorState { Id = 31, Oid = 0x30BC, Kind = 2, Position = center, Hp = 1000, Targetable = false };
            var actors = new List<ReplayActorState>
            {
                new() { Id = 1, Oid = 0x30BA, Kind = 2, SubKind = 5, Position = center, Targetable = true, InCombat = true, Hp = 1000 },
                new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
                new() { Id = 32, Oid = 0x30BE, Kind = 2, Position = center + Vector3.UnitZ * 8, Hp = 1000 },
            };
            actors.AddRange(order.Select(c => c == 'B' ? bolt : c == 'H' ? helper : warrior));
            ReplayPartyMember[] party = [new(0, "", 100)];
            var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
            void Frame(double at)
            {
                var now = START.AddSeconds(at);
                session.BeginFrame(now);
                feed.ApplyFrame(new(936, 760, 100, 0, [new(0, 100, 100, "")], [],
                    actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
                feed.Flush(); session.Update(); model.Synchronize(actors, party, 100, now);
                var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x30BA);
                var expected = source.Components.OfType<GenericAOEs>().SelectMany(c => c.ActiveAOEs(0, session.World.Actors.Find(100)!).ToArray()).ToArray();
                Check(expected.Select(e => (e.Origin.ToVec3(), e.Activation))
                    .SequenceEqual(model.Visible().Select(a => (a.Cast.Origin, a.Cast.Activation))), "DRN3 cold/hot order " + order + "/" + at);
            }
            Frame(0);
            var late = bolt with { Id = 40 };
            actors.Add(late);
            Frame(1);
            Check(model.Visible().Count() >= 1, "DRN3 existing false Warrior does not repeat clear");
            actors.Add(warrior with { Id = 41 }); Frame(1.5);
            Check(model.Visible().Count() >= 1, "DRN3 hot new false Warrior does not synthesize Untargetable");
            var index = actors.FindIndex(a => a.Id == 31);
            actors[index] = warrior with { Targetable = true }; Frame(2);
            actors[index] = warrior; Frame(3);
            Check(!model.Visible().Any(), "DRN3 hot true to false clears");
        }
    }

    private static void GuardStatusFeed()
    {
        foreach (var pet in new[] { false, true })
        {
            Initialize();
            using var session = new MechanicSession("DRN3-real-status-" + pet);
            var feed = new MechanicWorldFeed(session);
            var model = new BozjaEncounter(BozjaCatalog.Definitions[23]);
            var center = model.Center;
            var statuses = Enumerable.Range(0, 60).Select(i => new ReplayStatus(i is 0 or 59 ? 680u : 0,
                i == 0 ? (ushort)3 : (ushort)12, 0, START.AddSeconds(99)) { SlotIndex = i }).ToArray();
            var stateActor = new ReplayActorState { Id = 30, Oid = 0x30BB, Kind = 2, SubKind = pet ? (byte)2 : (byte)5,
                Position = center, Hp = 1000, Targetable = true, Statuses = statuses };
            var actors = new List<ReplayActorState>
            {
                new() { Id = 1, Oid = 0x30BA, Kind = 2, SubKind = 5, Position = center, Targetable = true, InCombat = true, Hp = 1000 },
                new() { Id = 100, Kind = 1, Position = center + Vector3.UnitX * 10, TargetId = 30, Hp = 1000 },
                stateActor,
            };
            ReplayPartyMember[] party = [new(0, "", 100)];
            var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
            void Frame(double at, int expected)
            {
                var now = START.AddSeconds(at);
                session.BeginFrame(now);
                feed.ApplyFrame(new(936, 760, 100, 0, [new(0, 100, 100, "")], [],
                    actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
                feed.Flush(); session.Update(); model.Synchronize(actors, party, 100, now);
                var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x30BA);
                var parry = source.Components.OfType<DirectionalParry>().Single();
                Check(parry.ActorState(30) == expected && model.GuardParries.GetValueOrDefault(30UL) == expected,
                    "DRN3 real status slots/Pet/generation " + pet + "/" + at);
                Check(parry.ActorStates.OrderBy(p => p.Key).SequenceEqual(model.GuardParries.OrderBy(p => p.Key)),
                    "DRN3 complete status dictionary");
            }
            Frame(0, 12);
            Check(session.World.Actors.Find(30)!.Statuses[0].ID == 680 && session.World.Actors.Find(30)!.Statuses[59].ID == 680
                && model.Actors[30].Statuses[0]?.Id == 680 && model.Actors[30].Statuses[59]?.Id == 680, "DRN3 true both slots");
            if (pet)
            {
                actors[2] = stateActor with { Statuses = [] }; Frame(1, 12);
                actors.RemoveAt(2); Frame(2, 12);
                actors.Add(stateActor with { Oid = 0x233C, SubKind = 5, Statuses = [], Position = center + Vector3.UnitX * 15 });
                Frame(3, 12);
            }
            else
            {
                actors[2] = stateActor with { Cast = new(22518, 0, center, .4f, START, START.AddSeconds(6.3f), false, false)
                    { IsSpell = true, TotalTime = 6 } };
                Frame(1, 0x3C);
                actors[2] = actors[2] with { Statuses = statuses.Select((s, i) => i == 0 ? s with { Id = 0 } : s).ToArray() };
                Frame(2, 0x30); // loss of either slot clears active bits despite the other unchanged slot
                actors.RemoveAt(2); Frame(3, 0x30);
                actors.Add(stateActor with { Oid = 0x233C, Statuses = [], Position = center + Vector3.UnitZ * 15, Rotation = .9f });
                Frame(4, 0x30);
            }
        }
    }
}
