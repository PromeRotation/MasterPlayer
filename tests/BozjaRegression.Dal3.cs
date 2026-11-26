using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;
using Dal3Source = BossMod.Shadowbringers.Foray.TheDalriada.DAL3SaunionDawon.DAL3SaunionDawon;

internal static partial class BozjaRegression
{
    private static void RunDal3(Action<string, Action> run)
    {
        run("Bozja native DAL3 client seventeen components square exact spiral table", Dal3Client);
        run("Bozja native DAL3 dual boss phase cold order kept instances original generations no HP inference", Dal3Phases);
        foreach (var rule in BozjaCatalog.Definitions[31].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
            run("Bozja native DAL3 standard " + rule.Component, () => Dal3Standard(rule));
        run("Bozja native DAL3 mobile eight offsets overwrite real Effect any source no Finish TTL", Dal3Mobile);
        run("Bozja native DAL3 plumes cold created clock lock duplicates Finish clears all", Dal3Plumes);
        run("Bozja native DAL3 Obey hot animation only queued first clock FIFO original Dawon", Dal3Obey);
        run("Bozja native DAL3 Spiral eight tables six match axes same clock count color old queue", Dal3Spiral);
        run("Bozja native DAL3 HighPowered raw original targets circle not cone Finish dynamic dead mask", Dal3Bait);
        run("Bozja native DAL3 HighPowered true Party slot ordering invalid slots excluded", Dal3BaitOrder);
        run("Bozja native DAL3 OneMind true slots59 Pet cold hot Lose XOR names no refcount", Dal3Mind);
        run("Bozja native DAL3 Touchdown square exact wall immunity strict multi caster lines no navigation", Dal3Knockback);
        run("Bozja native DAL3 stack spread original target no invented predictions", Dal3Targets);
        run("Bozja native DAL3 two dynamic raw NPC damage clocks original caster", Dal3Damage);
        run("Bozja native DAL3 real Feed both phases seventeen components cold slots exact normalized outputs", Dal3Feed);
        run("Bozja native DAL3 real Feed lazy empty Dawon list hot Created order original binding", Dal3DawonOrder);
    }

    private static void Dal3Client()
    {
        using var f = new Fixture(31);
        Check(f.M.Definition.Rules.Length == 17 && f.M.ActiveRules.Count() == 8, "DAL3 17/8 components");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(10192)?.Singular.ToString() == f.M.Definition.Name, "DAL3 name");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(778)?.TerritoryType.RowId == 975, "DAL3 CFC");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.TheDalriada.DAL3SaunionDawon.AID>())
            Check(data!.GetExcelSheet<Lumina.Excel.Sheets.Action>()!.TryGetRow((uint)action, out _)
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "DAL3 AID " + action);
        var patterns = (WPos[][])f.Source.GetType().Assembly.GetType(f.Source.GetType().Namespace + ".SpiralScourge")!
            .GetField("patterns", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        Check(patterns.Length == 8 && patterns.Sum(a => a.Length) == 272, "DAL3 actual source table");
        for (var i = 0; i < 8; ++i)
            Check(patterns[i].Select(p => p.ToVec3()).SequenceEqual(DalriadaSpirals.Patterns[i]), "DAL3 exported points exact " + i);
        foreach (var v in new[] { -27f, -26.5f, MathF.BitIncrement(-26.5f), 0, 26.5f, MathF.BitIncrement(26.5f), 27f })
            foreach (var z in new[] { -27f, -26.5f, 0, 26.5f, 27 })
            {
                var p = f.Center + new Vector3(v, 0, z);
                Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), "DAL3 square edge");
            }
    }

    private static void Dal3Second(Fixture f)
    {
        var dawon = f.Add(4, 0x31DE, f.Center + new Vector3(7.12345f, 0, 4.8765f), ActorType.Enemy);
        f.Sync(); f.Sync();
        ((Dal3Source)f.Source).BossDawon = dawon;
        Check(f.M.SaunionPhase == 1 && f.M.ActiveRules.Count() == 14, "DAL3 phase1 retained5 plus9");
    }

    private static void Dal3Phases()
    {
        using var f = new Fixture(31);
        f.Source.PrimaryActor.InCombat = true;
        f.Source.Update();
        Check(f.Source.Components.Select(c => c.GetType().Name).SequenceEqual(f.M.ActiveRules.Select(r => r.Component)), "DAL3 P0 actual activation order");
        var keep = f.Source.Components.Where(c => c.KeepOnPhaseChange).ToArray();
        var d = f.Add(4, 0x31DE, f.Center, ActorType.Enemy);
        f.Sync(); f.Source.Update();
        Check(f.M.SaunionPhase == 0 && f.Source.StateMachine.ActivePhaseIndex == 0, "DAL3 initial targetable caches after phase");
        f.Sync(); f.Source.Update();
        Check(f.M.SaunionPhase == 1 && f.Source.StateMachine.ActivePhaseIndex == 1
            && f.Source.Components.Select(c => c.GetType().Name).SequenceEqual(f.M.ActiveRules.Select(r => r.Component))
            && keep.SequenceEqual(f.Source.Components.Take(5)), "DAL3 P1 exact instance keep/order");
        var original = f.M.Primary;
        var oldDawon = f.M.Dawon;
        f.Remove(1); f.Source.Update();
        Check(f.M.Active && ReferenceEquals(f.M.Primary, original), "DAL3 primary destroy doesn't end live Dawon");
        f.Add(1, 0x31DD, f.Center, ActorType.Enemy); f.Sync();
        Check(ReferenceEquals(f.M.Primary, original) && !ReferenceEquals(f.M.Primary, f.M.Actors[1]), "DAL3 sameID primary no rebind");
        d.IsDead = true; f.Set(4, a => a with { Dead = true }); f.Sync(); f.Source.Update();
        Check(!f.M.Active && f.Source.StateMachine.ActiveState == null, "DAL3 both dead completes");
        Check(ReferenceEquals(f.M.Dawon, oldDawon), "DAL3 original cached Dawon retained");
        using var hp = new Fixture(31);
        hp.Set(1, a => a with { Hp = 0, Dead = true }); hp.Sync();
        Check(hp.M.Active && hp.M.SaunionPhase == 0, "DAL3 P0 Dead HP0 no invented complete");
    }

    private static void Dal3Standard(BozjaRule rule)
    {
        using var f = new Fixture(31);
        if (Array.IndexOf(f.M.Definition.Rules, rule) >= 8) Dal3Second(f);
        var source = f.Activate<GenericAOEs>(rule.Component);
        var spell = RobotStart(f, source, rule.Actions[0], rotation: .37f);
        Dal2Compare(f, source, rule.Component);
        f.Move(2, f.Center + new Vector3(13.2345f, 0, -8.7654f));
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(rule.Actions[0])); f.M.Effect(1, rule.Actions[0]);
        Dal2Compare(f, source, rule.Component);
        RobotFinish(f, source, spell); Dal2Compare(f, source, rule.Component);
        Check(!f.M.Damage(f.NativeViewer).Any(), "DAL3 AOE no invented damage");
    }

    private static void Dal3Mobile()
    {
        using var f = new Fixture(31);
        var source = f.Activate<GenericAOEs>("MobileHaloCrossray");
        foreach (var action in Enumerable.Range(23993, 8).Select(a => (uint)a))
        {
            var spell = RobotStart(f, source, action);
            Dal2Compare(f, source, "MobileHaloCrossray");
            RobotFinish(f, source, spell);
            Dal2Compare(f, source, "MobileHaloCrossray");
        }
        Dal3Second(f);
        Dal2Compare(f, source, "MobileHaloCrossray");
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        Dal2Compare(f, source, "MobileHaloCrossray");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23990)); f.M.Effect(1, 23990);
        Dal2Compare(f, source, "MobileHaloCrossray");
    }

    private static void Dal3Plumes()
    {
        using var f = new Fixture(31);
        f.Add(8, 0x31E1, f.Center + new Vector3(1.2345f, 0, -3.8765f)); f.Sync();
        f.Now = START.AddSeconds(6); f.W.Frame.Timestamp = f.Now;
        Dal3Second(f);
        var source = f.Activate<GenericAOEs>("VerdantPlumeVermilionFlame");
        Dal2Compare(f, source, "VerdantPlumeVermilionFlame");
        Check(f.M.Visible().Single().Cast.Activation == f.Now.AddSeconds(14.4d), "DAL3 cold plume from activation clock");
        f.Add(9, 0x31E0, f.Center + Vector3.UnitX * 12); f.Sync();
        Dal2Compare(f, source, "VerdantPlumeVermilionFlame");
        f.Move(8, f.Center + Vector3.UnitX * 20);
        f.Remove(9);
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        Dal2Compare(f, source, "VerdantPlumeVermilionFlame");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(24015)); f.M.Effect(1, 24015);
        Dal2Compare(f, source, "VerdantPlumeVermilionFlame");
        source.OnCastFinished(f.W.Actors.Find(1)!, new() { Action = new(ActionType.Spell, 24014) });
        f.M.Finish(f.M.Primary!, new(24014, 0, f.Center, 0, f.Now, f.Now, false, false));
        Dal2Compare(f, source, "VerdantPlumeVermilionFlame");
    }

    private static void Dal3Obey()
    {
        using var f = new Fixture(31);
        Dal3Second(f);
        var source = f.Activate<GenericAOEs>("Obey");
        f.Add(8, 0x1EB1E8, f.Center + new Vector3(5.12345f, 0, 14.54321f), ActorType.EventObj); f.Sync();
        f.Add(9, 0x1EB1E7, f.Center - Vector3.UnitX * 8, ActorType.EventObj); f.Sync();
        void Anim(ulong id, uint state)
        {
            source.OnActorEAnim(f.W.Actors.Find(id)!, state); f.M.EAnim(id, state);
            Dal2Compare(f, source, "Obey");
        }
        Anim(8, 0); Anim(8, 0x10002); Anim(8, 0x10002); Anim(9, 0x100020); Anim(8, 0x400080);
        for (var i = 0; i < 5; ++i)
        {
            f.Now = START.AddSeconds(100 + i); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(i % 2 == 0 ? 24011u : 24012u)); f.M.Effect(2, i % 2 == 0 ? 24011u : 24012u);
            Dal2Compare(f, source, "Obey");
            if (i == 0) Anim(8, 0x10002);
        }
        Anim(8, 0x10002);
        f.Remove(4);
        source.Update(); f.M.Update(f.Now);
        Dal2Compare(f, source, "Obey");
        f.Add(4, 0x31DE, f.Center); f.Sync(); Anim(8, 0x10002);
        Check(!f.M.Visible().Any(), "DAL3 no sameID Dawon recache");
    }

    private static void Dal3Spiral()
    {
        for (var pattern = 0; pattern < 8; ++pattern)
        {
            using var f = new Fixture(31);
            Dal3Second(f);
            var source = f.Activate<GenericAOEs>("SpiralScourge");
            f.Move(2, DalriadaSpirals.Patterns[pattern][0] + new Vector3(1, 0, 1));
            var spell = RobotStart(f, source, 23986, total: 7);
            Dal2Compare(f, source, "SpiralScourge");
            Check(f.M.Visible().Count() == (pattern < 6 ? 6 : 0), "DAL3 match per axis first six");
            if (pattern >= 6) continue;
            for (var i = 0; i < 9; ++i)
            {
                source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23987)); f.M.Effect(1, 23987);
                Dal2Compare(f, source, "SpiralScourge");
            }
            // Unknown visual resets count, revealing already-mutated colors.
            f.Move(2, f.Center); RobotStart(f, source, 23986);
            Dal2Compare(f, source, "SpiralScourge");
            Check(f.M.Visible().All(a => a.Danger), "DAL3 old colors persist after reset");
            RobotFinish(f, source, spell); Dal2Compare(f, source, "SpiralScourge");
            f.Move(2, DalriadaSpirals.Patterns[pattern][0]); RobotStart(f, source, 23986);
            for (var i = 0; i < DalriadaSpirals.Patterns[pattern].Length * 2 + 1; ++i)
            {
                source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23987)); f.M.Effect(1, 23987);
            }
            Dal2Compare(f, source, "SpiralScourge");
            f.Add(15, 0x1EB216, f.Center, ActorType.EventObj); f.Sync();
            f.Remove(15);
            Check(f.M.SpiralNumCasts == source.NumCasts && !f.M.Visible().Any(), "DAL3 Destroy clears only queue not counter");
        }
    }

    private static void Dal3CompareOutputs(Fixture f, BossComponent source, string name)
    {
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))), name + " hints");
        var global = new BossComponent.GlobalHints(); source.AddGlobalHints(global);
        Check(global.SequenceEqual(f.M.GlobalHints()), name + " global");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        var zones = f.M.Navigation(f.NativeViewer).ToArray();
        Check(ai.ForbiddenZones.Count == zones.Length, name + " nav count");
        for (var i = 0; i < zones.Length; ++i)
        {
            Check(ai.ForbiddenZones[i].Item2 == zones[i].Activation, name + " nav clock");
            for (var x = -27f; x <= 27; x += 1.37f)
                for (var z = -27f; z <= 27; z += 1.93f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(ai.ForbiddenZones[i].Item1.Distance(new(p.X, p.Z)) == zones[i].Distance(p.X, p.Z), name + " raw distance");
                }
        }
        Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
            .SequenceEqual(f.M.Damage(f.NativeViewer).Select(d => (d.Mask, d.Activation, d.Type))), name + " damage");
    }

    private static void Dal3Bait()
    {
        using var f = new Fixture(31);
        var source = f.Activate<BaitAwayCast>("HighPoweredMagitekRay");
        var spell = f.Start(source, 24005);
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        Dal3CompareOutputs(f, source, "DAL3 bait initial duplicate");
        Dal3Second(f);
        Check(source.CurrentBaits.Count == f.M.Casts.Count(c => c.Rule.Component == "HighPoweredMagitekRay"), "DAL3 keep no duplicate cold Start");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(24005)); f.M.Effect(1, 24005);
        f.Move(101, f.Center + new Vector3(1, 0, 1));
        Dal3CompareOutputs(f, source, "DAL3 bait own R5 peers");
        f.Viewer.IsDead = true; f.Set(100, a => a with { Dead = true }); f.Sync();
        source.Update(); Dal3CompareOutputs(f, source, "DAL3 target Dead retained");
        f.Viewer.IsDead = false; f.Set(100, a => a with { Dead = false }); f.Sync();
        f.Remove(100); f.Add(100, 0, f.Center + Vector3.UnitX * 15, ActorType.Player); f.Sync();
        Dal3CompareOutputs(f, source, "DAL3 original target new generation byID display byRef nav");
        var raw = f.M.Casts.First(c => c.Rule.Component == "HighPoweredMagitekRay");
        Check(!ReferenceEquals(raw.Target, f.NativeViewer), "DAL3 retained raw target");
        RobotFinish(f, source, spell); Dal3CompareOutputs(f, source, "DAL3 Finish exactly one");
        f.M.Primary!.Actor = f.M.Primary.Actor with { Dead = true };
        Check(f.M.Active, "DAL3 primary dead no eager completion");
        using var g = new Fixture(31);
        var rawSource = g.Activate<BaitAwayCast>("HighPoweredMagitekRay");
        g.Start(rawSource, 24005, total: 3);
        g.Start(rawSource, 24005, id: 1, total: 9, target: 101);
        g.W.Actors.Find(2)!.IsDead = true; g.Set(2, a => a with { Dead = true }); g.Sync();
        rawSource.Update();
        Dal3CompareOutputs(g, rawSource, "DAL3 dead first source raw clock active second mask");
        Check(g.M.Damage(g.NativeViewer).Single().Mask == 2
            && g.M.Damage(g.NativeViewer).Single().Activation == rawSource.CurrentBaits[0].Activation, "DAL3 raw first clock survives inactive first bait");
    }

    private static void Dal3Mind()
    {
        using var f = new Fixture(31);
        var pet = f.Add(8, 0, f.Center, ActorType.Pet);
        var expire = START.AddSeconds(200);
        pet.Statuses[59] = new(2553, 0, expire, 1);
        f.Set(8, a => a with { SubKind = 2, Statuses = [new(2553, 0, 1, expire) { SlotIndex = 59 }] }); f.Sync();
        Dal3Second(f);
        var source = f.Activate<BossComponent>("OneMind");
        f.Source.PrimaryActor.Name = "primary-input"; f.Set(1, a => a with { Name = "primary-input" });
        f.W.Actors.Find(4)!.Name = "second-input"; f.Set(4, a => a with { Name = "second-input" }); f.Sync();
        foreach (var pair in new[] { (100UL, 101UL), (101UL, 100UL), (100UL, 100UL), (0UL, 0UL) })
        {
            f.Source.PrimaryActor.TargetID = pair.Item1; f.W.Actors.Find(4)!.TargetID = pair.Item2;
            f.M.Primary!.Actor = f.M.Primary.Actor with { TargetId = pair.Item1 };
            f.M.Dawon!.Actor = f.M.Dawon.Actor with { TargetId = pair.Item2 };
            Dal3CompareOutputs(f, source, "DAL3 cold Pet mind XOR");
        }
        // Real hot Pet Lose is filtered by the module, native snapshot must match.
        f.W.Execute(new ActorState.OpStatus(8, 59, default));
        f.Set(8, a => a with { Statuses = [] }); f.Sync();
        Dal3CompareOutputs(f, source, "DAL3 hot Pet Lose ignored");
        var s = new ActorStatus(2553, 0, expire, 1);
        source.OnStatusGain(f.Viewer, ref s);
        source.OnStatusGain(f.W.Actors.Find(101)!, ref s);
        source.OnStatusLose(f.Viewer, ref s);
        var apply = typeof(BozjaEncounter).GetMethod("Status", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var v in new[] { (100UL, true), (101UL, true), (100UL, false) })
            apply.Invoke(f.M, [f.M.Actors[v.Item1], new ReplayStatus(2553, 0, 1, expire), v.Item2]);
        Dal3CompareOutputs(f, source, "DAL3 any Lose clears no refcount");
    }

    private static void Dal3BaitOrder()
    {
        using var f = new Fixture(31);
        f.Add(102, 0, f.Center + new Vector3(3, 0, 1), ActorType.Player); f.Sync();
        f.W.Execute(new PartyState.OpModify(4, new(102, 102, false, "slot4")));
        f.Party.Clear(); f.Party.AddRange([new(0, "", 100), new(4, "", 102), new(1, "", 101)]);
        f.Sync();
        var source = f.Activate<BaitAwayCast>("HighPoweredMagitekRay");
        f.Start(source, 24005);
        Dal3CompareOutputs(f, source, "DAL3 true source Party slot order");
        f.Add(103, 0, f.Center, ActorType.Player);
        f.Party.Add(new(64, "", 103)); f.Sync();
        Dal3CompareOutputs(f, source, "DAL3 invalid Party slot excluded");
    }

    private static void Dal3Knockback()
    {
        using var f = new Fixture(31);
        var source = f.Activate<GenericKnockback>("Touchdown");
        var spell = RobotStart(f, source, 24006, 1);
        RobotStart(f, source, 24006, 2);
        foreach (var hitbox in new[] { .5f, 2.7f })
            foreach (var x in new[] { -27f, -26.5f, -10, 0, 17, 26.5f, 27f })
                foreach (var z in new[] { -27f, -26.5f, 0, 10, 21, 26.5f, 27f })
                {
                    var pos = f.Center + new Vector3(x, 0, z);
                    f.Viewer.PosRot = new(pos, 0); f.Viewer.HitboxRadius = hitbox;
                    f.NativeViewer.Actor = f.NativeViewer.Actor with { Position = pos, HitboxRadius = hitbox };
                    var expected = source.CalculateMovements(0, f.Viewer).Select(m => (m.from.ToVec3(), m.to.ToVec3())).ToArray();
                    var actual = f.M.SaunionMovements(f.NativeViewer).Select(m => (m.From, m.To)).ToArray();
                    Check(expected.SequenceEqual(actual), $"DAL3 square wall raw {pos}/{hitbox} source={string.Join(';', expected)} actual={string.Join(';', actual)}");
                }
        f.Viewer.HitboxRadius = .5f;
        f.Set(100, a => a with { HitboxRadius = .5f });
        f.Move(100, f.Center + Vector3.UnitX * 5);
        var activation = f.M.Casts.First(c => c.Rule.Component == "Touchdown").Activation;
        foreach (var expiry in new[] { activation, activation.AddTicks(1) })
        {
            var status = new ActorStatus(160, 0, expiry, 1); source.OnStatusGain(f.Viewer, ref status);
            typeof(BozjaEncounter).GetMethod("Status", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(f.M, [f.NativeViewer, new ReplayStatus(160, 0, 1, expiry), true]);
            Check(source.CalculateMovements(0, f.Viewer).Count() == f.M.SaunionMovements(f.NativeViewer).Count(), "DAL3 strict immune expiry");
        }
        Dal3CompareOutputs(f, source, "DAL3 KB no nav damage danger hint");
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(24007)); f.M.Effect(2, 24007);
        Check(f.M.Casts.Count(c => c.Rule.Component == "Touchdown") == 2, "DAL3 24007 no completion");
        RobotFinish(f, source, spell, 1);
        Check(f.M.Casts.Count(c => c.Rule.Component == "Touchdown") == 1, "DAL3 Finish removes one caster");
        RobotStart(f, source, 24006, 2);
        var other = new ActorCastInfo { Action = new(ActionType.Spell, 999) };
        source.OnCastFinished(f.W.Actors.Find(2)!, other);
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { ActionId = 999 });
        Check(((SimpleKnockbacks)source).Casters.Count == f.M.Casts.Count(c => c.Rule.Component == "Touchdown"),
            "DAL3 unrelated Finish preserves repeated caster leftovers");
    }

    private static void Dal3Targets()
    {
        foreach (var name in new[] { "AntiPersonnelMissile", "MissileSalvo" })
        {
            using var f = new Fixture(31);
            var source = f.Activate<UniformStackSpread>(name);
            var rule = f.M.Definition.Rules.Single(r => r.Component == name);
            var spell = f.Start(source, rule.Actions[0]);
            Dal3CompareOutputs(f, source, "DAL3 targets " + name);
            Dal3Second(f);
            Dal3CompareOutputs(f, source, "DAL3 targets phase keep " + name);
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(rule.Actions[0])); f.M.Effect(1, rule.Actions[0]);
            f.Viewer.IsDead = true; f.Set(100, a => a with { Dead = true }); f.Sync();
            Dal3CompareOutputs(f, source, "DAL3 targets hidden Dead " + name);
            f.Viewer.IsDead = false; f.Set(100, a => a with { Dead = false }); f.Sync();
            source.OnCastFinished(f.W.Actors.Find(1)!, spell);
            f.M.Finish(f.M.Primary!, f.M.Actors[2].Cast!);
            Dal3CompareOutputs(f, source, "DAL3 anycaster targetID Finish " + name);
        }
    }

    private static void Dal3Damage()
    {
        foreach (var name in new[] { "ToothAndTalon", "WildfireWinds" })
        {
            using var f = new Fixture(31);
            Dal3Second(f);
            var source = f.Activate<CastHint>(name);
            var rule = f.M.Definition.Rules.Single(r => r.Component == name);
            var spell = f.Start(source, rule.Actions[0]);
            Dal3CompareOutputs(f, source, "DAL3 dynamic " + name);
            source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
            f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; spell.ElapsedTime = 3; f.M.Advance(f.Now);
            Dal3CompareOutputs(f, source, "DAL3 capped raw clock " + name);
            RobotFinish(f, source, spell);
            Dal3CompareOutputs(f, source, "DAL3 exactly first ref Finish " + name);
        }
    }

    private static void Dal3Feed()
    {
        Initialize();
        using var session = new MechanicSession("DAL3-seventeen-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(975);
        var model = region.Encounters.Single(m => m.IsSaunion);
        var center = model.Center;
        ReplayCast Spell(uint action, double at = 0, Vector3? loc = null)
            => new(action, 100, loc ?? center, .37f, START.AddSeconds(at), START.AddSeconds(at + 8 + .3f), false, false)
                { TotalTime = 8, IsSpell = true };
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x31DD, Name = "Saunion-live-input", Kind = 2, SubKind = 5, Position = center, Targetable = true,
                InCombat = true, Hp = 1000, Cast = Spell(23993), TargetId = 100 },
            new() { Id = 4, Oid = 0x31DE, Name = "Dawon-live-input", Kind = 2, SubKind = 5, Position = center + Vector3.UnitX * 7.1234f,
                Targetable = false, Hp = 1000, TargetId = 101 },
            new() { Id = 100, Kind = 1, Position = center + Vector3.UnitX * 3, HitboxRadius = .5f, Hp = 1000 },
            new() { Id = 101, Kind = 1, Position = center - Vector3.UnitX * 10, HitboxRadius = .5f, Hp = 1000 },
            new() { Id = 102, Kind = 2, SubKind = 2, Position = center, HitboxRadius = .5f, Hp = 1000,
                Statuses = [new(2553, 0, 1, START.AddSeconds(200)) { SlotIndex = 59 }] },
        };
        foreach (var action in new uint[] { 24002, 23989, 23991, 24003, 24004, 24006, 24005 })
            actors.Add(new() { Id = (ulong)action, Oid = 0x233C, Kind = 2, SubKind = 5, Position = center,
                Hp = 1000, Cast = Spell(action) });
        ReplayPartyMember[] party = [new(0, "", 100), new(1, "", 101), new(2, "", 102)];
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Frame(double at, uint effect = 0, ulong anim = 0, uint state = 0)
        {
            var now = START.AddSeconds(at);
            session.BeginFrame(now);
            feed.ApplyFrame(new(975, 778, 100, 0, party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(),
                [], actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            if (effect != 0) feed.Enqueue(new MechanicEventActionEffect(1, 1, effect, 100, 0, center, 0, 0, 0, []));
            if (anim != 0) feed.Enqueue(new MechanicEventEObjAnimation(anim, (ushort)(state >> 16), (ushort)state));
            feed.Flush(); session.Update();
            region.Synchronize(actors.ToArray(), party, 100, now, true, () =>
            {
                if (effect != 0) region.Effect(1, effect, 100, center);
                if (anim != 0) region.EAnim(anim, state);
            });
            var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31DD);
            Check(source.Components.Select(c => c.GetType().Name).SequenceEqual(model.ActiveRules.Select(r => r.Component)), "DAL3 Feed activation order " + at);
            var viewer = session.World.Actors.Find(100)!; var native = model.Actors[100];
            var slot = session.World.Party.FindSlot(100);
            foreach (var component in source.Components.OfType<GenericAOEs>())
            {
                var expected = component.ActiveAOEs(slot, viewer).ToArray();
                var name = component.GetType().Name;
                var actual = model.Visible(native).Where(a => a.Cast.Rule.Component == name).ToArray();
                Check(expected.Select(a => (a.Origin.ToVec3(), a.Rotation.Rad, a.Activation, a.Risky, a.Color))
                    .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky, a.Danger ? Colors.Danger : 0))),
                    "DAL3 Feed AOE " + name + "/" + at);
            }
            var ai = new AIHints(); var hints = new BossComponent.TextHints();
            foreach (var c in source.Components) { c.AddAIHints(slot, viewer, default, ai); c.AddHints(slot, viewer, hints); }
            ai.Normalize();
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(model.Hints(native).Select(h => (h.Text, h.Risky))), "DAL3 Feed personal " + at);
            Check(source.CalculateGlobalHints().SequenceEqual(model.GlobalHints()), "DAL3 Feed global " + at);
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(model.DamageForHost(native).Select(d => (d.Mask, d.Activation, d.Type))), "DAL3 Feed normalized damage " + at);
            var zones = model.NavigationForHost(native);
            Check(ai.ForbiddenZones.Select(z => z.Item2).SequenceEqual(zones.Select(z => z.Activation)), "DAL3 Feed normalized clocks " + at);
            for (var i = 0; i < zones.Length; ++i)
            {
                var expected = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, center, model.SampleRadius, out _);
                var actual = zones[i].Convert(center, model.SampleRadius).ToArray();
                Check(expected.Count == actual.Length, "DAL3 Feed converted count " + at);
                for (var x = -26f; x <= 26; x += 1.371f)
                    for (var z = -26f; z <= 26; z += 1.719f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        Check(expected.Any(s => s.Template.Resolve(s.Position, s.Rotation, null).Contains(p, 0))
                            == actual.Any(s => s.Contains(p, 0)), "DAL3 Feed actual host geometry " + at + "/" + zones[i].Component);
                    }
            }
            if (source.Components.OfType<GenericKnockback>().FirstOrDefault() is { } kb)
                Check(kb.CalculateMovements(slot, viewer).Select(m => (m.from.ToVec3(), m.to.ToVec3()))
                    .SequenceEqual(model.SaunionMovements(native).Select(m => (m.From, m.To))), "DAL3 Feed actual KB");
        }
        Frame(0); Frame(.1);
        actors[1] = actors[1] with { Targetable = true, Cast = Spell(24020, 1) };
        foreach (var action in new uint[] { 24701, 24016, 24018, 24013, 23986 })
            actors.Add(new() { Id = (ulong)action, Oid = 0x233C, Kind = 2, SubKind = 5, Position = center,
                Hp = 1000, Cast = Spell(action, 1, action == 23986 ? DalriadaSpirals.Patterns[3][0] : center) });
        actors.Add(new() { Id = 80, Oid = 0x31E1, Kind = 2, SubKind = 5, Position = center + Vector3.UnitX * 13, Hp = 1000 });
        actors.Add(new() { Id = 81, Oid = 0x1EB1E8, Kind = 1, Position = center - Vector3.UnitX * 12, Hp = 1000 });
        Frame(1); Check(model.SaunionPhase == 1, "DAL3 Feed P1");
        Frame(2, anim: 81, state: 0x10002); Frame(3, anim: 81, state: 0x10002);
        Frame(4, 23987); Frame(5, 24011); Frame(6, 23992);
        actors[4] = actors[4] with { Statuses = [] }; Frame(7);
        for (var i = 0; i < actors.Count; ++i) actors[i] = actors[i] with { Cast = null };
        Frame(9); Frame(10, 24014); Frame(11, 24012);
        actors[0] = actors[0] with { Statuses = [new(2553, 0, 1, START.AddSeconds(30)) { SlotIndex = 59 }] }; Frame(12);
        actors[1] = actors[1] with { Statuses = [new(2553, 0, 1, START.AddSeconds(30)) { SlotIndex = 0 }] }; Frame(13);
        actors[0] = actors[0] with { Statuses = [] }; Frame(14);
        Frame(100);
    }

    private static void Dal3DawonOrder()
    {
        Initialize();
        using var session = new MechanicSession("DAL3-lazy-Dawon-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(975);
        var model = region.Encounters.Single(m => m.IsSaunion);
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x31DD, Kind = 2, SubKind = 5, Position = model.Center,
                Targetable = true, InCombat = true, Hp = 1000 },
            new() { Id = 10, Oid = 0x233C, Kind = 2, Position = model.Center, Hp = 1000 },
            new() { Id = 11, Oid = 0x233C, Kind = 2, Position = model.Center, Hp = 1000 },
            new() { Id = 100, Kind = 1, Position = model.Center, Hp = 1000 },
        };
        ReplayPartyMember[] party = [new(0, "", 100)];
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        Dal3Source Frame(double at)
        {
            var now = START.AddSeconds(at);
            session.BeginFrame(now);
            feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, "")], [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            feed.Flush(); session.Update();
            region.Synchronize(actors.ToArray(), party, 100, now, true);
            return (Dal3Source)session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31DD);
        }
        var source = Frame(0);
        Check(source.RelevantEnemies[0x31DE].Count == 0 && model.Dawon == null, "DAL3 initialized empty source cache");
        actors.RemoveAll(a => a.Id is 10 or 11);
        actors.Add(new() { Id = 40, Oid = 0x31DE, Kind = 2, SubKind = 5, Position = model.Center,
            Targetable = false, Hp = 1000 });
        actors.Add(new() { Id = 41, Oid = 0x31DE, Kind = 2, SubKind = 5, Position = model.Center,
            Targetable = true, Hp = 1000 });
        source = Frame(1);
        Check(source.BossDawon?.InstanceID == 40 && model.Dawon?.Actor.Id == 40,
            $"DAL3 hot Created list order source={source.BossDawon?.InstanceID} native={model.Dawon?.Actor.Id}");
        Frame(2);
        Check(source.StateMachine.ActivePhaseIndex == 0 && model.SaunionPhase == 0,
            "DAL3 second targetable actor cannot advance phase");
        actors.RemoveAll(a => a.Id == 40);
        Frame(3);
        Check(source.BossDawon!.IsDestroyed && model.Dawon!.Destroyed && model.Dawon.Actor.Id == 40,
            "DAL3 removed cached original never rebounds to surviving second");
    }
}
