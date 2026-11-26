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
    private static void RunDahu(Action<string, Action> run)
    {
        run("Bozja native DRN2 Dahu client territory NameID all source actions exact notched polygon", DahuClient);
        run("Bozja native DRN2 Dahu five standard AOEs ordinary watched Finish no Effect TTL", DahuStandard);
        run("Bozja native DRN2 Dahu two Charge widths original delta quantized origin and zero length", DahuCharges);
        run("Bozja native DRN2 Dahu HotCharge query parity actual Effect and weak goal old bridge rejection", DahuChargeQueries);
        run("Bozja native DRN2 Dahu Shockwave FIFO wrong source Finish sticky color and Risky promotion", DahuShockwave);
        run("Bozja native DRN2 Dahu rotation icon last callback origin five Finish cache and dynamic boss near", () => DahuRotation(false));
        run("Bozja native DRN2 Dahu rotation visual last callback origin five Finish cache", () => DahuRotation(true));
        run("Bozja native DRN2 Dahu zero rotation unknown icon empty Finish and duplicate sequence first index", DahuRotationEdges);
        run("Bozja native DRN2 Dahu FeralHowl wall hitbox original source chain claws union unsafe", DahuHowl);
        run("Bozja native DRN2 Dahu FeralHowl collinear boundary both directions large hitbox multiple sources", DahuWallEdges);
        run("Bozja native DRN2 Dahu claws multi cone direct split versus invalid member whole union sampling", DahuClawUnion);
        run("Bozja native DRN2 Dahu HeatBreath original source target dynamic tankbuster Effect first source and dead", DahuHeat);
        run("Bozja native DRN2 Dahu HeatBreath destroyed target same ID generation and dead before versus after Update", DahuHeatGenerations);
        run("Bozja native DRN2 Dahu actual Feed eleven components cold Finish and no clock reset", DahuFeed);
    }

    private static void DahuClient()
    {
        using var f = new Fixture(18);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(760)?.TerritoryType.RowId == 936
            && Service.LuminaRow<Lumina.Excel.Sheets.TerritoryType>(936)?.ContentFinderCondition.RowId == 760
            && Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(9751)?.Singular.ToString() == f.M.Definition.Name
            && f.M.Definition.Rules.Length == 11, "DRN2 exact client data");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.DelubrumReginae.DRN2Dahu.AID>())
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "DRN2 real source action " + action);
        Check(f.M.Center == f.Source.Arena.Center.ToVec3(), "DRN2 asymmetric notches recentered exactly");
        for (var x = -31f; x <= 31; x += .37f)
            for (var z = -31f; z <= 31; z += .43f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), "DRN2 exact exported arena");
            }
        f.Move(100, f.Center + Vector3.UnitX * 100);
        var far = new BozjaEncounter(f.M.Definition);
        far.Synchronize(f.Actors, f.Party.ToArray(), 100, f.Now);
        Check(far.Active, "DRN2 default pull has no artificial R30 restriction");
        var primaryIndex = f.Actors.FindIndex(a => a.Id == 1);
        f.Actors[primaryIndex] = f.Actors[primaryIndex] with { Targetable = false };
        var hidden = new BozjaEncounter(f.M.Definition);
        hidden.Synchronize(f.Actors, f.Party.ToArray(), 100, f.Now);
        Check(!hidden.Active, "DRN2 targetable pull required");
    }

    private static void DahuStandard()
    {
        foreach (var rule in BozjaCatalog.Definitions[18].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
        {
            using var f = new Fixture(18);
            var source = f.Activate<GenericAOEs>(rule.Component);
            var spell = f.Start(source, rule.Actions[0]);
            source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            CompareMetalFoxBits(f, source, rule.Component, "DRN2 duplicate " + rule.Component);
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(rule.Actions[0])); f.M.Effect(1, rule.Actions[0]);
            f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareMetalFoxBits(f, source, rule.Component, "DRN2 no TTL " + rule.Component);
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, rule.Component, "DRN2 watched Finish " + rule.Component);
        }
    }

    private static void DahuCharges()
    {
        foreach (var component in new[] { "HotCharge", "HeadDown" })
        {
            using var f = new Fixture(18);
            var source = f.Activate<ChargeAOEs>(component);
            var action = f.M.Definition.Rules.Single(r => r.Component == component).Actions[0];
            var spell = f.Start(source, action);
            CompareMetalFoxBits(f, source, component, "DRN2 zero-length retained " + component);
            Check(source.Casters.Count == 1 && f.M.Casts.Single().Shape is MasterPlayer.Spatial.SafeZone.RayRectangleTemplate { Length: 0 },
                "DRN2 raw degenerate not dropped");
            source.Casters.Clear(); f.M.Casts.Clear();
            var actor = f.W.Actors.Find(2)!;
            actor.PosRot = new(f.Center + new Vector3(10.00031f, 0, .0023f), 2);
            f.Set(2, a => a with { Position = actor.Position.ToVec3(), Cast = null }); f.Sync();
            spell = new() { Action = new(ActionType.Spell, action), TotalTime = 7,
                Rotation = new(-1), Location = f.Center + new Vector3(-13.471f, 0, 8.219f) };
            actor.CastInfo = spell;
            source.OnCastStarted(actor, spell);
            f.Set(2, a => a with { Cast = new(action, 0, spell.Location, -1, f.Now, f.Now.AddSeconds(7.3f), false, false)
                { TotalTime = 7, IsSpell = true } }); f.Sync();
            CompareMetalFoxBits(f, source, component, "DRN2 original delta and true width " + component);
            source.OnCastStarted(actor, spell); f.M.Start(f.M.Actors[2]);
            source.OnCastFinished(actor, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, component, "DRN2 first same source Finish " + component);
        }
    }

    private static void DahuChargeQueries()
    {
        using var f = new Fixture(18);
        var source = f.Activate<ChargeAOEs>("HotCharge");
        for (var i = 0; i < 6; ++i)
        {
            if (i == 2)
            {
                source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(22372));
                f.M.Effect(2, 22372);
            }
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            var goal = f.M.DahuChargeGoal();
            Check(source.NumCasts == f.M.NumDahuCharges && ai.GoalZones.Count == (goal == null ? 0 : 1),
                "DRN2 query and Effect share exact counter");
            if (goal is { } g)
                foreach (var p in new[] { f.Center, f.Center + Vector3.UnitX * 6, f.Center + Vector3.UnitX * 6.001f })
                    Check(ai.GoalZones[0](new(p.X, p.Z)) == (BozjaEncounter.DistanceSquared(p, g.Origin) <= g.Radius * g.Radius ? g.Weight : 0),
                        "DRN2 original weak score");
            Check(MechanicHintsConvert.ConvertGoalZones(ai.GoalZones, f.Source.Arena.Center, 29.5f, 0).Count == 0,
                "DRN2 weight5 below real host threshold100");
        }
    }

    private static void DahuShockwave()
    {
        using var f = new Fixture(18);
        var source = f.Activate<GenericAOEs>("Shockwave");
        f.Start(source, 22368);
        CompareMetalFoxBits(f, source, "Shockwave", "DRN2 shock first query mutates head");
        var original = source.ActiveAOEs(0, f.Viewer).ToArray();
        Check(original[0].Color == Colors.Danger && original[1].Color == 0 && !original[1].Risky, "DRN2 shock raw colors");
        var second = new ActorCastInfo { Action = new(ActionType.Spell, 22371), Location = new(999, 0, 999) };
        source.OnCastFinished(f.W.Actors.Find(1)!, second);
        f.M.Finish(f.M.Actors[1], new(22371, 0, new(999), 2, f.Now, f.Now, false, false));
        CompareMetalFoxBits(f, source, "Shockwave", "DRN2 arbitrary second Finish promoted zero color");
        Check(source.ActiveAOEs(0, f.Viewer)[0].Color == 0, "DRN2 tail color never reset to future");
        source.OnCastFinished(f.W.Actors.Find(1)!, second); f.M.Finish(f.M.Actors[1], new(22371, 0, default, 0, f.Now, f.Now, false, false));
        source.OnCastFinished(f.W.Actors.Find(1)!, second); f.M.Finish(f.M.Actors[1], new(22371, 0, default, 0, f.Now, f.Now, false, false));
        CompareMetalFoxBits(f, source, "Shockwave", "DRN2 empty FIFO harmless");
    }

    private static void DahuRotation(bool iconFirst)
    {
        using var f = new Fixture(18);
        var source = f.Activate<GenericRotatingAOE>("FirebreatheRotation");
        void Icon()
        {
            source.OnEventIcon(f.Viewer, 167, 999);
            f.M.Icon(100, 167, 999);
        }
        if (iconFirst) Icon();
        f.Start(source, 22361);
        if (!iconFirst) Icon();
        source.Update(); f.M.Update(f.Now);
        CompareMetalFoxBits(f, source, "FirebreatheRotation", "DRN2 rotating initial", Colors.AOE, true);
        Check(source.Sequences.Single().Origin.ToVec3() == (iconFirst
            ? DungeonQuantized(f.W.Actors.Find(2)!.Position.ToVec3()) : DungeonQuantized(f.Viewer.Position.ToVec3())),
            "DRN2 successful callback owns origin");
        f.Now = START.AddSeconds(90); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        source.Update(); f.M.Update(f.Now);
        CompareMetalFoxBits(f, source, "FirebreatheRotation", "DRN2 cache survives clock", Colors.AOE, true);
        for (var i = 0; i < 6; ++i)
        {
            var spell = new ActorCastInfo { Action = new(ActionType.Spell, 22362), Location = new(777, 0, 777), Rotation = new(2) };
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(22362)); f.M.Effect(1, 22362);
            source.OnCastFinished(f.W.Actors.Find(1)!, spell);
            f.M.Finish(f.M.Actors[1], new(22362, 0, spell.Location, 2, f.Now, f.Now, false, false));
            source.Update(); f.M.Update(f.Now);
            Check(source.NumCasts == f.M.NumDahuRotations && source.NumCasts == i + 1, "DRN2 empty Finish still counter");
            CompareMetalFoxBits(f, source, "FirebreatheRotation", "DRN2 five plus empty Finish " + i, Colors.AOE, true);
        }
    }

    private static Vector3 DungeonQuantized(Vector3 p) => MasterPlayer.Mechanics.Dungeons.DungeonEncounter.QuantizedDungeonPosition(p);

    private static void DahuRotationEdges()
    {
        using var f = new Fixture(18);
        var source = f.Activate<GenericRotatingAOE>("FirebreatheRotation");
        var actor = f.W.Actors.Find(2)!;
        var zero = new ActorCastInfo { Action = new(ActionType.Spell, 22361), TotalTime = 5, Rotation = default, Location = f.Center };
        actor.CastInfo = zero;
        f.Set(2, a => a with { Cast = new(22361, 0, f.Center, 0, f.Now, f.Now.AddSeconds(5.3f), false, false) { TotalTime = 5 } });
        source.OnCastStarted(actor, zero); f.Sync();
        source.OnEventIcon(f.Viewer, 167, 0); f.M.Icon(100, 167, 0);
        Check(source.Sequences.Count == 0 && !f.M.Visible().Any(), "DRN2 zero rotation remains missing");
        source.OnEventIcon(f.Viewer, 999, 0); f.M.Icon(100, 999, 0);
        var spell = zero; spell.Rotation = new(.4f); actor.CastInfo = spell;
        source.OnCastStarted(actor, spell);
        f.Set(2, a => a with { Cast = a.Cast! with { Rotation = .4f } }); f.Sync();
        Check(source.Sequences.Count == 0 && !f.M.Visible().Any(), "DRN2 unknown icon clears prior direction");
        source.OnEventIcon(f.Viewer, 168, 0); f.M.Icon(100, 168, 0);
        source.OnCastStarted(actor, spell); f.M.Start(f.M.Actors[2]);
        source.OnEventIcon(actor, 167, 0); f.M.Icon(2, 167, 0);
        source.Update(); f.M.Update(f.Now);
        CompareMetalFoxBits(f, source, "FirebreatheRotation", "DRN2 duplicate sequences ordered", Colors.AOE, true);
        Check(source.Sequences.Count == 2, "DRN2 no dedup");
    }

    private static void DahuHowl()
    {
        using var f = new Fixture(18);
        var claw = f.Activate<GenericAOEs>("HuntersClaw");
        var source = f.Activate<SimpleKnockbacks>("FeralHowl");
        f.Set(100, a => a with { HitboxRadius = f.Viewer.HitboxRadius }); f.Sync();
        f.Move(2, f.Center + Vector3.UnitZ * 25); f.Start(claw, 22359);
        f.Start(source, 22357, 1);
        foreach (var offset in new[] { new Vector3(1, 0, 3), new Vector3(10, 0, 1), new Vector3(-3, 0, -12), Vector3.Zero })
        {
            f.Move(100, f.Center + offset);
            var expected = source.CalculateMovements(0, f.Viewer);
            var actual = f.M.DahuMovements(f.NativeViewer).ToArray();
            Check(expected.Count == actual.Length, "DRN2 wall movement count");
            for (var i = 0; i < actual.Length; ++i)
                Check(expected[i].from.ToVec3() == actual[i].From
                    && Vector3.Distance(expected[i].to.ToVec3(), actual[i].To) < .00002f, "DRN2 source polygon wall landing");
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).Where(z => z.Component == "FeralHowl").ToArray(), "DRN2 one raw SDUnion");
            var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
            Check(hints.Select(h => h.Item1).SequenceEqual(f.M.Hints(f.NativeViewer).Where(h => h.Text == "About to be knocked into danger!").Select(h => h.Text)),
                "DRN2 claw DestinationUnsafe");
        }
    }

    private static void DahuHeat()
    {
        using var f = new Fixture(18);
        var source = f.Activate<BaitAwayCast>("HeatBreath");
        var spell = f.Start(source, 22374);
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        void Compare(string step)
        {
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).Where(z => z.Component == "HeatBreath").ToArray(), step);
            Check(source.CurrentBaits.Select(b => (b.Source.InstanceID, b.Target.InstanceID, b.Activation))
                .SequenceEqual(f.M.DahuBaits.Select(b => (b.Source.Actor.Id, b.Target.Actor.Id, b.Activation))), step + " raw baits");
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(f.M.Damage(f.NativeViewer).Select(d => (d.Mask, d.Activation, d.Type))), step + " damage");
            var global = new BossComponent.GlobalHints(); source.AddGlobalHints(global);
            Check(global.SequenceEqual(f.M.GlobalHints()), step + " tankbuster global");
        }
        Compare("DRN2 heat duplicate");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Compare("DRN2 heat Finish ignored");
        f.Move(2, f.Center - Vector3.UnitX * 10); f.Move(100, f.Center + Vector3.UnitX * 3);
        Compare("DRN2 heat current geometry");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(22374)); f.M.Effect(1, 22374);
        Compare("DRN2 heat wrong source does not consume");
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(22374)); f.M.Effect(2, 22374);
        Compare("DRN2 heat first source");
        f.Viewer.IsDead = true; f.Set(100, a => a with { Dead = true }); f.Sync();
        source.Update();
        Compare("DRN2 heat target death Update cleans");
    }

    private static void DahuWallEdges()
    {
        foreach (var sign in new[] { -1f, 1f })
            foreach (var hitbox in new[] { .5f, 3f })
            {
                using var f = new Fixture(18);
                f.Activate<GenericAOEs>("HuntersClaw");
                var source = f.Activate<SimpleKnockbacks>("FeralHowl");
                var position = f.Center + new Vector3(0, 0, 28.502502f);
                f.Viewer.HitboxRadius = hitbox;
                f.Set(100, a => a with { HitboxRadius = hitbox });
                f.Move(100, position);
                f.Move(2, position - Vector3.UnitX * sign);
                f.Start(source, 22357);
                Compare("on horizontal wall");
                f.Add(3, 0x233C, f.Center - Vector3.UnitZ * 10);
                f.Sync();
                f.Start(source, 22357, 3);
                Compare("two original caster chain");
                f.Move(100, f.Center + new Vector3(3, 0, 5));
                Compare("large hitbox inside");
                void Compare(string step)
                {
                    var expected = source.CalculateMovements(0, f.Viewer);
                    var actual = f.M.DahuMovements(f.NativeViewer).ToArray();
                    Check(expected.Count == actual.Length, step + " count");
                    for (var i = 0; i < actual.Length; ++i)
                        Check(Vector3.Distance(expected[i].from.ToVec3(), actual[i].From) < .00002f
                            && Vector3.Distance(expected[i].to.ToVec3(), actual[i].To) < .00002f,
                            step + $" first source wall movement {i}: {expected[i].to} versus {actual[i].To}");
                }
            }
    }

    private static void DahuHeatGenerations()
    {
        using var f = new Fixture(18);
        var source = f.Activate<BaitAwayCast>("HeatBreath");
        f.Start(source, 22374);
        var original = f.Viewer;
        var nativeOriginal = f.NativeViewer;
        void Compare(string step)
        {
            var ai = new AIHints();
            source.AddAIHints(0, f.Viewer, default, ai);
            CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).Where(z => z.Component == "HeatBreath").ToArray(), step);
            var hints = new BossComponent.TextHints();
            source.AddHints(0, f.Viewer, hints);
            Check(hints.Select(h => (h.Item1, h.Item2))
                .SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))), step + " ID-based personal hints");
            Check(source.CurrentBaits.Count == f.M.DahuBaits.Count, step + " raw list");
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(f.M.Damage(f.NativeViewer).Select(d => (d.Mask, d.Activation, d.Type))), step + " original damage");
        }
        f.Remove(100);
        source.Update();
        Check(source.CurrentBaits.Count == 1 && f.M.DahuBaits.Count == 1
            && original.IsDestroyed && nativeOriginal.Destroyed, "DRN2 destroyed target alone retained after Update");
        f.Add(100, 0, f.Center + Vector3.UnitX * 7, ActorType.Player);
        f.Sync();
        Check(ReferenceEquals(source.CurrentBaits[0].Target, original)
            && ReferenceEquals(f.M.DahuBaits[0].Target, nativeOriginal)
            && !ReferenceEquals(f.NativeViewer, nativeOriginal), "DRN2 recreated target does not replace captured reference");
        Compare("DRN2 old destroyed target and new same-ID viewer");
        original.IsDead = true;
        nativeOriginal.Actor = nativeOriginal.Actor with { Dead = true };
        Compare("DRN2 dead original target remains active before Update");
        source.Update();
        f.M.Update(f.Now);
        Compare("DRN2 original target death removes only on Update");
        Check(f.M.DahuBaits.Count == 0, "DRN2 removed dead original target");
        f.Set(2, a => a with { Cast = null });
        f.Sync();
        f.Start(source, 22374);
        f.W.Actors.Find(2)!.IsDead = true;
        f.Set(2, a => a with { Dead = true });
        f.Sync(false);
        Compare("DRN2 dead source invisible but raw bait before Update");
        source.Update();
        f.M.Update(f.Now);
        Compare("DRN2 dead source raw bait removed after Update");
    }

    private static void DahuClawUnion()
    {
        using var f = new Fixture(18);
        var claw = f.Activate<GenericAOEs>("HuntersClaw");
        var source = f.Activate<SimpleKnockbacks>("FeralHowl");
        f.Set(100, a => a with { HitboxRadius = .5f }); f.Sync();
        f.Move(2, f.Center + Vector3.UnitZ * 25); f.Start(claw, 22359);
        f.Add(3, 0x233C, f.Center - Vector3.UnitX * 18); f.Sync(); f.Start(claw, 22359, 3);
        f.Start(source, 22357, 1);
        void Compare(string step)
        {
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            var zones = f.M.Navigation(f.NativeViewer).Where(z => z.Component == "FeralHowl").ToArray();
            Check(ai.ForbiddenZones.Count == 1 && zones.Length == 1, step + " preserves single raw union");
            for (var x = -30f; x <= 30; x += .57f)
                for (var z = -30f; z <= 30; z += .71f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(ai.ForbiddenZones[0].Item1.Distance(new(p.X, p.Z)) == zones[0].Distance(p.X, p.Z),
                        step + " exact raw union ignores NaN members");
                }
            CompareMetalFoxZones(f, ai, zones, step);
        }
        Compare("DRN2 valid cones convert separately");
        f.Add(4, 0x233C, f.Center + Vector3.UnitX * 4); f.Sync(); f.Start(claw, 22359, 4);
        Compare("DRN2 nearby unclamped Asin forces whole union sample");
        f.Add(5, 0x233C, f.Center); f.Sync(); f.Start(claw, 22359, 5);
        Compare("DRN2 zero-distance NaN not contagious");
    }

    private static void DahuFeed()
    {
        Initialize();
        using var session = new MechanicSession("DRN2-source-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(936);
        var model = region.Encounters.Single(m => m.Definition.Key == "DRN2Dahu");
        var center = model.Center;
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x30A6, Kind = 2, SubKind = 5, Position = center, Targetable = true, InCombat = true, Hp = 1000 },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000, HitboxRadius = .5f },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitZ * 20, Hp = 1000, HitboxRadius = .5f },
        };
        foreach (var (rule, i) in model.Definition.Rules.Select((r, i) => (r, i)))
            actors.Add(new() { Id = (ulong)(20 + i), Oid = 0x233C, Kind = 2, SubKind = 5, Position = center + Vector3.UnitX * 5,
                Hp = 1000, Cast = new(rule.Actions[0], 100, center + Vector3.UnitX * 5, .4f, START, START.AddSeconds(4.3f), false, false)
                    { TotalTime = 4, IsSpell = true } });
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Frame(double seconds, bool apply)
        {
            var now = START.AddSeconds(seconds); session.BeginFrame(now);
            if (apply)
            {
                feed.ApplyFrame(new(936, 760, 100, 0, [new(0, 100, 100, ""), new(1, 101, 101, "")], [],
                    actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
                feed.Flush();
            }
            session.Update();
            if (apply) region.Synchronize(actors.ToArray(), [new(0, "", 100), new(1, "", 101)], 100, now, true);
            else region.Update(now);
            var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x30A6);
            Check(source.Components.Count == 11 && model.Active, "DRN2 eleven actual activated components");
            var viewer = session.World.Actors.Find(100)!;
            var expected = source.Components.OfType<GenericAOEs>().SelectMany(c => c.ActiveAOEs(0, viewer).ToArray()).ToArray();
            var actual = model.Visible(model.Actors[100]).ToArray();
            Check(expected.Select(e => (e.Origin.ToVec3(), e.Rotation.Rad, e.Activation, e.Risky, e.Color))
                .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky, a.Danger ? Colors.Danger : 0))),
                "DRN2 full Feed raw ordered shapes no missing historical icons");
            var ai = new AIHints();
            foreach (var c in source.Components) c.AddAIHints(0, viewer, default, ai);
            _ = model.DahuChargeGoal();
            ai.Normalize();
            var zones = model.NavigationForHost(model.Actors[100]);
            Check(ai.ForbiddenZones.Select(z => z.Item2).SequenceEqual(zones.Select(z => z.Activation)), "DRN2 full Feed normalized clocks");
            for (var i = 0; i < zones.Length; ++i)
            {
                var e = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, center, 29.5f, out _);
                var a = zones[i].Convert(center, 29.5f).ToArray();
                Check(e.Count == a.Length, "DRN2 full Feed conversion count");
                for (var x = -29f; x < 29; x += 1.31f)
                    for (var z = -29f; z < 29; z += 1.67f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        Check(e.Any(s => s.Template.Resolve(s.Position, s.Rotation, null).Contains(p, 0))
                            == a.Any(s => s.Contains(p, 0)), "DRN2 full Feed converted geometry");
                    }
            }
            Check(source.CalculateGlobalHints().SequenceEqual(model.GlobalHints())
                && source.CalculateHintsForRaidMember(0, viewer).Select(h => (h.Item1, h.Item2))
                    .SequenceEqual(model.Hints(model.Actors[100]).Select(h => (h.Text, h.Risky))), "DRN2 full Feed both hint streams");
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(model.DamageForHost(model.Actors[100]).Select(d => (d.Mask, d.Activation, d.Type))), "DRN2 full Feed damage");
        }
        Frame(0, true); Frame(.1, false);
        for (var i = 3; i < actors.Count; ++i) actors[i] = actors[i] with { Cast = null };
        Frame(1, true); Frame(50, false);
    }
}
