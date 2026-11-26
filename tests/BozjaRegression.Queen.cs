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
    private static void RunQueen(Action<string, Action> run)
    {
        run("Bozja native DRN6 client exact default polygon three Map arena states", QueenClient);
        run("Bozja native DRN6 standard duplicate ordinary grouped Finish no Effect TTL", QueenStandard);
        run("Bozja native DRN6 AboveBoard twenty one seconds and TurretsTour no untargetable clear", QueenBoardTurrets);
        run("Bozja native DRN6 real Feed fourteen components 60slots chess means Doom KB arena casts damage", QueenFeed);
        run("Bozja native DRN6 Chess nearest even rows excludes exactly two pending and appended risky tiles", QueenChessEdges);
        run("Bozja native DRN6 real cold per Actor short clock order Pet 60slots Doom duplicate Lose Destroy", QueenColdOrder);
        run("Bozja native DRN6 cold existing knockback immunity true slot59 no projected movement", QueenColdImmunity);
        run("Bozja native DRN6 exact diagonal raw rectangle boundary versus source Check", QueenRectEdges);
    }

    private static void QueenClient()
    {
        using var f = new Fixture(24);
        Check(f.M.IsQueen && f.M.Definition.PrimaryOid == 0x310B && f.M.Definition.Rules.Length == 14, "DRN6 catalog");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.DelubrumReginae.DRN6Queen.AID>())
            Check(data!.GetExcelSheet<Lumina.Excel.Sheets.Action>()!.TryGetRow((uint)action, out _)
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "DRN6 client action " + action);
        var source = f.Activate<GenericAOEs>("ArenaChange");
        void Compare()
        {
            Check(f.Source.Arena.Center.ToVec3() == f.M.Center && f.Source.Arena.Bounds.Radius == f.M.SampleRadius, "DRN6 arena center/radius");
            for (var x = -30f; x <= 30; x += .413f)
                for (var z = -30f; z <= 30; z += .537f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), "DRN6 exact arena");
                }
        }
        Compare();
        f.Start(source, 22984);
        CompareMetalFoxBits(f, source, "ArenaChange", "DRN6 shrink preview");
        foreach (var state in new uint[] { 0x00020001, 0x00200010, 0x00400001 })
        {
            source.OnMapEffect(0x19, state); f.M.MapEffect(0x19, state); Compare();
            f.Set(2, a => a with { Cast = null }); f.Sync();
            f.Start(source, 22984);
            CompareMetalFoxBits(f, source, "ArenaChange", "DRN6 no shrink after Map");
        }
    }

    private static void QueenStandard()
    {
        foreach (var rule in BozjaCatalog.Definitions[24].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
        {
            using var f = new Fixture(24);
            var source = f.Activate<GenericAOEs>(rule.Component);
            var spell = f.Start(source, rule.Actions[0]);
            source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            CompareMetalFoxBits(f, source, rule.Component, "DRN6 duplicate " + rule.Component);
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(rule.Actions[0])); f.M.Effect(1, rule.Actions[0]);
            f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareMetalFoxBits(f, source, rule.Component, "DRN6 no Effect TTL");
            var finish = rule.GroupedFinish ? new ActorCastInfo { Action = new(ActionType.Spell, 6499) } : spell;
            source.OnCastFinished(f.W.Actors.Find(2)!, finish);
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { ActionId = finish.Action.ID });
            CompareMetalFoxBits(f, source, rule.Component, "DRN6 first ID Finish");
        }
    }

    private static void QueenBoardTurrets()
    {
        using var f = new Fixture(24);
        var board = f.Activate<GenericAOEs>("AboveBoard");
        f.Add(20, 0x310E, f.Center + new Vector3(1.234f, 0, 3.456f)); f.Sync();
        CompareMetalFoxBits(f, board, "AboveBoard", "DRN6 bolt 21");
        Check(f.M.Visible().Single().Cast.Activation == START.AddSeconds(21), "DRN6 exact 21 Created clock");
        f.Add(21, 0x310F, f.Center - Vector3.UnitX * 5); f.Sync();
        f.Start(board, 23360);
        CompareMetalFoxBits(f, board, "AboveBoard", "DRN6 replacement 15.1f");
        f.M.Actors[1].Actor = f.M.Actors[1].Actor with { Targetable = false };
        f.M.Effect(2, 22995); CompareMetalFoxBits(f, board, "AboveBoard", "DRN6 short and false retain");
        board.OnEventCast(f.Viewer, PatriotEvent(22994)); f.M.Effect(2, 22994);
        CompareMetalFoxBits(f, board, "AboveBoard", "DRN6 long clears");
        var turrets = f.Activate<GenericAOEs>("TurretsTour");
        f.Add(30, 0x3113, f.Center); f.Add(31, 0x3113, f.Center + Vector3.UnitZ * 13); f.Sync();
        f.Set(2, a => a with { Cast = null }); f.Sync(); f.Start(turrets, 23008);
        CompareMetalFoxBits(f, turrets, "TurretsTour", "DRN6 same family half threshold");
        f.Remove(30); f.Add(30, 0x233C, f.Center + Vector3.UnitX * 20); f.Sync();
        foreach (var id in new ulong[] { 30, 2, 31 })
        {
            turrets.OnEventCast(f.W.Actors.Find(id)!, PatriotEvent(23009)); f.M.Effect(id, 23009);
            CompareMetalFoxBits(f, turrets, "TurretsTour", "DRN6 reused ID consumes raw");
        }
    }

    private static void QueenFeed()
    {
        Initialize();
        using var session = new MechanicSession("DRN6-source-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(936);
        var model = region.Encounters.Single(m => m.IsQueen);
        var center = model.Center;
        ReplayCast Spell(uint action, Vector3 point, float rot = .43f, ulong target = 100)
            => new(action, target, point, rot, START, START.AddSeconds(6.3f), false, false) { IsSpell = true, TotalTime = 6 };
        ReplayStatus Status(uint id, ushort param = 0, int slot = 59) => new(id, param, 0, START.AddSeconds(100)) { SlotIndex = slot };
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x310B, Kind = 2, SubKind = 5, Position = center, Targetable = true, InCombat = true, Hp = 1000,
                Cast = Spell(22984, center) },
            new() { Id = 20, Oid = 0x310C, Kind = 2, Position = center + Vector3.UnitX * 10, Targetable = true, Hp = 1000,
                Statuses = [Status(2056, 0xE2)], Cast = Spell(22988, center) },
            new() { Id = 21, Oid = 0x310D, Kind = 2, Position = center - Vector3.UnitX * 10, Targetable = true, Hp = 1000,
                Statuses = [Status(2056, 0xE3)] },
            new() { Id = 30, Oid = 0x310E, Kind = 2, Position = center + Vector3.UnitZ * 8, Hp = 1000 },
            new() { Id = 31, Oid = 0x310F, Kind = 2, Position = center - Vector3.UnitZ * 8, Hp = 1000 },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000, ClassJob = 24, Statuses = [Status(2474)] },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 20, Hp = 1000, Statuses = [Status(910)] },
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
            var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x310B);
            var viewer = session.World.Actors.Find(100)!; var native = model.Actors[100];
            Check(source.Components.Count == 14 && model.Active, "DRN6 real fourteen components");
            Check(source.Arena.Center.ToVec3() == model.Center && source.Arena.Bounds.Radius == model.SampleRadius, "DRN6 Feed arena");
            var expected = source.Components.OfType<GenericAOEs>().SelectMany(c => c.ActiveAOEs(0, viewer).ToArray()).ToArray();
            var actual = model.Visible(native).ToArray();
            Check(expected.Select(e => (e.Origin.ToVec3(), e.Rotation.Rad, e.Activation, e.Risky, e.Color))
                .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky,
                    a.Cast.Rule.Safe ? Colors.SafeFromAOE : a.Danger ? Colors.Danger : 0))), "DRN6 Feed fields/order at " + at);
            for (var i = 0; i < actual.Length; ++i)
                for (var x = -29f; x < 29; x += 2.31f)
                    for (var z = -29f; z < 29; z += 2.67f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        Check(expected[i].Check(new(p.X, p.Z)) == actual[i].Cast.Contains(p), "DRN6 Feed raw shape");
                    }
            var ai = new AIHints();
            foreach (var c in source.Components) c.AddAIHints(0, viewer, default, ai);
            ai.Normalize();
            var zones = model.NavigationForHost(native);
            Check(ai.ForbiddenZones.Select(z => z.Item2).SequenceEqual(zones.Select(z => z.Activation)), "DRN6 Feed normalized clocks at " + at);
            if (at == 1)
            {
                var chess = source.Components.OfType<GenericAOEs>().Single(c => c.GetType().Name == "Chess");
                var chessAi = new AIHints(); chess.AddAIHints(0, viewer, default, chessAi);
                var chessAoes = chess.ActiveAOEs(0, viewer).ToArray();
                Check(chessAoes.Length > 0 && chessAoes.All(a => a.Risky), "DRN6 actual YourMove owns risky real tiles");
                Check(chessAi.ForbiddenZones.Count == 1
                    && MechanicHintsConvert.ConvertForbiddenZone(chessAi.ForbiddenZones[0].Item1, 0, center, model.SampleRadius, out _).Count == 0,
                    "DRN6 source actual chess intersection sample fill exceeds 65 percent");
                Check(model.Navigation(native).Single(z => z.Component == "Chess").Convert(center, model.SampleRadius).Count() == 0,
                    "DRN6 native conversion preserves actual source rejection");
            }
            for (var i = 0; i < zones.Length; ++i)
            {
                var converted = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, model.Center, model.SampleRadius, out var kind);
                if (kind == MechanicNavZoneKind.ComplementSkipped
                    && MechanicHintsConvert.TryConvertComplement(ai.ForbiddenZones[i].Item1, 0, out var safe))
                {
                    converted = safe;
                    Check(zones[i].Goal, "DRN6 direct complement stays goal");
                }
                var nativeShapes = zones[i].Convert(model.Center, model.SampleRadius).ToArray();
                Check(converted.Count == nativeShapes.Length, "DRN6 Feed conversion count at " + at + "/" + zones[i].Component);
                for (var x = -29f; x < 29; x += 2.31f)
                    for (var z = -29f; z < 29; z += 2.67f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        Check(converted.Any(s => s.Template.Resolve(s.Position, s.Rotation, null).Contains(p, 0))
                            == nativeShapes.Any(s => s.Contains(p, 0)), "DRN6 Feed nav shape " + zones[i].Component);
                    }
            }
            Check(source.CalculateGlobalHints().SequenceEqual(model.GlobalHints())
                && source.CalculateHintsForRaidMember(0, viewer).Select(h => (h.Item1, h.Item2))
                    .SequenceEqual(model.Hints(native).Select(h => (h.Text, h.Risky))), "DRN6 Feed hints at " + at
                    + " source=" + string.Join("/", source.CalculateHintsForRaidMember(0, viewer)) + " MP=" + string.Join("/", model.Hints(native)));
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(model.DamageForHost(native).Select(d => (d.Mask, d.Activation, d.Type))), "DRN6 Feed damage");
            var kb = source.Components.OfType<GenericKnockback>().Single();
            Check(kb.CalculateMovements(0, viewer).Select(m => (m.from.ToVec3(), m.to.ToVec3()))
                .SequenceEqual(model.QueenMovements(native).Select(m => (m.From, m.To))), "DRN6 source sequential KB");
        }
        Frame(0);
        actors[0] = actors[0] with { Cast = Spell(22969, center) };
        actors[5] = actors[5] with { Statuses = [Status(2480)] };
        Frame(1, [new MechanicEventMapEffect(0x19, 0x00200010)]);
        actors[1] = actors[1] with { Statuses = [], Cast = Spell(22970, center) };
        Frame(2);
        actors[1] = actors[1] with { Cast = Spell(22983, center - Vector3.UnitX * 15, 0), Position = center - Vector3.UnitX * 15 };
        actors[5] = actors[5] with { Statuses = [] };
        Frame(3);
        actors[0] = actors[0] with { Cast = Spell(22985, center) };
        actors[2] = actors[2] with { Cast = Spell(23360, center) };
        actors[6] = actors[6] with { Statuses = [Status(910, slot: 0), Status(910)] };
        Frame(4, [new MechanicEventMapEffect(0x19, 0x00400001)]);
        actors[0] = actors[0] with { Cast = Spell(22981, center, target: 101) };
        actors[1] = actors[1] with { Cast = Spell(23426, center) };
        actors[5] = actors[5] with { Statuses = [Status(160)] };
        actors[6] = actors[6] with { Statuses = [Status(910)] };
        Frame(5);
        actors[1] = actors[1] with { Cast = Spell(22989, center) };
        actors[2] = actors[2] with { Cast = Spell(22980, center) };
        actors[6] = actors[6] with { Statuses = [] };
        Frame(6, [new MechanicEventActionEffect(20, 1, 23430, 0, 0, default, 0, 0, 0, [])]);
        actors[0] = actors[0] with { Cast = null };
        actors[1] = actors[1] with { Cast = null };
        actors[2] = actors[2] with { Cast = null };
        Frame(100);
    }

    private static void QueenChessEdges()
    {
        using var f = new Fixture(24);
        var chess = f.Activate<GenericAOEs>("Chess");
        var means = f.Activate<GenericAOEs>("MeansEnds");
        void Gain(ulong id, uint sid, ushort param)
        {
            var status = new ActorStatus(sid, param, START.AddSeconds(100), 0);
            chess.OnStatusGain(f.W.Actors.Find(id)!, ref status); means.OnStatusGain(f.W.Actors.Find(id)!, ref status);
            f.Set(id, a => a with { Statuses = [.. a.Statuses, new ReplayStatus(sid, param, 0, START.AddSeconds(100))] });
            f.Sync();
        }
        void Compare(string step)
        {
            var expected = chess.ActiveAOEs(0, f.Viewer).ToArray();
            var actual = f.M.Visible(f.NativeViewer).Where(a => a.Cast.Rule.Kind == BozjaRuleKind.QueenChess).ToArray();
            Check(expected.Select(a => (a.Origin.ToVec3(), a.Activation, a.Risky))
                .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Activation, a.Risky))), step + " tiles");
            Check(actual.Select(a => a.Cast.Generation).Distinct().Count() == actual.Length, step + " each published tile needs a distinct draw ID");
            var ai = new AIHints(); chess.AddAIHints(0, f.Viewer, default, ai);
            CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).Where(z => z.Component == "Chess").ToArray(), step);
            CompareMetalFoxBits(f, means, "MeansEnds", step + " crosses");
        }
        Gain(100, 2474, 0);
        Compare("DRN6 no excludes eight pending tiles");
        Check(chess.ActiveAOEs(0, f.Viewer).Length == 8, "DRN6 source eight tiles at center");
        Gain(100, 2480, 0);
        Compare("DRN6 no excludes eight actual tiles preserve source sampler rejection");
        var eight = new AIHints(); chess.AddAIHints(0, f.Viewer, default, eight);
        Check(MechanicHintsConvert.ConvertForbiddenZone(eight.ForbiddenZones.Single().Item1, 0, f.Center, f.M.SampleRadius, out _).Count == 0,
            "DRN6 source eight tiles real navigation sampling rejects fill above 65 percent");
        var lose = new ActorStatus(2480, 0, START.AddSeconds(100), 0);
        chess.OnStatusLose(f.Viewer, ref lose);
        f.Set(100, a => a with { Statuses = a.Statuses.Where(s => s.Id != 2480).ToArray() }); f.Sync();
        Gain(100, 2475, 0);
        foreach (var x in new[] { -25.001f, -25f, -15f, -5f, 5f, 15f, 25f, 25.001f })
        {
            f.Move(100, f.Center + new Vector3(x, 0, 0)); Compare("DRN6 bankers rounding " + x);
        }
        Gain(2, 2056, 0xE2); Compare("DRN6 only one exclude ignored");
        Gain(1, 2056, 0xE3); Compare("DRN6 exactly two exclude rows columns");
        Gain(101, 2056, 0xE4); Compare("DRN6 third exclude ignored");
        Gain(100, 2481, 0); Compare("DRN6 risky locked tiles promote shared crosses");
        f.Move(100, f.Center); Gain(100, 2480, 0); Compare("DRN6 duplicate Gain appends old origin tiles");
    }

    private static void QueenColdOrder()
    {
        foreach (var first in new[] { true, false })
            foreach (var pet in new[] { true, false })
            {
                Initialize();
                using var session = new MechanicSession("DRN6-cold-order");
                var feed = new MechanicWorldFeed(session);
                var region = new BozjaRegion(936);
                var model = region.Encounters.Single(m => m.IsQueen);
                var center = model.Center;
                ReplayStatus Status(uint sid, int slot, ushort param = 0, double expires = 100)
                    => new(sid, param, 0, START.AddSeconds(expires)) { SlotIndex = slot };
                var boss = new ReplayActorState
                {
                    Id = 1, Oid = 0x310B, Kind = 2, SubKind = 5, Hp = 1000, Position = center, Targetable = true, InCombat = true,
                    Cast = new(22969, 100, center, 0, START, START.AddSeconds(5.3f), false, false) { TotalTime = 5, IsSpell = true },
                };
                var mover = new ReplayActorState
                {
                    Id = 20, Oid = 0x310C, Kind = 2, SubKind = pet ? (byte)2 : (byte)5, Hp = 1000,
                    Position = center + new Vector3(3.234f, 0, -3.789f), Rotation = .73f,
                    Statuses = [Status(2056, 59, 0xE2), Status(910, 0), Status(910, 58)],
                };
                var viewer = new ReplayActorState { Id = 100, Kind = 1, Hp = 1000, Position = center, ClassJob = 24 };
                var actors = first ? new List<ReplayActorState> { boss, mover, viewer } : [mover, boss, viewer];
                ReplayPartyMember[] party = [new(0, "", 100)];
                var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
                void Frame(double at)
                {
                    var now = START.AddSeconds(at); session.BeginFrame(now);
                    feed.ApplyFrame(new(936, 760, 100, 0, [new(0, 100, 100, "")], [],
                        actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
                    feed.Flush(); session.Update();
                    region.Synchronize(actors.ToArray(), party, 100, now, true);
                    var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x310B);
                    var sourceViewer = session.World.Actors.Find(100)!;
                    var crosses = source.Components.OfType<GenericAOEs>().Single(c => c.GetType().Name == "MeansEnds").ActiveAOEs(0, sourceViewer).ToArray();
                    var nativeCrosses = model.Visible(model.Actors[100]).Where(a => a.Cast.Rule.Kind == BozjaRuleKind.QueenMeansEnds).ToArray();
                    Check(crosses.Select(a => (a.Origin.ToVec3(), a.Rotation.Rad, a.Activation, a.Risky))
                        .SequenceEqual(nativeCrosses.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky))), "DRN6 cold per Actor/Pet at " + at);
                    Check(source.CalculateHintsForRaidMember(0, sourceViewer).Select(h => (h.Item1, h.Item2))
                        .SequenceEqual(model.Hints(model.Actors[100]).Select(h => (h.Text, h.Risky))), "DRN6 cold/hot Doom duplicate slots at " + at);
                    if (at == 0)
                        Check(crosses.Length == 1 && crosses[0].Activation == START.AddSeconds(first ? 11.7d : 22d)
                            && crosses[0].Risky == first, "DRN6 cold real short ordering");
                }
                Frame(0);
                var moverIndex = actors.FindIndex(a => a.Id == 20);
                actors[moverIndex] = actors[moverIndex] with { Statuses = [Status(2056, 59, 0xE3), Status(910, 58)] };
                Frame(1);
                actors[moverIndex] = actors[moverIndex] with { Statuses = [Status(2056, 59, 0xE4)] };
                Frame(2);
                actors[moverIndex] = actors[moverIndex] with { Statuses = [Status(910, 59)] };
                Frame(3);
                actors.RemoveAt(moverIndex);
                Frame(4);
            }
    }

    private static void QueenColdImmunity()
    {
        Initialize();
        using var session = new MechanicSession("DRN6-cold-immunity");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(936);
        var model = region.Encounters.Single(m => m.IsQueen);
        var center = model.Center;
        ReplayActorState[] actors =
        [
            new() { Id = 1, Oid = 0x310B, Kind = 2, SubKind = 5, Hp = 1000, Position = center, Targetable = true, InCombat = true },
            new() { Id = 20, Oid = 0x233C, Kind = 2, Hp = 1000, Position = center - Vector3.UnitX * 15,
                Cast = new(22983, 100, center, 0, START, START.AddSeconds(6.3f), false, false) { TotalTime = 6, IsSpell = true } },
            new() { Id = 100, Kind = 1, Hp = 1000, Position = center + Vector3.UnitX * 15,
                Statuses = [new(160, 0, 100, START.AddSeconds(100)) { SlotIndex = 59 }] },
        ];
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        session.BeginFrame(START);
        feed.ApplyFrame(new(936, 760, 100, 0, [new(0, 100, 100, "")], [],
            actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, START])!).ToArray()));
        feed.Flush(); session.Update();
        region.Synchronize(actors, [new(0, "", 100)], 100, START, true);
        var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x310B);
        var kb = source.Components.OfType<GenericKnockback>().Single();
        var expected = kb.CalculateMovements(0, session.World.Actors.Find(100)!);
        Check(expected.Count == 0 && !model.QueenMovements(model.Actors[100]).Any(), "DRN6 cold immune excludes all projected movements");
        Check(!model.Hints(model.Actors[100]).Any(h => h.Text == "About to be knocked into danger!"), "DRN6 cold immune has no false unsafe destination hint");
        Check(model.Navigation(model.Actors[100]).Any(z => z.Component == "HeavensWrathKnockback" && z.Goal),
            "DRN6 source AI override triangle intentionally ignores immunity");
    }

    private static void QueenRectEdges()
    {
        using var f = new Fixture(24);
        foreach (var rule in f.M.Definition.Rules.Where(r => r.Shape is MasterPlayer.Spatial.SafeZone.RayRectangleTemplate))
        {
            var rect = (MasterPlayer.Spatial.SafeZone.RayRectangleTemplate)rule.Shape!;
            var source = new AOEShapeRect(rect.Length, rect.MaxLocalX);
            for (var i = 0; i < 1500; ++i)
            {
                var rotation = i * .004321f - 3;
                var (sin, cos) = Math.SinCos(rotation);
                var native = new BozjaCast(1, rule, f.NativeViewer, f.Center, rotation, START);
                foreach (var forward in new[] { 0f, .001f, rect.Length * .5f, rect.Length })
                    foreach (var side in new[] { -rect.MaxLocalX, MathF.BitDecrement(rect.MaxLocalX), rect.MaxLocalX, MathF.BitIncrement(rect.MaxLocalX) })
                    {
                        var point = f.Center + new Vector3(forward * (float)sin + side * (float)cos, 0,
                            forward * (float)cos - side * (float)sin);
                        Check(source.Check(new(point.X, point.Z), new(f.Center.X, f.Center.Z), new(rotation)) == native.Contains(point),
                            "DRN6 raw rectangle exact edge " + rule.Component + "/" + rotation + "/" + forward + "/" + side);
                    }
            }
        }
    }
}
