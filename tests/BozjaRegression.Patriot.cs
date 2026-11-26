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
    private static void RunPatriot(Action<string, Action> run)
    {
        run("Bozja native CE24 ordinary source shapes raw Finish and client manifest", PatriotStandard);
        run("Bozja native CE24 square exact bounds corner pull no circular gate", PatriotArena);
        run("Bozja native CE24 mines actual party towers HP role R8 replacement hints drawing SDF damage", PatriotMines);
        run("Bozja native CE24 mines effect FIFO position tolerance true Update gate", PatriotMineEvents);
        run("Bozja native CE24 laser spatial charge cap retained state first mine conditional navigation", PatriotReaction);
        run("Bozja native CE24 raw tether transitions cold actor order no invented history", PatriotTethers);
        run("Bozja native CE24 actual Feed cold tether order full inputs and unchanged snapshot dedup", PatriotFeed);
        run("Bozja native CE24 real duplicate tether events hot zero targets and frame reconciliation", PatriotHotTethers);
        run("Bozja native CE24 actual Feed frame event batch cold activation cancel and stale snapshots", PatriotEventBatch);
        run("Bozja native CE24 both source SingleTargetCast dynamic damage no invented raidwide", () => Damage(5));
    }

    private static ActorCastEvent PatriotEvent(uint action)
        => new(new(ActionType.Spell, action), 100, 0, 0, default, 0, 0, default);

    private static void PatriotStandard()
    {
        foreach (var rule in BozjaCatalog.Definitions[5].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
        {
            using var f = new Fixture(5);
            var source = f.Activate<BossMod.Components.GenericAOEs>(rule.Component);
            var spell = f.Start(source, rule.Actions[0]);
            CompareHounds(f, source, rule.Component);
            f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareHounds(f, source, "past predicted clock");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell);
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareHounds(f, source, "watched Finish");
        }
        Initialize();
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.CriticalEngagement.CE24PatriotGames.AID>())
            Check(new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE24 actual extra " + action);
        var name = data!.GetExcelSheet<Lumina.Excel.Sheets.DynamicEvent>()!.GetRow(10).Name.ToString();
        Console.WriteLine("INFO CE24 DynamicEvent10 actual name=" + name);
        Check(name == BozjaCatalog.Definitions[5].Name, "CE24 actual client name.");
    }

    private static void PatriotArena()
    {
        using var f = new Fixture(5);
        var bounds = MechanicHintsConvert.ConvertBounds(f.Source.Arena.Bounds, f.Source.Arena.Center, 0)!;
        for (var x = -26f; x <= 26; x += .37f)
            for (var z = -26f; z <= 26; z += .41f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                Check(bounds.Contains(p, 0) == f.M.Arena.Contains(p, 0), "CE24 actual square bounds.");
            }
        f.W.Actors.Find(1)!.InCombat = true;
        var checkPull = f.Source.GetType().GetMethod("CheckPull", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        foreach (var offset in new[] { new Vector3(24, 0, 24), new Vector3(25, 0, 25), new Vector3(25.01f, 0, 0) })
        {
            f.Viewer.PosRot = new(f.Center + offset, 0);
            var model = new BozjaEncounter(f.M.Definition);
            model.Synchronize(f.Actors.Select(a => a.Id == 100 ? a with { Position = f.Center + offset } : a),
                f.Party.ToArray(), 100, START);
            Check(model.Active == (bool)checkPull.Invoke(f.Source, null)!, "CE24 inclusive square pull " + offset);
        }
    }

    private static void PatriotMember(Fixture f, ulong id, Class job, uint hp, bool dead = false)
    {
        f.Set(id, a => a with { ClassJob = (uint)job, Hp = hp, Dead = dead });
        var source = f.W.Actors.Find(id)!;
        source.Class = job;
        source.HPMP = new(hp, 100000, 0, 0, 0);
        source.IsDead = dead;
    }

    private static void PatriotMines()
    {
        using var f = new Fixture(5);
        var source = f.Activate<BossMod.Components.GenericTowers>("LightningRod");
        f.Move(2, f.Center);
        void Effect(uint action)
        {
            source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(action));
            f.M.Effect(2, action);
        }
        Effect(20448);
        f.Move(2, f.Center + new Vector3(12, 0, 0)); Effect(20448);
        foreach (var job in Enum.GetValues<Class>())
        {
            foreach (var hp in new uint[] { 0, 39999, 40000, 40001 })
            {
                PatriotMember(f, 100, job, hp);
                PatriotMember(f, 101, Class.PLD, 1);
                f.Sync(); source.Update();
                Check(source.Towers.Select(t => t.ForbiddenSoakers.Raw).SequenceEqual(f.M.Mines.Select(m => m.ForbiddenSoakers)),
                    "CE24 exact role/hp forbidden " + job + "/" + hp);
            }
        }
        foreach (var hp in new uint[] { 39999, 40000 })
            foreach (var buddyDead in new[] { false, true })
                foreach (var offset in new[] { new Vector3(-12, 0, 0), Vector3.Zero, new Vector3(6, 0, 0), new Vector3(12, 0, 0) })
                {
                    PatriotMember(f, 100, Class.WHM, hp);
                    PatriotMember(f, 101, Class.PLD, 1, buddyDead);
                    f.Move(100, f.Center + offset);
                    f.Move(101, f.Center);
                    source.Update(); f.Sync();
                    ComparePatriotMines(f, source, $"{hp}/{buddyDead}/{offset}");
                }
        f.Add(102, 0, f.Center, ActorType.Player);
        f.Party.Add(new(8, "", 102));
        f.W.Execute(new PartyState.OpModify(8, new(102, 102, false, "")));
        PatriotMember(f, 102, Class.WHM, 1);
        PatriotMember(f, 100, Class.WHM, 40000, true);
        f.Sync(); source.Update();
        Check(source.Towers[0].ForbiddenSoakers.Raw == f.M.Mines[0].ForbiddenSoakers
            && (f.M.Mines[0].ForbiddenSoakers & (1UL << 8)) == 0, "CE24 Update first8 includes dead excludes alliance.");
        ComparePatriotMines(f, source, "living alliance included in tower counts");
    }

    private static void ComparePatriotMines(Fixture f, BossMod.Components.GenericTowers source, string step)
    {
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
            step + " exact hints.");
        var expected = source.ActiveTowers(0, f.Viewer).ToArray();
        var views = f.M.MineViews(f.NativeViewer).ToArray();
        Check(expected.Length == views.Length, step + " tower count.");
        for (var i = 0; i < views.Length; ++i)
        {
            var t = expected[i];
            Check(t.Position.ToVec3() == views[i].Mine.Position && t.Shape is AOEShapeCircle c && c.Radius == views[i].Radius
                && t.Activation == default && t.ForbiddenSoakers.Raw == views[i].Mine.ForbiddenSoakers, step + " active tower raw.");
        }
        var sink = new MechanicDrawSink(new());
        using (MechanicDrawSink.Install(sink)) source.DrawArenaBackground(0, f.Viewer);
        CompareDraw(views.Where(v => v.Background).ToArray(), false);
        sink.Commands.Clear();
        using (MechanicDrawSink.Install(sink)) source.DrawArenaForeground(0, f.Viewer);
        CompareDraw(views.Where(v => v.Outline).ToArray(), true);
        void CompareDraw(BozjaMineView[] selected, bool outline)
        {
            var circles = sink.Commands.OfType<MechanicDrawZoneCircle>().ToArray();
            Check(circles.Length == selected.Length, step + " Draw count.");
            for (var i = 0; i < selected.Length; ++i)
                Check(circles[i].Center.ToVec3() == selected[i].Mine.Position && circles[i].Radius == selected[i].Radius
                    && circles[i].Outline == outline
                    && circles[i].Color == (!outline ? Colors.AOE : selected[i].Safe ? Colors.Safe : Colors.Danger),
                    step + " actual Draw fields.");
        }
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        var zones = f.M.Navigation(f.NativeViewer).ToArray();
        Check(ai.ForbiddenZones.Count == zones.Length, step + " raw navigation count.");
        for (var i = 0; i < zones.Length; ++i)
        {
            var zone = zones[i]; var e = ai.ForbiddenZones[i];
            Check(e.Item2 == zone.Activation, step + " default activation.");
            var converted = MechanicHintsConvert.ConvertForbiddenZone(e.Item1, 0, f.Center, f.M.SampleRadius, out _);
            var shapes = zone.Convert(f.Center, f.M.SampleRadius).ToArray();
            Check(converted.Count == shapes.Length, step + " actual conversion count.");
            for (var x = -26f; x <= 26; x += .93f)
                for (var z = -26f; z <= 26; z += 1.17f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    var actual = zone.Join == null
                        ? MathF.Sqrt(BozjaEncounter.DistanceSquared(zone.Origin, p)) - ((CircleTemplate)zone.Shape!).Radius
                        : zone.Distance(p.X, p.Z);
                    Check(e.Item1.Distance(new(p.X, p.Z)) == actual, step + " raw SDF.");
                    Check(converted.Any(s => s.Template.Resolve(s.Position, s.Rotation, null).Contains(p, 0))
                        == shapes.Any(s => s.Contains(p, 0)), step + " converted geometry.");
                }
        }
        var damage = f.M.Damage(f.NativeViewer).ToArray();
        Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
            .SequenceEqual(damage.Select(d => (d.Mask, d.Activation, d.Type))), step + " exact damage mask clock type.");
    }

    private static void PatriotMineEvents()
    {
        using var f = new Fixture(5);
        var source = f.Activate<BossMod.Components.GenericTowers>("LightningRod");
        PatriotMember(f, 100, Class.WHM, 39999);
        PatriotMember(f, 101, Class.PLD, 1);
        f.Sync();
        void Effect(uint action)
        {
            source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(action));
            f.M.Effect(2, action);
            Check(source.Towers.Select(t => t.Position.ToVec3()).SequenceEqual(f.M.Mines.Select(m => m.Position)), "CE24 mine effect position FIFO.");
            Check(source.Towers.Select(t => t.ForbiddenSoakers.Raw).SequenceEqual(f.M.Mines.Select(m => m.ForbiddenSoakers)),
                "CE24 new mine defaults until Update.");
        }
        Effect(20448); Check(f.M.Mines[0].ForbiddenSoakers == 0, "CE24 effects do not invent Update.");
        source.Update(); f.Sync();
        Effect(20448);
        Check(f.M.Mines[0].ForbiddenSoakers == 1 && f.M.Mines[1].ForbiddenSoakers == 0, "CE24 independent per-mine Update age.");
        var q = source.Towers[0].Position.ToVec3();
        f.Move(2, q + new Vector3(1, 0, 1)); source.Update(); Effect(20449);
        Check(f.M.Mines.Count == 1, "CE24 square AlmostEqual removes only first.");
        f.Move(2, q + new Vector3(1.001f, 0, 0)); source.Update(); Effect(20450);
        Check(f.M.Mines.Count == 1, "CE24 outside exact tolerance must retain.");
        f.Move(2, q); source.Update(); Effect(20450);
        Check(f.M.Mines.Count == 0, "CE24 second removal.");
        Effect(20448);
        f.Remove(2);
        Check(f.M.Mines.Count == source.Towers.Count && f.M.Mines.Count == 1, "CE24 source destruction does not clear mines.");
        f.Now = START.AddSeconds(99); f.M.Advance(f.Now);
        Check(f.M.Mines.Count == 1, "CE24 default clock not TTL.");
    }

    private static void PatriotReaction()
    {
        using var f = new Fixture(5);
        PatriotMember(f, 100, Class.PLD, 1000);
        PatriotMember(f, 101, Class.PLD, 1000);
        f.Sync();
        var towers = f.Activate<BossMod.Components.GenericTowers>("LightningRod");
        var source = f.Activate<BossMod.Components.GenericAOEs>("ElectrochemicalReaction");
        void Tether(ulong id, uint tether, Vector3 position, float rotation)
        {
            f.Move(id, position);
            f.W.Actors.Find(id)!.PosRot = new(position, rotation);
            f.Set(id, a => a with { Rotation = rotation }); f.Sync();
            var info = new ActorTetherInfo(tether, 1);
            source.OnTethered(f.W.Actors.Find(id)!, info);
            f.M.PatriotTethered(f.M.Actors[id], tether);
            ComparePatriotReaction(f, source, "CE24 reaction exact visible");
        }
        var origin = f.Center + new Vector3(7.123f, 0, 3.456f);
        Tether(2, 114, origin, .731f);
        f.Now = START.AddSeconds(2); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        Tether(2, 115, origin + new Vector3(.9f, 0, .9f), 1.1f);
        Check(f.M.Casts.Single().Activation == f.Now.AddSeconds(10.2d), "CE24 clock second tether.");
        CompareNavigation("no mine no navigation", false);
        towers.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(20448)); f.M.Effect(2, 20448);
        PatriotMember(f, 100, Class.PLD, 1); f.Sync(); towers.Update();
        CompareNavigation("allowed mine suppresses reaction", false);
        PatriotMember(f, 100, Class.WHM, 39999); f.Sync(); towers.Update();
        CompareNavigation("forbidden first mine enables reaction", true);
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(20443)); f.M.Effect(2, 20443);
        Check(!f.M.Visible().Any() && source.ActiveAOEs(0, f.Viewer).Length == 0, "CE24 effect clears AOEs.");
        Tether(2, 114, origin, .2f);
        Tether(2, 115, origin, .3f);
        Check(f.M.Visible().Count() == 1, "CE24 Effect retained bartizan positions.");
        for (var i = 0; i < 4; ++i)
        {
            var p = f.Center + new Vector3(-20 + i * 5, 0, -15);
            Tether(2, 114, p, .4f); Tether(2, 115, p, .6f);
        }
        Check(f.M.Visible().Count() == 4, "CE24 four spatial locations cap excludes fifth.");
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        ComparePatriotReaction(f, source, "CE24 no TTL");
        f.M.Finish(f.M.Actors[2], new(20443, 100, origin, 0, START, START, false, false));
        ComparePatriotReaction(f, source, "CE24 Finish does not clear");
        void CompareNavigation(string step, bool expect)
        {
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            var zones = f.M.Navigation(f.NativeViewer).Where(z => z.Component == "ElectrochemicalReaction").ToArray();
            Check(ai.ForbiddenZones.Count == zones.Length && (zones.Length > 0) == expect, step + " conditional navigation.");
            for (var i = 0; i < zones.Length; ++i)
                Check(ai.ForbiddenZones[i].Item2 == zones[i].Activation, step + " activation retained.");
        }
    }

    private static void PatriotTethers()
    {
        using var f = new Fixture(5);
        f.Activate<BossMod.Components.GenericTowers>("LightningRod");
        var source = f.Activate<BossMod.Components.GenericAOEs>("ElectrochemicalReaction");
        void Apply(ushort id, ulong target, float rotation)
        {
            var actor = f.W.Actors.Find(2)!;
            actor.PosRot = new(actor.Position.ToVec3(), rotation);
            if (actor.Tether.ID != id || actor.Tether.Target != target)
                f.W.Execute(new ActorState.OpTether(2, new ActorTetherInfo(id, target)));
            f.Set(2, a => a with { Rotation = rotation, Tethers = id == 0 ? [] : [new(id, target)] });
            f.Sync();
            ComparePatriotReaction(f, source, "CE24 real OpTether transition");
        }
        Apply(114, 1, .7f); Apply(114, 1, .7f);
        Check(!f.M.Visible().Any(), "CE24 duplicate unchanged snapshot no callback.");
        Apply(114, 100, .8f);
        Check(f.M.Visible().Count() == 1, "CE24 target change emits second tether.");
        Apply(0, 0, .8f); Apply(115, 1, .9f);
        Check(f.M.Visible().Count() == 1, "CE24 cancel does not clear charges or AOEs.");
        Apply(114, 1, 1.1f);
        Check(f.M.Visible().Count() == 2, "CE24 subsequent two accumulate.");
    }

    private static void ComparePatriotReaction(Fixture f, BossMod.Components.GenericAOEs source, string step)
    {
        var expected = source.ActiveAOEs(0, f.Viewer).ToArray();
        var actual = f.M.Visible().Where(a => a.Cast.Rule.Component == "ElectrochemicalReaction").ToArray();
        Check(expected.Length == actual.Length, step + " visible count.");
        for (var i = 0; i < expected.Length; ++i)
        {
            var e = expected[i]; var a = actual[i].Cast;
            Check(e.Origin.ToVec3() == a.Origin && e.Rotation.Rad == a.Rotation && e.Activation == a.Activation
                && e.Risky == actual[i].Risky && e.Color == 0, step + " locked raw fields.");
            for (var x = -55f; x < 55; x += 1.31f)
                for (var z = -55f; z < 55; z += 1.57f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(e.Check(new(p.X, p.Z)) == a.Contains(p), step + " exact raw rectangle.");
                }
        }
        var hints = new BossComponent.TextHints();
        foreach (var component in f.Source.Components) component.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
            step + " complete component hints.");
        var ai = new AIHints(); f.Source.CalculateAIHints(0, f.Viewer, default, ai);
        var zones = f.M.Navigation(f.NativeViewer).ToArray();
        Check(ai.ForbiddenZones.Count == zones.Length, step + " complete tower + conditional laser navigation count.");
        for (var i = 0; i < zones.Length; ++i)
        {
            Check(ai.ForbiddenZones[i].Item2 == zones[i].Activation, step + " exact navigation clock.");
            var converted = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, f.Center, f.M.SampleRadius, out _);
            var shapes = zones[i].Convert(f.Center, f.M.SampleRadius).ToArray();
            Check(converted.Count == shapes.Length, step + " actual converted navigation count.");
            for (var x = -27f; x < 27; x += .91f)
                for (var z = -27f; z < 27; z += .83f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(converted.Any(s => s.Template.Resolve(s.Position, s.Rotation, null).Contains(p, 0))
                        == shapes.Any(s => s.Contains(p, 0)), step + " actual converted navigation geometry.");
                }
        }
    }

    private static void PatriotHotTethers()
    {
        using var f = new Fixture(5);
        f.Activate<BossMod.Components.GenericTowers>("LightningRod");
        var source = f.Activate<BossMod.Components.GenericAOEs>("ElectrochemicalReaction");
        void Event(ushort id, ulong target)
        {
            f.W.Execute(new ActorState.OpTether(2, new(id, target == 0xE0000000 ? 0 : target)));
            f.M.Tether(2, id, target);
            ComparePatriotReaction(f, source, "actual hot event " + id + "/" + target);
        }
        Event(114, 1);
        Event(114, 1);
        Check(f.M.Visible().Count() == 1, "CE24 identical actual events must both charge.");
        f.Set(2, a => a with { Tethers = [new(114, 1)] });
        f.Sync();
        ComparePatriotReaction(f, source, "snapshot catches up without duplicate");
        Event(115, 0); Event(115, 0xE0000000);
        Check(f.M.Visible().Count() == 1, "CE24 zero/sentinel hot target cannot charge.");
        Event(115, 1);
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(20443)); f.M.Effect(2, 20443);
        Event(114, 1);
        Check(f.M.Visible().Count() == 1, "CE24 Effect preserves half charge.");
        Event(0, 0);
        Event(114, 1);
        Event(114, 1);
        Check(f.M.Visible().Count() == 2, "CE24 cancel preserves locations and paired charging.");
    }

    private static void PatriotFeed()
    {
        Initialize();
        foreach (var reverse in new[] { false, true })
        foreach (var zeroTarget in new[] { false, true })
        {
            using var session = new MechanicSession("CE24-real-cold-tethers");
            var feed = new MechanicWorldFeed(session);
            var region = new BozjaRegion();
            var center = BozjaCatalog.Definitions[5].Center;
            ReplayActorState[] actors =
            [
                new() { Id = 1, Oid = 0x2E14, Kind = 2, SubKind = 5, Position = center, Hp = 100, InCombat = true, Targetable = true },
                new() { Id = 100, Kind = 1, Position = center, Hp = 100, ClassJob = 19 },
                new() { Id = 20, Oid = 0x2F02, Kind = 2, SubKind = 5, Position = center, Rotation = .731f,
                    Tethers = [new(114, zeroTarget ? 0UL : 1UL)] },
                new() { Id = 21, Oid = 0x2F02, Kind = 2, SubKind = 5, Position = center + new Vector3(.9f, 0, .9f),
                    Rotation = 1.1f, Tethers = [new(115, zeroTarget ? 0UL : 1UL)] },
            ];
            if (reverse) (actors[2], actors[3]) = (actors[3], actors[2]);
            var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
            void Apply(string step, DateTime now)
            {
                session.BeginFrame(now);
                var input = actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray();
                feed.ApplyFrame(new(920, 735, 100, 0, [new(0, 100, 100, "")], [], input));
                session.Update();
                region.Synchronize(actors, [new(0, "", 100)], 100, now, true);
                Compare(step);
            }
            void Compare(string step)
            {
                var module = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x2E14);
                var source = (BossMod.Components.GenericAOEs)module.Components.Single(c => c.GetType().Name == "ElectrochemicalReaction");
                var expected = source.ActiveAOEs(0, session.World.Actors.Find(100)!).ToArray();
                var actual = region.Encounters[5].Visible().ToArray();
                Check(expected.Length == actual.Length, step + " actual Feed count.");
                for (var i = 0; i < expected.Length; ++i)
                    Check(expected[i].Origin.ToVec3() == actual[i].Cast.Origin && expected[i].Rotation.Rad == actual[i].Cast.Rotation
                        && expected[i].Activation == actual[i].Cast.Activation, step + " actual Feed exact fields.");
            }
            Apply("cold order " + reverse, START);
            Check(region.Encounters[5].Casts.Count == 1, "CE24 cold real two Actor tethers produce one prediction.");
            Apply("unchanged", START.AddSeconds(1));
            Check(region.Encounters[5].Casts.Count == 1, "CE24 unchanged actual Feed no callback.");
            actors = actors.Select(a => a.Id is 20 or 21 ? a with { Tethers = [new(a.Tethers[0].Id, 100)] } : a).ToArray();
            Apply("both target transitions", START.AddSeconds(2));
            Check(region.Encounters[5].Casts.Count == 2, "CE24 actual Feed target transitions second pair.");
            foreach (var target in new ulong[] { 100, 100, 0, 0xE0000000 })
            {
                feed.ApplyEvent(new MechanicEventTether(20, 114, target));
                region.Tether(20, 114, target);
                Compare("actual Feed duplicate/zero event " + target);
            }
            Check(region.Encounters[5].Casts.Count == 3, "CE24 actual Feed repeats charge, zero hot targets do not.");
        }
    }

    private static void PatriotEventBatch()
    {
        Initialize();
        foreach (var mode in new[] { "active", "cold", "cancel" })
        {
            using var session = new MechanicSession("CE24-real-event-batch");
            var feed = new MechanicWorldFeed(session);
            var region = new BozjaRegion();
            var center = BozjaCatalog.Definitions[5].Center;
            ReplayActorState[] actors =
            [
                new() { Id = 1, Oid = 0x2E14, Kind = 2, SubKind = 5, Position = center, Hp = 100, InCombat = mode != "cold", Targetable = true },
                new() { Id = 100, Kind = 1, Position = center, Hp = 100, ClassJob = 19 },
                new() { Id = 20, Oid = 0x2F02, Kind = 2, SubKind = 5, Position = center, Rotation = .731f },
                new() { Id = 21, Oid = 0x2F02, Kind = 2, SubKind = 5, Position = center, Rotation = 1.1f },
            ];
            var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
            void Frame(DateTime now, params MechanicFeedEvent[] events)
            {
                session.BeginFrame(now);
                var input = actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray();
                feed.ApplyFrame(new(920, 735, 100, 0, [new(0, 100, 100, "")], [], input));
                foreach (var e in events)
                {
                    feed.Enqueue(e);
                    // Live OnLogEvent can scan the same snapshot between every
                    // packet. Only the final frame pass may observe its tether.
                    region.Synchronize(actors, [new(0, "", 100)], 100, now, false);
                }
                feed.Flush();
                session.Update();
                region.Synchronize(actors, [new(0, "", 100)], 100, now, true, () =>
                {
                    foreach (var e in events)
                    {
                        if (e is MechanicEventTether t) region.Tether(t.SourceId, t.TetherId, t.TargetId);
                        if (e is MechanicEventTetherCancel c) region.Tether(c.SourceId, 0, 0);
                        if (e is MechanicEventActionEffect effect) region.Effect(effect.CasterId, effect.ActionId);
                    }
                });
                var module = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x2E14);
                var source = module.Components.OfType<BossMod.Components.GenericAOEs>()
                    .SingleOrDefault(c => c.GetType().Name == "ElectrochemicalReaction");
                var expected = source == null ? [] : source.ActiveAOEs(0, session.World.Actors.Find(100)!).ToArray();
                var actual = region.Encounters[5].Visible().ToArray();
                Check(expected.Length == actual.Length, mode + " batched count.");
                for (var i = 0; i < expected.Length; ++i)
                    Check(expected[i].Origin.ToVec3() == actual[i].Cast.Origin
                        && expected[i].Rotation.Rad == actual[i].Cast.Rotation
                        && expected[i].Activation == actual[i].Cast.Activation, mode + " batched exact fields.");
            }
            Frame(START);
            actors = actors.Select(a => a.Id == 1 ? a with { InCombat = true }
                : a.Id == 20 && mode != "cold" ? a with { Tethers = [new(114, 100)] } : a).ToArray();
            if (mode == "cancel")
            {
                Frame(START.AddSeconds(1));
                Frame(START.AddSeconds(2), new MechanicEventTetherCancel(20));
                Check(region.Encounters[5].Casts.Count == 0, "CE24 Cancel plus repeated scans must not invent charge.");
                Frame(START.AddSeconds(3), new MechanicEventTether(20, 114, 100));
            }
            else if (mode == "cold")
            {
                Frame(START.AddSeconds(1), new MechanicEventTether(20, 114, 100), new MechanicEventTether(21, 115, 100));
                Check(region.Encounters[5].Casts.Count == 1, "CE24 events before cold activation must survive stale snapshots.");
            }
            else
            {
                Frame(START.AddSeconds(1), new MechanicEventTether(20, 114, 1), new MechanicEventTether(20, 114, 100));
                Check(region.Encounters[5].Casts.Count == 1, "CE24 snapshot plus two different packets produce three charges.");
                Frame(START.AddSeconds(2), new MechanicEventTether(20, 114, 100));
                Check(region.Encounters[5].Casts.Count == 2, "CE24 odd batch retains half charge.");
            }
        }
    }
}
