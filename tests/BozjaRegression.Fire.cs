using System.Numerics;
using System.Reflection;
using BossMod;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static partial class BozjaRegression
{
    private static void RunFire(Action<string, Action> run)
    {
        run("Bozja native CE13 ordinary actual source Finish no TTL client seven actions", FireStandard);
        run("Bozja native CE13 moving pheromones raw capsule four clocks polygon obstacles generations", () => FirePheromones(3));
        run("Bozja native CE13 DeadLeaves actual Feed sixty slots old Party cold priority duplicate loss", FireStatuses);
        run("Bozja native CE13 eight player stack hints two player navigation actual source", () => FireStacks(3));
        run("Bozja native CE13 actual source dynamic buster raidwide clocks", () => Damage(3));
        run("Bozja native CE13 MaxValue raw clock actual bridge registration boundary", FireMaxClock);
        run("Bozja native CE13 actual replay adapter expired duplicate history no invented refresh", FireReplayStatuses);
        run("Bozja native CE13 pheromones lazy first query after destroyed world slot reuse", FireLazyPheromones);
    }

    private static void FireStandard()
    {
        foreach (var rule in BozjaCatalog.Definitions[3].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
        {
            using var f = new Fixture(3);
            var source = f.Activate<BossMod.Components.GenericAOEs>(rule.Component);
            var spell = f.Start(source, rule.Actions[0]);
            CompareHounds(f, source, "CE13 " + rule.Component);
            f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareHounds(f, source, "CE13 no TTL");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell);
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareHounds(f, source, "CE13 watched Finish");
        }
        Initialize();
        foreach (var action in BozjaCatalog.Definitions[3].Rules.SelectMany(r => r.Actions))
            Check(new ActionID(ActionType.Spell, action).CastTimeExtra() == 0, $"CE13 action {action} extra.");
        var de = data!.GetExcelSheet<Lumina.Excel.Sheets.DynamicEvent>()!.GetRow(1);
        Check(de.Name.ToString().Contains("皮里福尔"), "CE13 actual DynamicEvent name.");
    }

    private static void FirePheromones(int definition)
    {
        using var f = new Fixture(definition);
        var rule = f.M.Definition.Rules.Single(r => r.Kind == BozjaRuleKind.Pheromones);
        var source = f.Activate<BossMod.Components.GenericAOEs>(rule.Component);
        f.Add(10, rule.EnemyOid, f.Center + new Vector3(.123f, 0, .257f)); f.Sync();
        var edgeSource = new AOEShapeCapsule(4, 3);
        var edgeNative = new BozjaCapsuleTemplate(4, 3);
        var edge = new Vector3(-86, 0, 699.9989f);
        Check(!edgeSource.Check(new(edge.X, edge.Z), new(-90, 700), default)
            && !edgeNative.Contains(new(-90, 0, 700), 0, edge)
            && edgeNative.Distance(new(-90, 0, 700), 0, edge) == 0,
            "Capsule source squared Check must stay separate from rounded Distance.");
        void Compare(string step)
        {
            var expected = source.ActiveAOEs(0, f.Viewer).ToArray();
            var actual = f.M.Visible(f.NativeViewer).ToArray();
            Check(expected.Length == actual.Length, step + " source count.");
            for (var i = 0; i < actual.Length; ++i)
            {
                var a = actual[i].Cast; var e = expected[i];
                Check(e.Origin.X == a.Origin.X && e.Origin.Z == a.Origin.Z && e.Rotation.Rad == a.Rotation
                    && e.Activation == a.Activation, step + " source position rotation clock.");
                for (var x = -12f; x < 12; x += .31f)
                    for (var z = -12f; z < 12; z += .37f)
                    {
                        var p = f.Center + new Vector3(x, 0, z);
                        Check(e.Check(new(p.X, p.Z)) == a.Contains(p), step + " analytic capsule.");
                    }
            }
            var ai = new AIHints();
            source.AddAIHints(0, f.Viewer, default, ai);
            var nav = f.M.Navigation(f.NativeViewer).ToArray();
            var danger = nav.Where(z => !z.Obstacle).ToArray();
            var obstacles = nav.Where(z => z.Obstacle).ToArray();
            Check(ai.ForbiddenZones.Count == danger.Length && ai.TemporaryObstacles.Count == obstacles.Length,
                step + " four forecasts and separate obstacles.");
            for (var i = 0; i < obstacles.Length; ++i)
            {
                var shape = obstacles[i].Convert(f.Center, 29.5f).Single();
                for (var x = -12f; x < 12; x += .31f)
                    for (var z = -12f; z < 12; z += .37f)
                    {
                        var p = f.Center + new Vector3(x, 0, z);
                        Check(ai.TemporaryObstacles[i].Contains(new(p.X, p.Z)) == shape.Contains(p, 0),
                            step + " separate R4 obstacle exact circle.");
                    }
            }
            for (var i = 0; i < danger.Length; ++i)
            {
                var zone = danger[i];
                Check(ai.ForbiddenZones[i].Item2 == zone.Activation, step + " relative four clocks.");
                var converted = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0,
                    f.Center, 29.5f, out _).Single();
                var polygon = converted.Template.Resolve(converted.Position, converted.Rotation, null);
                var native = zone.Convert(f.Center, 29.5f).Single();
                for (var x = -14f; x < 14; x += .31f)
                    for (var z = -14f; z < 14; z += .37f)
                    {
                        var p = f.Center + new Vector3(x, 0, z);
                        Check(ai.ForbiddenZones[i].Item1.Distance(new(p.X, p.Z)) == zone.Distance(p.X, p.Z),
                            step + " raw distance.");
                        Check(polygon.Contains(p, 0) == native.Contains(p, 0), step + " actual polygon bridge.");
                    }
            }
            var sink = new MechanicDrawSink(new());
            using (MechanicDrawSink.Install(sink)) source.DrawArenaBackground(0, f.Viewer);
            var shapes = new List<MechanicPreviewShape>();
            Check(MechanicDrawConvert.Convert(sink.Commands, 0, shapes, [], []) == 0
                && shapes.Count == actual.Length, step + " actual drawing conversion.");
            for (var i = 0; i < shapes.Count; ++i)
                Check(shapes[i].Shape.GetContours().SelectMany(p => p)
                    .SequenceEqual(actual[i].Cast.Shape.Resolve(actual[i].Cast.Origin, actual[i].Cast.Rotation, null)
                        .GetContours().SelectMany(p => p)), step + " exact display polygon vertices.");
            ai.Normalize();
            var normalized = f.M.NavigationForHost(f.NativeViewer).Where(z => !z.Obstacle).ToArray();
            Check(normalized.Length == ai.ForbiddenZones.Count, step + " normalized count.");
            for (var i = 0; i < normalized.Length; ++i)
            {
                Check(normalized[i].Activation == ai.ForbiddenZones[i].Item2, step + " normalized clocks.");
                foreach (var actor in f.Actors.Where(a => a.Oid == rule.EnemyOid))
                    foreach (var offset in new[] { Vector3.Zero, new Vector3(2.3f, 0, 8.1f) })
                    {
                        var p = actor.Position + offset;
                        Check(normalized[i].Distance(p.X, p.Z) == ai.ForbiddenZones[i].Item1.Distance(new(p.X, p.Z)),
                            step + " normalized exact list order.");
                    }
            }
        }
        Compare("created");
        f.Now = START.AddSeconds(7.1d); f.W.Frame.Timestamp = f.Now;
        f.Move(10, f.Center + new Vector3(2.13f, 0, 3.27f));
        f.W.Actors.Find(10)!.PosRot = new(f.W.Actors.Find(10)!.PosRot.XYZ(), .731f);
        f.Set(10, a => a with { Rotation = .731f, Dead = true, Targetable = false, EventState = 7 });
        f.W.Actors.Find(10)!.IsDead = true; f.Sync();
        Compare("moving dead state7 relative forecast");
        f.Add(11, rule.EnemyOid, f.Center); f.Sync(); Compare("ordered two");
        f.Remove(10); Compare("real destroyed");
        f.Add(10, rule.EnemyOid, f.Center + new Vector3(-3, 0, 0)); f.Sync(); Compare("new generation appended");
    }

    private static void FireMaxClock()
    {
        using var f = new Fixture(3);
        f.Add(10, 0x2E31, f.Center); f.Sync();
        var last = f.M.Navigation(f.NativeViewer).Single(z => z.Activation == DateTime.MaxValue);
        var mapped = MechanicHintsConvert.MapActivation(last.Activation, START);
        Check(!mapped.Persistent && mapped.DelaySeconds > 1e11f, "MaxValue must remain future.");
        using var manager = new SafeZoneManager(new());
        var registry = new SafeZoneRegistry(manager, () => START);
        var overflow = false;
        try
        {
            registry.AddDelayedDanger(last.Shape!, new StaticTransform(f.Center, 0), mapped.DelaySeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            // This verifies the legacy float-delay boundary, not a production
            // fallback. Native snapshots retain the original absolute clock.
            overflow = true;
        }
        Console.WriteLine($"INFO CE13 legacy MaxValue float delay={mapped.DelaySeconds:R}; actual registry overflow={overflow}");
        var shape = last.Convert(f.Center, 29.5f).Single();
        var handle = manager.AddDangerArea(new MasterPlayer.Mechanics.Dungeons.DungeonShapeTemplate(shape),
            new StaticTransform(default, 0), [new(DateTime.MaxValue, null, default)]);
        Check(handle.IsValid, "Absolute native MaxValue snapshot must register.");
    }

    private static void FireReplayStatuses()
    {
        Initialize();
        using var session = new MechanicSession("CE13-real-replay-status-clock");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion();
        var center = BozjaCatalog.Definitions[3].Center;
        var statuses = new[] { new ReplayStatus(2301, 0, 0, START.AddSeconds(10)),
            new ReplayStatus(2301, 0, 0, START.AddSeconds(10)) };
        var now = START;
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Apply(string step)
        {
            ReplayActorState[] actors =
            [
                new() { Id = 1, Oid = 0x2E2F, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100,
                    Position = center, Targetable = true, InCombat = true },
                new() { Id = 100, Kind = 1, Hp = 100, Position = center, Statuses = statuses },
                new() { Id = 2, Oid = 0x233C, Kind = 2, SubKind = 5, Hp = 100, Position = center,
                    Cast = new(20518, 100, center, 0, START, START.AddSeconds(3.3f), false, false) { TotalTime = 3, IsSpell = true } },
            ];
            session.BeginFrame(now);
            feed.ApplyFrame(new(920, 735, 100, 0, [new(0, 100, 100, "")], [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            session.Update();
            region.Synchronize(actors, [new(0, "", 100)], 100, now, true);
            var source = (BossMod.Components.GenericAOEs)session.BossModules.LoadedModules.Single().Components
                .Single(c => c.GetType().Name == "DeadLeaves");
            var expected = source.ActiveAOEs(0, session.World.Actors.Find(100)!).Length;
            var actual = region.Encounters[3].Visible(region.Encounters[3].Actors[100]).Count();
            Check(expected == actual, step + " actual replay time mapping.");
        }
        Apply("cold two duplicates");
        statuses = [statuses[0]];
        now = START.AddSeconds(1);
        Apply("one Lose clears");
        now = START.AddSeconds(11);
        Apply("expired record clamped zero does not reflect past expiry into future");
        Check(!region.Encounters[3].Visible(region.Encounters[3].Actors[100]).Any(), "Expired snapshot invented Gain.");
    }

    private static void FireLazyPheromones()
    {
        using var f = new Fixture(3);
        var source = f.Activate<BossMod.Components.Voidzone>("Pheromones");
        f.Add(10, 0x2E31, f.Center); f.Sync();
        f.Add(11, 0x2E31, f.Center); f.Sync();
        f.Remove(10);
        f.Add(12, 0x2E31, f.Center); f.Sync();
        var expected = source.Sources(f.Source).Select(a => a.InstanceID).ToArray();
        var actual = f.M.Visible(f.NativeViewer).Select(a => a.Cast.Source.Actor.Id).ToArray();
        Check(expected.SequenceEqual(new ulong[] { 12, 11 }) && expected.SequenceEqual(actual),
            "First source cache query must reflect current world dictionary, not pull-time order.");
    }

    private static void FireStatuses()
    {
        Initialize();
        using var session = new MechanicSession("CE13-status-real-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion();
        var center = BozjaCatalog.Definitions[3].Center;
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x2E2F, Kind = 2, SubKind = 5, Hp = 100, MaxHp = 100, Targetable = true,
                InCombat = true, Position = center },
            new() { Id = 100, Kind = 1, Position = center, Hp = 100 },
            new() { Id = 101, Kind = 1, Position = center, Hp = 100 },
            new() { Id = 2, Oid = 0x233C, Kind = 2, SubKind = 5, Position = center, Hp = 100 },
            new() { Id = 3, Oid = 0x233C, Kind = 2, SubKind = 5, Position = center + new Vector3(5, 0, 0), Hp = 100 },
            new() { Id = 4, Oid = 0x233C, Kind = 2, SubKind = 2, Position = center, Hp = 100 },
        };
        ReplayCast Spell(uint action) => new(action, 100, action == 20518 ? center : center + new Vector3(5, 0, 0),
            action == 20518 ? .731f : -1.4f, START, START.AddSeconds(3.3f), false, false)
            { TotalTime = 3, IsSpell = true };
        actors[3] = actors[3] with { Cast = Spell(20518) };
        actors[4] = actors[4] with { Cast = Spell(20519) };
        ReplayPartyMember[] party = [new(0, "", 100), new(1, "", 101), new(2, "", 4)];
        var statuses = new Dictionary<ulong, MechanicStatusDto[]>
        {
            [100] = [new(59, 2301, 0, 0xE0000000, 10), new(0, 2302, 0, 0, 10)],
            [4] = [new(0, 2301, 0, 0, 10)],
        };
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Apply(string step)
        {
            session.BeginFrame(START);
            var input = actors.Select((a, i) => ((MechanicActorSnapshot)converter.Invoke(null, [a, i, START])!)
                with { Statuses = statuses.GetValueOrDefault(a.Id, []) }).ToArray();
            feed.ApplyFrame(new(920, 735, 100, 0, party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [], input));
            session.Update();
            var values = actors.Select(a => a with { Statuses = statuses.GetValueOrDefault(a.Id, [])
                .Select(s => new ReplayStatus(s.Id, s.Extra, s.SourceId, ReplayStatus.Expiration(START, s.RemainingSeconds))
                    { SlotIndex = s.Slot, ZeroRemainingTime = s.RemainingSeconds == 0 }).ToArray() }).ToArray();
            region.Synchronize(values, party, 100, START, true);
            var source = (BossMod.Components.GenericAOEs)session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x2E2F)
                .Components.Single(c => c.GetType().Name == "DeadLeaves");
            var model = region.Encounters[3];
            foreach (var member in party)
            {
                var e = source.ActiveAOEs(member.Slot, session.World.Actors.Find(member.EntityId)!).ToArray();
                var a = model.Visible(model.Actors[member.EntityId]).Where(v => v.Cast.Rule.Kind == BozjaRuleKind.DeadLeaves).ToArray();
                Check(e.Length == a.Length && e.Select(v => (v.Origin.X, v.Origin.Z, v.Rotation.Rad, v.Activation))
                    .SequenceEqual(a.Select(v => (v.Cast.Origin.X, v.Cast.Origin.Z, v.Cast.Rotation, v.Cast.Activation))),
                    step + " exact slot history " + member.Slot);
                var viewer = session.World.Actors.Find(member.EntityId)!;
                var hints = new BossComponent.TextHints();
                source.AddHints(member.Slot, viewer, hints);
                Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(model.Hints(model.Actors[member.EntityId])
                    .Select(h => (h.Text, h.Risky))), step + " current viewer color hint.");
                var ai = new AIHints();
                source.AddAIHints(member.Slot, viewer, default, ai);
                var zones = model.Navigation(model.Actors[member.EntityId]).Where(z => z.Component == "DeadLeaves").ToArray();
                Check(ai.ForbiddenZones.Count == zones.Length, step + " current viewer color navigation count.");
                for (var i = 0; i < a.Length; ++i)
                    for (var x = -31f; x < 31; x += 3.1f)
                        for (var z = -31f; z < 31; z += 2.7f)
                        {
                            var p = center + new Vector3(x, 0, z);
                            Check(e[i].Check(new(p.X, p.Z)) == a[i].Cast.Contains(p), step + " color shape.");
                        }
            }
        }
        Apply("cold both priority Pet gain slot59");
        Check(region.Encounters[3].Visible(region.Encounters[3].Actors[100]).Single().Cast.Action == 20518, "Tender priority.");
        statuses[100] = [new(0, 2301, 0, 0, 10)];
        Apply("gain0 before lose59 must clear bit");
        Check(!region.Encounters[3].Visible(region.Encounters[3].Actors[100]).Any(), "59-to-0 must end false.");
        statuses[100] = [new(59, 2301, 0, 0, 10)];
        Apply("lose0 before gain59 restores bit");
        statuses[100] = [new(59, 2301, 0, 0, 10), new(1, 2301, 0, 0, 10)];
        Apply("duplicate SID two slots");
        statuses[100] = [new(59, 2301, 0, 0, 10)];
        Apply("one duplicate loss clears");
        statuses[100] = [new(59, 2301, 0, 0, 11)];
        Apply("one second suppress refresh");
        statuses[100] = [new(59, 2301, 0, 0, 12.001f)];
        Apply("over one second refresh restores");
        party = [new(0, "", 101), new(1, "", 100), new(2, "", 4)];
        statuses[100] = [new(59, 2301, 1, 0, 12.001f)];
        Apply("Extra gain uses old Party then swap");
        Apply("second pass and unchanged snapshots no remap");
        statuses[4] = [];
        Apply("Pet hot Lose ignored");
        statuses[100] = [new(59, 2301, 2, 1, 1), new(0, 2302, 0, 0, 10)];
        Apply("source change Lose Gain shortened extra");
        actors.RemoveAll(a => a.Id == 100);
        party = [new(0, "", 101), new(2, "", 4)];
        Apply("destroy uses old Party Lose");
        actors.Add(new() { Id = 102, Kind = 1, Hp = 100, Position = center });
        party = [new(0, "", 101), new(1, "", 102), new(2, "", 4)];
        statuses[102] = [new(0, 2302, 0, 0, 10)];
        Apply("same-frame join gain uses old Party FindSlot minus1 no-op");
        Check(!region.Encounters[3].Visible(region.Encounters[3].Actors[102]).Any(), "New party gain cannot set new slot.");
        actors[actors.FindIndex(a => a.Id == 3)] = actors.Single(a => a.Id == 3) with { Cast = null };
        Apply("any watched Finish clears both lists");
        Check(region.Encounters[3].Casts.Count == 0, "Leaves clear both colors.");
    }

    private static void FireStacks(int definition)
    {
        using var f = new Fixture(definition);
        var rule = f.M.Definition.Rules.Single(r => r.Kind == BozjaRuleKind.Stack);
        var source = f.Activate<BossMod.Components.StackWithCastTargets>(rule.Component);
        f.Start(source, rule.Actions[0]);
        void Compare(string step)
        {
            var hints = new BossComponent.TextHints();
            source.AddHints(0, f.Viewer, hints);
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.StackHints(f.NativeViewer, rule)
                .Select(h => (h.Text, h.Risky))), step + " stack hint.");
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            var nav = f.M.Navigation(f.NativeViewer).Where(z => z.Component == rule.Component).ToArray();
            Check(ai.ForbiddenZones.Count == nav.Length, step + " actual stack raw navigation count.");
            for (var i = 0; i < nav.Length; ++i)
            {
                var expected = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0,
                    f.Center, 29.5f, out var kind);
                var goal = kind == MechanicNavZoneKind.ComplementSkipped
                    && MechanicHintsConvert.TryConvertComplement(ai.ForbiddenZones[i].Item1, 0, out _);
                if (goal && MechanicHintsConvert.TryConvertComplement(ai.ForbiddenZones[i].Item1, 0, out var accepted))
                    expected = accepted;
                Check(nav[i].Goal == goal, step + " actual buddy converted zone kind.");
                var actual = nav[i].Convert(f.Center, 29.5f).ToArray();
                Check(expected.Count == actual.Length, step + " actual buddy converted zone count.");
                for (var x = -31f; x < 31; x += .91f)
                    for (var z = -31f; z < 31; z += .83f)
                    {
                        var p = f.Center + new Vector3(x, 0, z);
                        Check(expected.Any(v => v.Template.Resolve(v.Position, v.Rotation, null).Contains(p, 0))
                            == actual.Any(v => v.Contains(p, 0)), step + " actual buddy converted geometry.");
                    }
            }
        }
        f.Move(101, f.Center + new Vector3(1, 0, 0)); Compare("two of eight target");
        Check(f.M.Navigation(f.NativeViewer).Any(z => z.Component == rule.Component), "Two players must retain buddy nav.");
        for (ulong id = 102; id < 108; ++id)
        {
            f.Add(id, 0, f.Center, ActorType.Player); f.Party.Add(new((int)id - 100, "", id));
            f.W.Execute(new PartyState.OpModify((int)id - 100, new(id, id, false, ""))); f.Sync();
            Compare("party size " + f.Party.Count);
        }
        Check(!f.M.StackHints(f.NativeViewer, rule).Single().Risky, "Eight target satisfied.");
        f.Set(2, a => a with { Cast = a.Cast! with { TargetId = 101 } }); f.Sync();
        var spell = new ActorCastInfo { Action = new(ActionType.Spell, rule.Actions[0]), TargetID = 101, TotalTime = 3 };
        source.Stacks.Clear(); source.OnCastStarted(f.W.Actors.Find(2)!, spell);
        f.Move(100, f.Center + new Vector3(20, 0, 0)); Compare("outside complete stack");
        f.Move(102, f.Center + new Vector3(20, 0, 0)); Compare("outside incomplete stack");
        f.Move(100, f.Center); Compare("non target inside incomplete stack");
    }
}
