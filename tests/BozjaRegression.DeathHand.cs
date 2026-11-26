using System.Numerics;
using System.Reflection;
using BossMod;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;

internal static partial class BozjaRegression
{
    private static void RunDeathHand(Action<string, Action> run)
    {
        run("Bozja native CE11 actual source standard groups client manifest", DeathHandStandard);
        run("Bozja native CE11 arena donut overwrite true Created cold order persistent circle", DeathHandArena);
        run("Bozja native CE11 Wind lock raw clock overwrite actual effects hint line navigation", DeathHandWind);
        run("Bozja native CE11 Wind real Feed sixty slots three immunity histories old Party Pet", DeathHandStatuses);
        run("Bozja native CE11 CastHint named interrupt original casters no invented execution", DeathHandHints);
        run("Bozja native CE11 moving whirlwind reused capsule four clocks actual conversion", () => FirePheromones(4));
        run("Bozja native CE11 Helldive actual min8 hints two player conversion", () => FireStacks(4));
        run("Bozja native CE11 dynamic original caster damage clocks", () => Damage(4));
    }

    private static void DeathHandStandard()
    {
        foreach (var rule in BozjaCatalog.Definitions[4].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
        {
            using var f = new Fixture(4);
            var source = f.Activate<BossMod.Components.GenericAOEs>(rule.Component);
            var spell = f.Start(source, rule.Actions[0]);
            CompareHounds(f, source, rule.Component);
            if (rule.GroupedFinish)
            {
                f.Add(3, 0x233C, f.Center); f.Sync();
                f.Start(source, rule.Actions[1], 3);
                CompareHounds(f, source, "cross action group");
                var unrelated = new ActorCastInfo { Action = new(ActionType.Spell, 6499) };
                source.OnCastFinished(f.W.Actors.Find(2)!, unrelated);
                f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { ActionId = 6499 });
                CompareHounds(f, source, "group unrelated Finish");
            }
            f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareHounds(f, source, "no TTL");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell);
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareHounds(f, source, "watched Finish");
        }
        Initialize();
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.CriticalEngagement.CE11ShadowOfDeathHand.AID>())
            Check(new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, $"CE11 {action} client extra.");
        Check(data!.GetExcelSheet<Lumina.Excel.Sheets.DynamicEvent>()!.GetRow(5).Name.ToString().Contains("阿克巴巴"),
            "CE11 DynamicEvent actual name.");
    }

    private static void DeathHandArena()
    {
        using (var f = new Fixture(4))
        {
            var source = f.Activate<BossMod.Components.GenericAOEs>("ArenaChange");
            f.Start(source, 20182);
            void Compare(string step)
            {
                var e = source.ActiveAOEs(0, f.Viewer).ToArray();
                var a = f.M.Visible().Where(v => v.Cast.Rule.Kind == BozjaRuleKind.ArenaChange).ToArray();
                Check(e.Length == a.Length, step + " arena aoe count.");
                if (e.Length > 0)
                {
                    Check(e[0].Activation == a[0].Cast.Activation && e[0].Origin.ToVec3() == a[0].Cast.Origin,
                        step + " arena exact origin clock.");
                    for (var x = -31f; x < 31; x += .7f)
                        for (var z = -31f; z < 31; z += .9f)
                        {
                            var p = f.Center + new Vector3(x, 0, z);
                            Check(e[0].Check(new(p.X, p.Z)) == a[0].Cast.Contains(p), step + " donut.");
                        }
                }
                for (var x = -31f; x < 31; x += .71f)
                    for (var z = -31f; z < 31; z += .93f)
                    {
                        var p = f.Center + new Vector3(x, 0, z);
                        Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0),
                            $"{step} exact arena at {p} sourceCenter={f.Source.Arena.Center} nativeCenter={f.M.Center}.");
                    }
            }
            Compare("start");
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            source.OnCastFinished(f.W.Actors.Find(2)!, f.W.Actors.Find(2)!.CastInfo!);
            f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            Compare("Finish elapsed retain donut");
            f.Add(10, 0x1EB02E, f.Center); f.Sync();
            Compare("deathwall Created clears donut and shrinks");
            Check(f.M.Arena is CircleGeometry { Radius: 25 }, "Actual circle required.");
            f.Remove(10); Compare("deathwall destroyed does not restore");
            f.Start(source, 20182, total: 5); Compare("later Start still creates donut");
        }
        // Activation replays each actor's Created then current cast. Construct
        // both orders directly; dictionary remove/reinsert does not assure order.
        foreach (var wallFirst in new[] { false, true })
        {
            var definition = BozjaCatalog.Definitions[4];
            var center = definition.Center;
            ReplayActorState[] actors =
            [
                new() { Id = 1, Oid = definition.PrimaryOid, Kind = 2, SubKind = 5, Position = center,
                    Targetable = true, InCombat = true, Hp = 100 },
                new() { Id = 2, Oid = 0x233C, Kind = 2, SubKind = 5, Position = center,
                    Cast = new(20182, 100, center, .731f, START, START.AddSeconds(3.3f), false, false)
                    { TotalTime = 3, IsSpell = true } },
                new() { Id = 3, Oid = 0x1EB02E, Kind = 7, Position = center },
                new() { Id = 100, Kind = 1, Position = center, Hp = 100 },
            ];
            if (wallFirst) (actors[1], actors[2]) = (actors[2], actors[1]);
            var world = new WorldState(1, "ce11-cold-order");
            world.Frame.Timestamp = START;
            foreach (var actor in actors)
            {
                world.Execute(new ActorState.OpCreate(actor.Id, actor.Oid, (int)actor.Id, 0, "", 0,
                    actor.Kind == 1 ? ActorType.Player : ActorType.Helper, Class.PLD, 100,
                    new(actor.Position, 0), .5f, default, true, false, 0, 0, 0));
                if (actor.Cast != null) world.Actors.Find(actor.Id)!.CastInfo = new()
                {
                    Action = new(ActionType.Spell, 20182), Location = center, TotalTime = 3, Rotation = new(.731f),
                };
            }
            world.Execute(new PartyState.OpModify(0, new(100, 100, false, "")));
            using var module = new BossMod.Shadowbringers.Foray.CriticalEngagement.CE11ShadowOfDeathHand.CE11ShadowOfDeathHand(world, world.Actors.Find(1)!);
            var type = module.GetType().Assembly.GetType(module.GetType().Namespace + ".ArenaChange")!;
            typeof(BossModule).GetMethod("ActivateComponent")!.MakeGenericMethod(type).Invoke(module, null);
            var source = (BossMod.Components.GenericAOEs)module.Components.Single();
            var native = new BozjaEncounter(definition);
            native.Synchronize(actors, [new(0, "", 100)], 100, START);
            Check(source.ActiveAOEs(0, world.Actors.Find(100)!).Length == (wallFirst ? 1 : 0)
                && native.Visible().Count() == (wallFirst ? 1 : 0), "Cold Created/cast order must stay observable.");
            Check(native.Arena is CircleGeometry { Radius: 25 }, "Cold deathwall actual circle.");
        }
    }

    private static void DeathHandWind()
    {
        using var f = new Fixture(4);
        f.Activate<BossMod.Components.GenericAOEs>("ArenaChange");
        var source = f.Activate<BossMod.Components.GenericKnockback>("Wind");
        f.Start(source, 20183);
        void Compare(string step)
        {
            var expected = source.ActiveKnockbacks(0, f.Viewer).ToArray();
            var native = f.M.Casts.Where(c => c.Rule.Kind == BozjaRuleKind.Wind).ToArray();
            Check(expected.Length == native.Length, step + " Wind count.");
            if (expected.Length > 0)
                Check(expected[0].Origin.ToVec3() == native[0].Origin && expected[0].Direction.Rad == native[0].Rotation
                    && expected[0].Activation == native[0].Activation && expected[0].Distance == 30, step + " locked raw params.");
            var movements = source.CalculateMovements(0, f.Viewer);
            var actual = f.M.WindMovements(f.NativeViewer).ToArray();
            Check(movements.Select(m => (m.from.ToVec3(), m.to.ToVec3())).SequenceEqual(actual.Select(m => (m.From, m.To))),
                step + " exact movement projection.");
            var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
                step + " dangerous landing hints.");
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            var zones = f.M.Navigation(f.NativeViewer).Where(z => z.Component == "Wind").ToArray();
            Check(ai.ForbiddenZones.Count == zones.Length, step + " AI count.");
            for (var i = 0; i < zones.Length; ++i)
            {
                var converted = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, f.Center, f.M.SampleRadius, out _);
                var shapes = zones[i].Convert(f.Center, f.M.SampleRadius).ToArray();
                Check(converted.Count == shapes.Length && ai.ForbiddenZones[i].Item2 == zones[i].Activation,
                    step + " actual AI conversion clock.");
                for (var x = -45f; x < 45; x += .73f)
                    for (var z = -45f; z < 45; z += .87f)
                    {
                        var p = f.Center + new Vector3(x, 0, z);
                        Check(converted.Any(v => v.Template.Resolve(v.Position, v.Rotation, null).Contains(p, 0))
                            == shapes.Any(v => v.Contains(p, 0)), step + " actual AI converted cone.");
                    }
            }
            var sink = new MechanicDrawSink(new());
            using (MechanicDrawSink.Install(sink)) source.DrawArenaForeground(0, f.Viewer);
            var lines = new List<MechanicPreviewLine>();
            MechanicDrawConvert.Convert(sink.Commands, 0, [], lines, []);
            Check(lines.Count == actual.Length, step + " source line conversion.");
            Check(lines.Select(l => (l.Start, l.End, l.Safe))
                .SequenceEqual(actual.Select(m => (m.From, m.To, false))), step + " exact converted line start/end/safe.");
        }
        Compare("first");
        f.Add(9, 0x1EB02E, f.Center); f.Sync(); Compare("current quantized circle arena");
        foreach (var angle in Enumerable.Range(0, 72).Select(i => i * (2 * MathF.PI / 72)))
        {
            var spell = f.W.Actors.Find(2)!.CastInfo!;
            spell.Rotation = new(angle);
            source.OnCastStarted(f.W.Actors.Find(2)!, spell);
            f.Set(2, a => a with { Cast = a.Cast! with { Rotation = angle } }); f.Sync();
            Compare("direction " + angle);
        }
        f.Move(2, f.Center + new Vector3(8, 0, 5)); Compare("source moves lock retained");
        f.Move(100, f.Center + new Vector3(-15, 0, 0)); Compare("viewer projection follows");
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        source.OnCastFinished(f.W.Actors.Find(2)!, f.W.Actors.Find(2)!.CastInfo!);
        f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now); Compare("Finish elapsed retain");
        f.Add(3, 0x233C, f.Center); f.Sync(); f.Start(source, 20183, 3, total: 5); Compare("new source overwrites");
        f.Remove(3); Compare("destroy caster retains Wind");
        foreach (var action in new uint[] { 20184, 20185, 20186, 20187 })
        {
            f.Set(2, a => a with { Cast = null }); f.Sync();
            f.Start(source, 20183);
            source.OnEventCast(f.W.Actors.Find(2)!, new(new(ActionType.Spell, action), 100, 0, 0, default, 0, 0, default));
            f.M.Effect(2, action); Compare("actual Effect " + action);
        }
        f.Set(2, a => a with { Cast = null }); f.Sync();
        f.Start(source, 20183); f.M.Effect(100, 20184);
        Check(f.M.Casts.Any(c => c.Rule.Kind == BozjaRuleKind.Wind), "Player Effect must not consume Wind.");
    }

    private static void DeathHandStatuses()
    {
        Initialize();
        using var session = new MechanicSession("CE11-real-immunity-slots");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion();
        var center = BozjaCatalog.Definitions[4].Center;
        ReplayActorState[] actors =
        [
            new() { Id = 1, Oid = 0x2DA7, Kind = 2, SubKind = 5, Position = center, Hp = 100, Targetable = true, InCombat = true },
            new() { Id = 100, Kind = 1, Position = center, Hp = 100 },
            new() { Id = 101, Kind = 1, Position = center, Hp = 100 },
            new() { Id = 2, Oid = 0x233C, Kind = 2, SubKind = 5, Position = center, Hp = 100,
                Cast = new(20183, 100, center, .731f, START, START.AddSeconds(3.3f), false, false)
                { TotalTime = 3, IsSpell = true } },
            new() { Id = 4, Kind = 2, SubKind = 2, Position = center, Hp = 100 },
        ];
        ReplayPartyMember[] party = [new(0, "", 100), new(1, "", 101), new(2, "", 4)];
        var statuses = new Dictionary<ulong, MechanicStatusDto[]>
        {
            [100] = [new(59, 160, 0, 0, 10), new(0, 1209, 0, 0, 20)],
            [4] = [new(59, 2345, 0, 0, 10)],
        };
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Apply(string step)
        {
            session.BeginFrame(START);
            var input = actors.Select((a, i) => ((MechanicActorSnapshot)converter.Invoke(null, [a, i, START])!)
                with { Statuses = statuses.GetValueOrDefault(a.Id, []) }).ToArray();
            feed.ApplyFrame(new(920, 735, 100, 0,
                party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [], input));
            session.Update();
            var values = actors.Select(a => a with { Statuses = statuses.GetValueOrDefault(a.Id, [])
                .Select(s => new ReplayStatus(s.Id, s.Extra, s.SourceId, ReplayStatus.Expiration(START, s.RemainingSeconds))
                { SlotIndex = s.Slot, ZeroRemainingTime = s.RemainingSeconds == 0 }).ToArray() }).ToArray();
            region.Synchronize(values, party, 100, START, true);
            var source = (BossMod.Components.GenericKnockback)session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x2DA7)
                .Components.Single(c => c.GetType().Name == "Wind");
            var model = region.Encounters[4];
            foreach (var member in party)
                foreach (var clock in new[] { START.AddSeconds(3.4d), START.AddSeconds(10), START.AddSeconds(20), START.AddSeconds(21) })
                    Check(source.IsImmune(member.Slot, clock) == model.ImmuneAt(member.Slot, clock),
                        step + " immune clock " + member.Slot + "/" + clock);
        }
        Apply("cold slot59 wins category not max cold Pet");
        Check(!region.Encounters[4].ImmuneAt(0, START.AddSeconds(10)), "Strict expiry equality.");
        statuses[100] = [new(0, 1209, 0, 0, 20)]; Apply("lose59 clears Role despite remaining0");
        Check(!region.Encounters[4].ImmuneAt(0, START.AddSeconds(3.4)), "Lose is not remaining max.");
        foreach (var id in new uint[] { 3054, 160, 1209, 1722, 2663, 2345 })
        {
            statuses[100] = [new(59, id, 0, 0, 10)]; Apply("all immune IDs " + id);
            statuses[100] = []; Apply("lose " + id);
        }
        statuses[100] = [new(59, 160, 0, 0, 10)]; Apply("role refresh base");
        statuses[100] = [new(59, 160, 0, 0, 11)]; Apply("one second no Gain");
        statuses[100] = [new(59, 160, 0, 0, 12.01f)]; Apply("strict over one Gain");
        party = [new(0, "", 101), new(1, "", 100), new(2, "", 4)];
        statuses[100] = [new(59, 160, 1, 0, 12.01f)]; Apply("Extra Gain oldParty then swap");
        Apply("same input no remap");
        statuses[4] = []; Apply("hotPet Lose ignored");
        statuses[100] = [new(59, 2345, 0, 1, 10), new(0, 1722, 0, 0, 20)];
        Apply("source identity and independent categories");
    }

    private static void DeathHandHints()
    {
        foreach (var name in new[] { "BestialLoyalty", "RunWild", "BlindsideBarrage" })
        {
            using var f = new Fixture(4);
            var source = f.Activate<BossMod.Components.CastHint>(name);
            var rule = f.M.Definition.Rules.Single(r => r.Component == name);
            f.W.Actors.Find(2)!.Name = "First beastmaster";
            f.Set(2, a => a with { Name = "First beastmaster" });
            f.Start(source, rule.Actions[0]);
            void Compare(string step)
            {
                var global = new BossComponent.GlobalHints(); source.AddGlobalHints(global);
                var actual = name == "BlindsideBarrage" ? f.M.Hints(f.NativeViewer).Select(h => h.Text) : f.M.GlobalHints();
                Check(global.SequenceEqual(actual), step + " exact global hint.");
                if (name == "RunWild")
                    Check(source.Casters.Select(a => a.InstanceID).SequenceEqual(f.M.InterruptTargets.Select(a => a.Actor.Id)),
                        step + " original interrupt caster intent.");
            }
            Compare("single named caster");
            f.W.Actors.Find(2)!.Name = "Renamed";
            f.Set(2, a => a with { Name = "Renamed" }); f.Sync(); Compare("name cache does not refresh on world-only change");
            f.Add(3, 0x2DA9, f.Center); f.Sync(); f.Start(source, rule.Actions[0], 3); Compare("two caster names hidden");
            source.OnCastFinished(f.W.Actors.Find(3)!, f.W.Actors.Find(3)!.CastInfo!);
            f.M.Finish(f.M.Actors[3], f.M.Actors[3].Cast!); Compare("remaining current name refreshed");
            source.OnCastStarted(f.W.Actors.Find(2)!, f.W.Actors.Find(2)!.CastInfo!);
            f.M.Start(f.M.Actors[2]); Compare("duplicate same actor list retained");
        }
    }
}
