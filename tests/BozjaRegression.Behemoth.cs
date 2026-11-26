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
    private static void RunBehemoth(Action<string, Action> run)
    {
        run("Bozja native CE33 real client name extra ordinary rectangle precise bounds", BehemothStandard);
        run("Bozja native CE33 meteor exact source polygons dead corpse hitbox freeze true navigation", BehemothMeteor);
        run("Bozja native CE33 meteor multiple caster origin current residual order FIFO Finish no blockers", BehemothMeteorQueue);
        run("Bozja native CE33 meteor overlapping near far zero radius exact polygons and sampler boundaries", BehemothMeteorEdges);
        run("Bozja native CE33 juice cast event live order original source target fallback repeats no TTL", BehemothJuice);
        run("Bozja native CE33 juice actual objects consume first per Update state7 dead birth order", BehemothJuiceUpdates);
        run("Bozja native CE33 Juice lazy enemy cache before first query null distinct from real zero", BehemothJuiceCache);
        run("Bozja native CE33 delayed raidwide single fixed clock Finish destroy timeout cross source Effect", BehemothBolt);
        run("Bozja native CE33 Horn original target dynamic cone own R100 hints damage Finish Effect first ID", BehemothHorn);
        run("Bozja native CE33 Horn Update destroyed target retained dead target source removed", BehemothHornUpdate);
        run("Bozja native CE33 Horn pre Update dead head clock current party masks missing and self targets", BehemothHornClock);
        run("Bozja native CE33 corpse Thunderbolt circle first rectangle all locked Finish clears all", BehemothThunderbolt);
        run("Bozja native CE33 cold existing Thunderbolt no invented Finish history actor order", BehemothCold);
        run("Bozja native CE33 actual Feed full six component cold activation frame Finish packet batch single Update", BehemothFeed);
        run("Bozja native CE33 translated meteor source row hull rounding boundary", BehemothMeteorRow);
    }

    private static void BehemothStandard()
    {
        Initialize();
        var name = data!.GetExcelSheet<Lumina.Excel.Sheets.DynamicEvent>()!.GetRow(15).Name.ToString();
        Console.WriteLine("INFO CE33 DynamicEvent15=" + name);
        Check(name == BozjaCatalog.Definitions[8].Name, "CE33 actual client name.");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.CriticalEngagement.CE33WhereStrodeTheBehemoth.AID>())
            Check(new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE33 actual extra " + action);
        using var f = new Fixture(8);
        var source = f.Activate<BossMod.Components.GenericAOEs>("ZombieBreath");
        var spell = f.Start(source, 20198);
        CompareMetalFoxBits(f, source, "ZombieBreath", "CE33 rect locked");
        f.Move(2, f.Center + new Vector3(27, 0, 11));
        CompareMetalFoxBits(f, source, "ZombieBreath", "CE33 rect no tracking");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell);
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareMetalFoxBits(f, source, "ZombieBreath", "CE33 rect watched Finish");
        var bounds = MechanicHintsConvert.ConvertBounds(f.Source.Arena.Bounds, f.Source.Arena.Center, 0)!;
        for (var i = 0; i < 360; ++i)
            foreach (var r in new[] { 29f, 29.5f, 29.51f, 30f })
            {
                var p = f.Center + r * new Vector3(MathF.Sin(i * MathF.PI / 180), 0, MathF.Cos(i * MathF.PI / 180));
                Check(bounds.Contains(p, 0) == f.M.Arena.Contains(p, 0), "CE33 polygon exact boundary.");
            }
    }

    private static Actor AddCorpse(Fixture f, ulong id, Vector3 p, float radius = 4.35f)
    {
        var corpse = f.Add(id, 0x2DCC, p);
        corpse.HitboxRadius = radius;
        f.Set(id, a => a with { HitboxRadius = radius });
        f.Sync();
        return corpse;
    }

    private static void CompareMeteor(Fixture f, BossMod.Components.CastLineOfSightAOE source, string step,
        string component = "BlazingMeteor")
    {
        var expected = source.ActiveAOEs(0, f.Viewer).ToArray();
        var actual = f.M.Visible(f.NativeViewer).Where(a => a.Cast.Rule.Component == component).ToArray();
        Check(expected.Length == actual.Length, step + " safe count.");
        for (var i = 0; i < expected.Length; ++i)
        {
            var e = expected[i];
            var a = actual[i].Cast;
            Check(e.Origin.ToVec3() == a.Origin && e.Activation == a.Activation
                && e.Color == Colors.SafeFromAOE && actual[i].Risky, step + " exact fields.");
            var shape = (AOEShapeCustom)e.Shape;
            var polygon = shape.GetCombinedPolygon(e.Origin);
            var expectedContours = polygon.Parts.Select(p => p.Exterior.ToArray().Select(v =>
                e.Origin.ToVec3() + new Vector3(v.X, 0, v.Z)).ToArray()).ToArray();
            var contours = a.Shape.Resolve(a.Origin, 0, null).GetContours();
            Check(expectedContours.Length == contours.Count, step + " exact display part count.");
            for (var j = 0; j < contours.Count; ++j)
                Check(expectedContours[j].SequenceEqual(contours[j]), step + " exact source display vertices.");
            for (var x = -31f; x <= 31; x += .53f)
                for (var z = -31f; z <= 31; z += .67f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(e.Check(new(p.X, p.Z)) == a.Contains(p), step + $" raw polygon {p}.");
                }
        }
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer)
            .Where(h => h.Text == "Hide behind obstacle!").Select(h => (h.Text, h.Risky))), step + " personal warning.");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer)
            .Where(z => z.Component == component).ToArray(), step + " real sampled navigation");
    }

    private static void BehemothMeteor()
    {
        using var f = new Fixture(8);
        AddCorpse(f, 30, f.Center + new Vector3(4.123f, 0, .257f));
        f.W.Actors.Find(30)!.IsDead = true;
        f.Set(30, a => a with { Dead = true, Targetable = false, EventState = 7 }); f.Sync();
        var source = f.Activate<BossMod.Components.CastLineOfSightAOE>("BlazingMeteor");
        f.Start(source, 20194);
        CompareMeteor(f, source, "dead untargetable state7 blocker");
        var original = f.M.Meteors.Single();
        f.Move(30, f.Center + new Vector3(20, 0, 15));
        CompareMeteor(f, source, "frozen after corpse movement");
        f.Remove(30); CompareMeteor(f, source, "frozen after corpse destroy");
        f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CompareMeteor(f, source, "expired clock retained");
        Check(ReferenceEquals(original, f.M.Meteors.Single()), "CE33 frozen visibility generation.");
    }

    private static void BehemothMeteorQueue()
    {
        using var f = new Fixture(8);
        var source = f.Activate<BossMod.Components.CastLineOfSightAOE>("BlazingMeteor");
        f.Start(source, 20194);
        CompareMeteor(f, source, "no blockers no blanket danger");
        AddCorpse(f, 30, f.Center + new Vector3(2, 0, 1));
        f.Add(3, 0x233C, f.Center + new Vector3(-15, 0, -5)); f.Sync();
        var second = f.Start(source, 20194, 3, total: 9);
        CompareMeteor(f, source, "second Start uses first active origin new clock");
        source.OnCastFinished(f.W.Actors.Find(3)!, second);
        f.M.Finish(f.M.Actors[3], f.M.Actors[3].Cast!);
        CompareMeteor(f, source, "later caster Finish removes first safe zone");
        f.W.Actors.Find(3)!.CastInfo = null;
        f.Set(3, a => a with { Cast = null }); f.Sync();
        f.Start(source, 20194, 3, total: .5f);
        CompareMeteor(f, source, "new earliest residual origin");
    }

    private static void BehemothJuice()
    {
        using var f = new Fixture(8);
        var source = f.Activate<BossMod.Components.GenericAOEs>("ZombieJuice");
        var spell = f.Start(source, 20196);
        CompareMetalFoxBits(f, source, "ZombieJuice", "cast only no spawn delay");
        f.Move(100, f.Center + new Vector3(3, 0, 4));
        CompareMetalFoxBits(f, source, "ZombieJuice", "cast follows current target");
        var position = f.Center + new Vector3(-8.123f, 0, 7.257f);
        var effect = new ActorCastEvent(new(ActionType.Spell, 20196), 999, 0, 0, position, 0, 0, default);
        source.OnEventCast(f.W.Actors.Find(2)!, effect); f.M.Effect(2, 20196, 999, position);
        source.OnEventCast(f.W.Actors.Find(2)!, effect); f.M.Effect(2, 20196, 999, position);
        CompareMetalFoxBits(f, source, "ZombieJuice", "two identical Effects plus live cast");
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(20199)); f.M.Effect(2, 20199);
        CompareMetalFoxBits(f, source, "ZombieJuice", "repeat ignored");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareMetalFoxBits(f, source, "ZombieJuice", "Finish removes cast only");
        f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CompareMetalFoxBits(f, source, "ZombieJuice", "events not expired");
    }

    private static void BehemothMeteorEdges()
    {
        foreach (var layout in new (Vector3 Position, float Radius)[][]
        {
            [(new(0, 0, 0), 4.35f)],
            [(new(0, 0, 40), 1)],
            [(new(0, 0, 5), 0)],
            [(new(0, 0, 5), 4.35f), (new(2, 0, 7), 4.35f), (new(-3, 0, 9), 2)],
            [(new(0, 0, 1), 4.35f), (new(0, 0, -1), 4.35f)],
        })
        {
            using var f = new Fixture(8);
            f.Move(2, f.Center);
            for (var i = 0; i < layout.Length; ++i)
                AddCorpse(f, (ulong)(30 + i), f.Center + layout[i].Position, layout[i].Radius);
            var source = f.Activate<BossMod.Components.CastLineOfSightAOE>("BlazingMeteor");
            f.Start(source, 20194);
            CompareMeteor(f, source, "edge layout " + string.Join(';', layout));
            Check(f.M.Meteors[0].Drawable == (f.M.Meteors[0].Shape.Resolve(f.Center, 0, null).GetContours().Count > 0),
                "CE33 empty source exterior cannot publish a preview.");
            foreach (var point in f.M.Meteors.Single().Shape.Resolve(f.Center, 0, null).GetContours().SelectMany(c => c))
            {
                var expected = source.ActiveAOEs(0, f.Viewer)[0].Check(new(point.X, point.Z));
                Check(expected == f.M.Meteors[0].Contains(point),
                    $"CE33 exact polygon vertex containment layout={string.Join(';', layout)}, point={point}, "
                    + $"expected={expected}, actual={f.M.Meteors[0].Contains(point)}.");
            }
        }
    }

    private static void BehemothJuiceUpdates()
    {
        using var f = new Fixture(8);
        var source = f.Activate<BossMod.Components.GenericAOEs>("ZombieJuice");
        var p = f.Center + new Vector3(2.123f, 0, 3.257f);
        var effect = new ActorCastEvent(new(ActionType.Spell, 20196), 999, 0, 0, p, 0, 0, default);
        for (var i = 0; i < 3; ++i)
        {
            source.OnEventCast(f.W.Actors.Find(2)!, effect); f.M.Effect(2, 20196, 999, p);
        }
        var obj = f.Add(30, 0x1E972C, p + Vector3.UnitX * 2);
        obj.EventState = 7;
        f.Set(30, a => a with { EventState = 7, Dead = true, Targetable = false }); f.Sync();
        source.Update(); CompareMetalFoxBits(f, source, "ZombieJuice", "state7 cannot consume");
        obj.EventState = 0; obj.IsDead = true;
        f.Set(30, a => a with { EventState = 0 }); f.Sync();
        source.Update(); CompareMetalFoxBits(f, source, "ZombieJuice", "dead live object at exact2 consumes one");
        Check(f.M.JuiceEvents.Count == 2, "CE33 one object consumes one prediction per Update.");
        f.Sync(); source.Update(); CompareMetalFoxBits(f, source, "ZombieJuice", "next Update consumes next");
        f.Move(30, p + Vector3.UnitX * 2.001f); source.Update();
        CompareMetalFoxBits(f, source, "ZombieJuice", "outside2 does not consume");
        f.Remove(30); source.Update(); CompareMetalFoxBits(f, source, "ZombieJuice", "Destroyed removes live zone only");
    }

    private static void BehemothBolt()
    {
        using var f = new Fixture(8);
        var source = f.Activate<BossMod.Components.RaidwideInstant>("WildBolt");
        var spell = f.Start(source, 20200, total: .301f);
        void Compare(string step)
        {
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(f.M.Damage(f.NativeViewer).Where(d => d.Type == "Raidwide")
                    .Select(d => (d.Mask, d.Activation, d.Type))), step + " fixed raidwide damage.");
            var hints = new BossComponent.GlobalHints(); source.AddGlobalHints(hints);
            Check(hints.SequenceEqual(f.M.GlobalHints()), step + " global hint.");
        }
        Compare("first Start NPC plus .9");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Compare("cancel remains");
        f.Remove(2); Compare("source Destroy remains");
        f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now); Compare("no TTL");
        f.Add(3, 0x233C, f.Center); f.Sync();
        f.Start(source, 20200, 3); Compare("next Start replaces single clock");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(20201)); f.M.Effect(1, 20201);
        Compare("arbitrary source Effect clears");
    }

    private static void BehemothJuiceCache()
    {
        using var f = new Fixture(8);
        var source = f.Activate<BossMod.Components.GenericAOEs>("ZombieJuice");
        source.Update(); f.Sync();
        f.Add(30, 0x1E972C, f.Center); f.Add(31, 0x1E972C, f.Center + Vector3.UnitX); f.Sync();
        f.Remove(30);
        f.Add(32, 0x1E972C, f.Center + Vector3.UnitZ); f.Sync();
        CompareMetalFoxBits(f, source, "ZombieJuice", "first query after destroyed dictionary slot reuse");
        f.M.Effect(2, 20196, 999);
        Check(f.M.JuiceEvents.Count == 0 && f.M.MissingJuicePositionCount == 1
            && f.M.GlobalHints().Single().Contains("缺少目标位置"), "CE33 unknown position diagnostic cannot become zero.");
        f.M.Effect(2, 20196, 999, Vector3.Zero);
        Check(f.M.JuiceEvents.Single().Origin == Vector3.Zero, "CE33 explicitly known zero remains real input.");
        f.M.Effect(2, 20196, 100);
        Check(f.M.JuiceEvents.Last().Origin == f.NativeViewer.Actor.Position,
            "CE33 valid MainTarget wins even without packet point.");
    }

    private static void CompareHorn(Fixture f, BossMod.Components.GenericBaitAway source, string step)
    {
        var expected = source.ActiveBaits;
        var actual = f.M.Horns.Where(b => !b.Source.Actor.Dead).ToArray();
        Check(expected.Count == actual.Length, step + " bait count.");
        for (var i = 0; i < expected.Count; ++i)
        {
            var e = expected[i]; var a = BozjaEncounter.HornView(actual[i]);
            Check(e.Source.InstanceID == a.Source.Actor.Id && e.Target.InstanceID == a.Target!.Actor.Id
                && e.Rotation.Rad == a.Rotation && source.BaitOrigin(ref e).ToVec3() == a.Origin
                && e.Activation == a.Activation, step + " identity dynamic direction fixed clock.");
        }
        foreach (var id in new ulong[] { 100, 101 })
        {
            var viewer = f.W.Actors.Find(id)!; var native = f.M.Actors[id];
            var hints = new BossComponent.TextHints(); source.AddHints(0, viewer, hints);
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(native)
                .Where(h => h.Text.Contains("bait", StringComparison.OrdinalIgnoreCase)).Select(h => (h.Text, h.Risky))),
                step + " personal bait hints.");
            var ai = new AIHints(); source.AddAIHints(0, viewer, default, ai);
            CompareMetalFoxZones(f, ai, f.M.Navigation(native).Where(z => z.Component == "WildHorn").ToArray(), step);
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(f.M.Damage(native).Where(d => d.Type == "Tankbuster")
                    .Select(d => (d.Mask, d.Activation, d.Type))), step + " aggregate damage.");
        }
        var globals = new BossComponent.GlobalHints(); source.AddGlobalHints(globals);
        Check(globals.SequenceEqual(f.M.GlobalHints()), step + " global cleave.");
    }

    private static void BehemothHorn()
    {
        using var f = new Fixture(8);
        var source = f.Activate<BossMod.Components.GenericBaitAway>("WildHorn");
        var spell = f.Start(source, 20193);
        CompareHorn(f, source, "Start");
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareHorn(f, source, "duplicate and Finish retained");
        f.Move(100, f.Center + new Vector3(1, 0, 10)); CompareHorn(f, source, "target moves direction tracks");
        var original = f.M.Horns[0].Target;
        f.Remove(100); f.Add(100, 0, f.Center + new Vector3(-9, 0, -5), ActorType.Player); f.Sync();
        Check(!ReferenceEquals(original, f.NativeViewer), "CE33 original target generation retained.");
        CompareHorn(f, source, "destroyed target original reference no retarget");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(20193)); f.M.Effect(1, 20193);
        CompareHorn(f, source, "other source does not consume");
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(20193)); f.M.Effect(2, 20193);
        CompareHorn(f, source, "same source ID consumes only first");
    }

    private static void BehemothHornUpdate()
    {
        foreach (var targetDead in new[] { false, true })
        {
            using var f = new Fixture(8);
            var source = f.Activate<BossMod.Components.GenericBaitAway>("WildHorn");
            f.Start(source, 20193);
            if (targetDead)
            {
                f.W.Actors.Find(100)!.IsDead = true;
                f.Set(100, a => a with { Dead = true }); f.Sync();
            }
            else f.Remove(2);
            source.Update(); CompareHorn(f, source, "Update removes " + targetDead);
            Check(f.M.Horns.Count == 0, "CE33 EndsOnCastEvent Update cleanup.");
        }
    }

    private static void BehemothHornClock()
    {
        using var f = new Fixture(8);
        var source = f.Activate<BossMod.Components.GenericBaitAway>("WildHorn");
        f.Start(source, 20193, total: 1);
        f.Add(3, 0x233C, f.Center + new Vector3(-10, 0, 0)); f.Sync();
        f.Start(source, 20193, 3, total: 9, target: 101);
        f.W.Actors.Find(2)!.IsDead = true;
        f.Set(2, a => a with { Dead = true }); f.Sync(false);
        CompareHorn(f, source, "before Update dead head active second mask original first clock");
        var expectedClock = f.M.Horns[0].Activation;
        Check(f.M.Damage(f.NativeViewer).Single().Activation == expectedClock, "CE33 damage clock remains raw first bait.");
        source.Update(); f.Sync(); CompareHorn(f, source, "Update permanently removes dead source");
        f.W.Actors.Find(2)!.IsDead = false;
        f.Set(2, a => a with { Dead = false }); f.Sync();
        CompareHorn(f, source, "revived source does not restore removed bait");
        f.W.Execute(new ActorState.OpCastInfo(3, null));
        f.Set(3, a => a with { Cast = null }); f.Sync();
        f.Start(source, 20193, 3, target: 999);
        CompareHorn(f, source, "missing target no fallback");
        f.W.Execute(new ActorState.OpCastInfo(3, null));
        f.Set(3, a => a with { Cast = null }); f.Sync();
        f.Start(source, 20193, 3, target: 3);
        CompareHorn(f, source, "self target uses source rotation no own navigation");
    }

    private static void BehemothThunderbolt()
    {
        using var f = new Fixture(8);
        AddCorpse(f, 30, f.Center + new Vector3(10, 0, 0));
        AddCorpse(f, 31, f.Center + new Vector3(11, 0, 0));
        AddCorpse(f, 32, f.Center + new Vector3(15, 0, 10));
        var source = f.Activate<BossMod.Components.GenericAOEs>("Thunderbolt");
        var spell = f.Start(source, 20196);
        CompareMetalFoxBits(f, source, "Thunderbolt", "no Start forecast");
        f.W.Execute(new ActorState.OpCastInfo(2, null));
        f.Set(2, a => a with { Cast = null }); f.Sync();
        Check(f.M.Visible().Count(a => a.Cast.Rule.Kind == BozjaRuleKind.Thunderbolt) == 1, "CE33 circle first only.");
        CompareMetalFoxBits(f, source, "Thunderbolt", "circle Finish first corpse");
        f.Start(source, 20198);
        f.W.Execute(new ActorState.OpCastInfo(2, null));
        f.Set(2, a => a with { Cast = null }); f.Sync();
        CompareMetalFoxBits(f, source, "Thunderbolt", "rectangle Finish all matching");
        f.Move(30, f.Center + new Vector3(-10, 0, 0)); f.Remove(31);
        CompareMetalFoxBits(f, source, "Thunderbolt", "locked original corpse geometry");
        f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CompareMetalFoxBits(f, source, "Thunderbolt", "no TTL");
        var clear = new ActorCastInfo { Action = new(ActionType.Spell, 20197) };
        source.OnCastFinished(f.W.Actors.Find(1)!, clear);
        f.M.Finish(f.M.Actors[1], new(20197, 0, default, 0, f.Now, f.Now, false, false));
        CompareMetalFoxBits(f, source, "Thunderbolt", "any corpse Finish clears all");
    }

    private static void BehemothCold()
    {
        using var f = new Fixture(8);
        AddCorpse(f, 30, f.Center);
        var actor = f.W.Actors.Find(30)!;
        actor.CastInfo = new() { Action = new(ActionType.Spell, 20197), TotalTime = 6, Location = f.Center, Rotation = new(.4f) };
        f.Set(30, a => a with { Cast = new(20197, 0, f.Center, .4f, START, START.AddSeconds(6.3f), false, false)
            { TotalTime = 6, IsSpell = true } });
        f.M.Clear(); f.Sync();
        var source = f.Activate<BossMod.Components.GenericAOEs>("Thunderbolt");
        Check(f.M.Visible().All(a => a.Cast.Rule.Kind != BozjaRuleKind.Thunderbolt), "CE33 cold current cast no Finish history.");
        CompareMetalFoxBits(f, source, "Thunderbolt", "cold current Thunderbolt cast");
    }

    private static void BehemothMeteorRow()
    {
        using var f = new Fixture(8);
        var origin = f.Center + new Vector3(0, 0, 1000);
        AddCorpse(f, 30, origin);
        f.Move(2, origin);
        var source = f.Activate<BossMod.Components.CastLineOfSightAOE>("BlazingMeteor");
        f.Start(source, 20194);
        var point = new Vector3(238.3190460205078f, 0, 1124.0625f);
        var expected = source.ActiveAOEs(0, f.Viewer)[0].Check(new(point.X, point.Z));
        Check(!expected, "CE33 real source row hull rejects rounding point.");
        Check(f.M.Meteors.Single().Contains(point) == expected, "CE33 translated row hull containment.");
    }

    private static void BehemothFeed()
    {
        Initialize();
        foreach (var reverse in new[] { false, true })
        {
            using var session = new MechanicSession("CE33-real-cold-and-batch");
            var feed = new MechanicWorldFeed(session);
            var region = new BozjaRegion();
            var center = BozjaCatalog.Definitions[8].Center;
            ReplayCast Spell(uint action, Vector3 loc, ulong target = 100)
                => new(action, target, loc, .4f, START, START.AddSeconds(4.3f), false, true)
                    { TotalTime = 4, IsSpell = true };
            ReplayActorState Npc(ulong id, uint oid, Vector3 p, ReplayCast? spell = null)
                => new() { Id = id, Oid = oid, Kind = 2, SubKind = 5, Position = p, Hp = 1000,
                    Targetable = true, Cast = spell, InCombat = id == 1, HitboxRadius = oid == 0x2DCC ? 4.35f : .5f };
            ReplayActorState[] actors =
            [
                Npc(1, 0x2DCB, center + Vector3.UnitX * 10, Spell(20193, center)),
                Npc(20, 0x233C, center, Spell(20196, center)),
                Npc(21, 0x233C, center, Spell(20194, center + Vector3.UnitX * 10)),
                Npc(22, 0x233C, center, Spell(20200, center)),
                Npc(23, 0x233C, center, Spell(20198, center)),
                Npc(30, 0x2DCC, center + Vector3.UnitX),
                Npc(31, 0x2DCC, center + Vector3.UnitX * 2),
                new() { Id = 100, Kind = 1, Position = center, Hp = 100, ClassJob = 19 },
                new() { Id = 101, Kind = 1, Position = center + Vector3.UnitZ * 15, Hp = 100, ClassJob = 19 },
            ];
            if (reverse) (actors[5], actors[6]) = (actors[6], actors[5]);
            var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
            void Frame(DateTime now, params MechanicEventActionEffect[] events)
            {
                session.BeginFrame(now);
                var input = actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray();
                feed.ApplyFrame(new(920, 735, 100, 0, [new(0, 100, 100, ""), new(1, 101, 101, "")], [], input));
                foreach (var e in events) feed.Enqueue(e);
                feed.Flush();
                session.Update();
                region.Synchronize(actors, [new(0, "", 100), new(1, "", 101)], 100, now, true, () =>
                {
                    foreach (var e in events) region.Effect(e.CasterId, e.ActionId, e.MainTargetId, e.TargetPos);
                });
                var module = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x2DCB);
                Check(module.Components.Count() == 6, "CE33 actual state machine six active components.");
                var model = region.Encounters[8];
                var viewer = session.World.Actors.Find(100)!;
                var native = model.Actors[100];
                foreach (var component in module.Components.OfType<BossMod.Components.GenericAOEs>())
                {
                    var name = component.GetType().Name;
                    var expected = component.ActiveAOEs(0, viewer).ToArray();
                    var actual = model.Visible(native).Where(a => a.Cast.Rule.Component == name).ToArray();
                    Check(expected.Length == actual.Length, "CE33 actual Feed " + name + " count.");
                    for (var i = 0; i < expected.Length; ++i)
                    {
                        var e = expected[i]; var a = actual[i];
                        Check(e.Origin.ToVec3() == a.Cast.Origin && e.Rotation.Rad == a.Cast.Rotation
                            && e.Activation == a.Cast.Activation && e.Risky == a.Risky,
                            "CE33 actual Feed " + name + " exact fields.");
                        foreach (var offset in new[] { Vector3.Zero, Vector3.UnitX * 8, Vector3.UnitZ * 20, -Vector3.UnitX * 15 })
                        {
                            var p = center + offset;
                            Check(e.Check(new(p.X, p.Z)) == a.Cast.Contains(p), "CE33 actual Feed raw shape.");
                        }
                    }
                }
                var sourceHorn = module.Components.OfType<BossMod.Components.GenericBaitAway>().Single();
                Check(sourceHorn.CurrentBaits.Count == model.Horns.Count, "CE33 actual Feed cold original-target bait.");
                var sourceDamage = new AIHints();
                foreach (var component in module.Components) component.AddAIHints(0, viewer, default, sourceDamage);
                Check(sourceDamage.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                    .SequenceEqual(model.Damage(native).Select(d => (d.Mask, d.Activation, d.Type))),
                    "CE33 actual full component fixed clocks masks and order.");
            }
            Frame(START);
            var point = center + Vector3.UnitX * 20;
            actors = actors.Select(a => a.Id is 20 or 23 ? a with { Cast = null } : a)
                .Append(Npc(40, 0x1E972C, point)).ToArray();
            var e = new MechanicEventActionEffect(20, 1, 20196, 999, 0, point, 0, 0, 0, []);
            Frame(START.AddSeconds(1), e, e);
            Check(region.Encounters[8].JuiceEvents.Count == 1, "CE33 actual two complete packets one source consumes once.");
            Frame(START.AddSeconds(2));
            Check(region.Encounters[8].JuiceEvents.Count == 0, "CE33 next actual Update consumes second prediction.");
        }
    }
}
