using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using CavalryAID = BossMod.Shadowbringers.Foray.CriticalEngagement.CE53HereComesTheCavalry.AID;

internal static partial class BozjaRegression
{
    private static void RunCavalry(Action<string, Action> run)
    {
        run("Bozja native CE53 real client all actions fourteen components circle untargetable pull", CavalryClient);
        run("Bozja native CE53 six standard AOEs exact raw source clocks Finish no visual knockback fabrication", CavalryStandard);
        run("Bozja native CE53 Charge raw direction then quantized origin duplicates first caster Finish", CavalryCharge);
        run("Bozja native CE53 Gust duplicate raw casters no AI ignore immune first applicable movement", CavalryGust);
        run("Bozja native CE53 RideDown two centered cones replace sources max1 after shape watched Finish", CavalryRide);
        run("Bozja native CE53 FireShot three lists target lookup event lock state7 dead first per Update", CavalryFire);
        run("Bozja native CE53 RawSteel true OpTether repeated colour cache delay Finish no clear", CavalrySteel);
        run("Bozja native CE53 RawSteel source target sides generations untether original cache actual Effect FIFO", CavalrySteelLife);
        run("Bozja native CE53 RawSteel exact text AI damage NaN close buddy dead source old head", CavalrySteelOutput);
        run("Bozja native CE53 RawSteel original destroyed references viewer same ID recreation", CavalrySteelGenerations);
        run("Bozja native CE53 RawSteel direct duplicate callback good bad records and invalid tether sides", CavalrySteelCallbacks);
        run("Bozja native CE53 RawSteel actual slot order independent DTO order and NaN distance hint", CavalrySteelParty);
        run("Bozja native CE53 actual Feed hot Created tether before shared Dead original target damage", CavalryCreatedDead);
        run("Bozja native CE53 actual Feed preScan new tether cannot see later-created target", () => CavalryPreScan(false));
        run("Bozja native CE53 actual Feed preScan tether consumes previous Party before same-frame roster", () => CavalryPreScan(true));
        run("Bozja native CE53 actual Feed preScan shared Region loads source order with CE51", () => CavalryRegionPreScan(false));
        run("Bozja native CE53 actual Feed changed preScan cannot commit global births before CE51", () => CavalryRegionPreScan(true));
        run("Bozja native CE53 actual Feed fourteen components cold tethers source Update merged normalized outputs", CavalryFeed);
    }

    private static void CavalryClient()
    {
        using var f = new Fixture(16);
        var row = Service.LuminaRow<Lumina.Excel.Sheets.DynamicEvent>(22);
        Console.WriteLine("INFO CE53 DE22=" + row?.Name);
        Check(row?.Name.ToString() == f.M.Definition.Name, "CE53 exact client DE22 name");
        foreach (var action in Enum.GetValues<CavalryAID>())
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)action) != null
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE53 all source AID extra " + action);
        Check(f.M.Definition.Rules.Length == 14 && f.M.Definition.PullRadius == 25, "CE53 all fourteen");
        var model = new BozjaEncounter(f.M.Definition);
        model.Synchronize(f.Actors.Select(a => a.Id == 1 ? a with { Targetable = false } : a), f.Party.ToArray(), 100, f.Now);
        Check(model.Active, "CE53 untargetable combat pull");
        f.Move(100, f.Center + new Vector3(25.001f, 0, 0));
        var far = new BozjaEncounter(f.M.Definition);
        far.Synchronize(f.Actors, f.Party.ToArray(), 100, f.Now);
        Check(!far.Active, "CE53 exact R25 pull gate");
        Damage(16);
    }

    private static void CavalryStandard()
    {
        foreach (var rule in BozjaCatalog.Definitions[16].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
        {
            using var f = new Fixture(16);
            var source = f.Activate<GenericAOEs>(rule.Component);
            var spell = f.Start(source, rule.Actions[0]);
            CompareMetalFoxBits(f, source, rule.Component, "CE53 standard start " + rule.Component);
            f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareMetalFoxBits(f, source, rule.Component, "CE53 no TTL");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, rule.Component, "CE53 watched Finish");
        }
    }

    private static ActorCastInfo CavalryChargeStart(Fixture f, GenericAOEs source, ulong id, Vector3 target)
    {
        var actor = f.W.Actors.Find(id)!;
        var spell = new ActorCastInfo { Action = new(ActionType.Spell, 23936), TotalTime = 3,
            TargetID = 100, Location = target, Rotation = new(2.1f) };
        actor.CastInfo = spell;
        source.OnCastStarted(actor, spell);
        f.Set(id, a => a with { Cast = new(23936, 100, target, 2.1f, f.Now, f.Now.AddSeconds(3.3f), false, false)
            { IsSpell = true, TotalTime = 3 } });
        f.Sync();
        return spell;
    }

    private static void CavalryCharge()
    {
        using var f = new Fixture(16);
        var source = f.Activate<GenericAOEs>("BurnishedJoust");
        f.Move(2, f.Center + new Vector3(7.12345f, 0, -2.31234f));
        var spell = CavalryChargeStart(f, source, 2, f.Center + new Vector3(-5, 0, 13));
        CompareMetalFoxBits(f, source, "BurnishedJoust", "CE53 true charge length raw before quantize");
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        f.Add(3, 0x31C6, f.Center); f.Sync();
        CavalryChargeStart(f, source, 3, f.Center - Vector3.UnitX * 20);
        CompareMetalFoxBits(f, source, "BurnishedJoust", "CE53 all three charges not replacement");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Check(source.ActiveAOEs(0, f.Viewer).Length == 2, "CE53 first only charge Finish");
        CompareMetalFoxBits(f, source, "BurnishedJoust", "CE53 raw first ID removal");
    }

    private static void CompareCavalryKnockback(Fixture f, GenericKnockback source, string component, string step)
    {
        var rule = f.M.Definition.Rules.Single(r => r.Component == component);
        var expected = source.ActiveKnockbacks(0, f.Viewer).ToArray();
        var actual = f.M.CavalryKnockbacks.Where(c => c.Rule == rule).ToArray();
        Check(expected.Length == actual.Length, step + " raw sources");
        for (var i = 0; i < actual.Length; ++i)
            Check(expected[i].Origin.ToVec3() == actual[i].Origin && expected[i].Direction.Rad == actual[i].Rotation
                && expected[i].Activation == actual[i].Activation && expected[i].IgnoreImmunes,
                step + " raw exact snapshot");
        var moves = source.CalculateMovements(0, f.Viewer);
        var native = f.M.CavalryMovements(f.NativeViewer, rule).ToArray();
        Check(moves.Count == native.Length, step + " successful max1");
        for (var i = 0; i < native.Length; ++i)
            Check(moves[i].from.ToVec3() == native[i].From && moves[i].to.ToVec3() == native[i].To,
                step + " exact from to");
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
            step + " exact unsafe text");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        Check(ai.ForbiddenZones.Count == 0 && !f.M.Navigation(f.NativeViewer).Any(), step + " no invented AI");
    }

    private static void CavalryGust()
    {
        using var f = new Fixture(16);
        var source = f.Activate<GenericKnockback>("GustSlash");
        var spell = f.Start(source, 23934);
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        CompareCavalryKnockback(f, source, "GustSlash", "CE53 duplicate casters");
        var immune = new ActorStatus(2345, 0, START.AddSeconds(100), 0);
        source.OnStatusGain(f.Viewer, ref immune);
        CompareCavalryKnockback(f, source, "GustSlash", "CE53 immune ignored");
        f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23934)); f.M.Effect(1, 23934);
        CompareCavalryKnockback(f, source, "GustSlash", "CE53 no Effect TTL clear");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareCavalryKnockback(f, source, "GustSlash", "CE53 Finish first only");
    }

    private static void CavalryRide()
    {
        using var f = new Fixture(16);
        var source = f.Activate<GenericKnockback>("RideDownKnockback");
        var spell = f.Start(source, 23940);
        foreach (var offset in new[] { new Vector3(10, 0, 0), new Vector3(-10, 0, 0), new Vector3(40, 0, 0) })
        {
            f.Move(100, f.Center + offset);
            CompareCavalryKnockback(f, source, "RideDownKnockback", "CE53 sided point " + offset);
        }
        f.Add(3, 0x233C, f.Center + Vector3.UnitX * 20); f.Sync();
        f.Start(source, 23940, 3);
        CompareCavalryKnockback(f, source, "RideDownKnockback", "CE53 any Start replaces two");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Check(f.M.CavalryKnockbacks.Count == 0, "CE53 unrelated watched caster Finish clears all");
    }

    private static void CavalryFire()
    {
        using var f = new Fixture(16);
        var source = f.Activate<GenericAOEs>("FireShot");
        var spell = f.Start(source, 23937);
        f.Move(100, f.Center + Vector3.UnitZ * 8);
        CompareMetalFoxBits(f, source, "FireShot", "CE53 cast tracks current target");
        foreach (var target in new ulong[] { 100, 999 })
        {
            source.OnEventCast(f.W.Actors.Find(1)!, HashmalEvent(23937, target, f.Center + Vector3.UnitZ * 8));
            f.M.Effect(1, 23937, target, f.Center + Vector3.UnitZ * 8);
        }
        CompareMetalFoxBits(f, source, "FireShot", "CE53 two zero delay effect snapshots");
        f.Add(30, 0x1EB1D3, f.Center + Vector3.UnitZ * 8);
        f.Sync(false);
        source.Update(); f.Sync();
        Check(f.M.JuiceEvents.Count == 1, "CE53 first event per actual object Update");
        CompareMetalFoxBits(f, source, "FireShot", "CE53 cast event actor three lists");
        f.W.Actors.Find(30)!.IsDead = true; f.Set(30, a => a with { Dead = true }); f.Sync(false);
        CompareMetalFoxBits(f, source, "FireShot", "CE53 dead actor remains live zone");
        f.W.Actors.Find(30)!.EventState = 7; f.Set(30, a => a with { EventState = 7 }); f.Sync(false);
        CompareMetalFoxBits(f, source, "FireShot", "CE53 state7 hidden not event cleanup");
        f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CompareMetalFoxBits(f, source, "FireShot", "CE53 no TTL");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareMetalFoxBits(f, source, "FireShot", "CE53 Finish cast only");
    }

    private static void CavalryTether(Fixture f, ulong source, uint id, ulong target)
    {
        f.W.Execute(new ActorState.OpTether(source, new(id, target)));
        f.M.Tether(source, id, target);
        f.Set(source, a => a with { Tethers = target == 0 ? [] : [new(checked((ushort)id), target)] });
    }

    private static void CompareCavalrySteel(Fixture f, BaitAwayChargeTether source, string step)
    {
        Check(source.CurrentBaits.Count == f.M.SteelBaits.Count && source.ActivationDelayOnActor.Count == f.M.SteelDelays.Count
            && source.TetherOnActor.Count == f.M.SteelTethers.Count && source.NumCasts == f.M.NumSteelCasts, step + " raw lists counter");
        for (var i = 0; i < source.CurrentBaits.Count; ++i)
        {
            var e = source.CurrentBaits[i]; var a = f.M.SteelBaits[i]; var view = BozjaEncounter.SteelView(a);
            Check(e.Source.InstanceID == a.Source.Actor.Id && e.Target.InstanceID == a.Target.Actor.Id
                && e.Activation == a.Activation && e.Rotation.Rad == view.Rotation
                && ((AOEShapeRect)e.Shape).LengthFront == ((RayRectangleTemplate)view.Shape).Length,
                step + " exact bait identity clock shape");
        }
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
            step + " exact hint text risks");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).ToArray(), step);
        Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
            .SequenceEqual(f.M.Damage(f.NativeViewer).Select(d => (d.Mask, d.Activation, d.Type))), step + " raw damage");
    }

    private static void CavalrySteel()
    {
        using var f = new Fixture(16);
        var source = f.Activate<BaitAwayChargeTether>("RawSteel");
        CavalryTether(f, 2, 57, 100);
        source.Update(); f.Sync(); CompareCavalrySteel(f, source, "CE53 first bad");
        f.Now = START.AddSeconds(2); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CavalryTether(f, 2, 57, 100);
        CompareCavalrySteel(f, source, "CE53 real repeated op untethers first reuses initial clock");
        CavalryTether(f, 2, 1, 100);
        source.Update(); f.Sync(); CompareCavalrySteel(f, source, "CE53 good replaces bad not duplicate");
        Check(f.M.SteelBaits.Single().Activation == START.AddSeconds(5d), "CE53 target cached original activation");
        var spell = new ActorCastInfo { Action = new(ActionType.Spell, 23943) };
        source.OnCastFinished(f.W.Actors.Find(2)!, spell);
        f.M.Finish(f.M.Actors[2], new(23943, 100, f.Center, 0, START, START, false, false));
        CompareCavalrySteel(f, source, "CE53 cast Finish irrelevant");
        f.Now = START.AddSeconds(6); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now); source.Update(); f.Sync();
        CompareCavalrySteel(f, source, "CE53 plus1 exact expires cache only");
        Check(f.M.SteelBaits.Count == 1 && f.M.SteelDelays.Count == 0, "CE53 no bait timeout");
    }

    private static void CavalrySteelLife()
    {
        using var f = new Fixture(16);
        var source = f.Activate<BaitAwayChargeTether>("RawSteel");
        f.Add(3, 0x31C6, f.Center - Vector3.UnitZ * 10); f.Sync();
        CavalryTether(f, 2, 57, 100); CavalryTether(f, 3, 1, 100);
        source.Update(); f.Sync(); CompareCavalrySteel(f, source, "CE53 two sources same target shared clock");
        source.OnEventCast(f.W.Actors.Find(1)!, HashmalEvent(23943, 100)); f.M.Effect(1, 23943, 100);
        CompareCavalrySteel(f, source, "CE53 arbitrary source Effect deletes first target ID");
        CavalryTether(f, 100, 57, 2);
        source.Update(); f.Sync(); CompareCavalrySteel(f, source, "CE53 player source reverses sides");
        CavalryTether(f, 100, 0, 0);
        CompareCavalrySteel(f, source, "CE53 reversed untether removes bait keeps asymmetric tether entry");
        f.Remove(3); CompareCavalrySteel(f, source, "CE53 destroy actual tether source untethers");
        source.OnEventCast(f.W.Actors.Find(1)!, HashmalEvent(23943, 999)); f.M.Effect(1, 23943, 999);
        CompareCavalrySteel(f, source, "CE53 unmatched increments counter");
    }

    private static void CavalrySteelOutput()
    {
        using var f = new Fixture(16);
        var source = f.Activate<BaitAwayChargeTether>("RawSteel");
        CavalryTether(f, 2, 57, 100); source.Update(); f.Sync();
        foreach (var distance in new[] { 1.999f, 2f, 2.001f, 20f })
        {
            f.Move(101, f.M.Actors[2].Actor.Position + Vector3.UnitX * distance);
            CompareCavalrySteel(f, source, "CE53 target cone NaN near buddy " + distance);
        }
        f.W.Actors.Find(100)!.IsDead = true; f.Set(100, a => a with { Dead = true }); f.Sync();
        CompareCavalrySteel(f, source, "CE53 dead target still active damage");
        f.W.Actors.Find(2)!.IsDead = true; f.Set(2, a => a with { Dead = true }); f.Sync();
        CompareCavalrySteel(f, source, "CE53 dead source hides raw bait");
        f.Add(3, 0x31C6, f.Center - Vector3.UnitZ * 10); f.Sync();
        f.Now = START.AddSeconds(7); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CavalryTether(f, 3, 57, 101); source.Update(); f.Sync();
        CompareCavalrySteel(f, source, "CE53 dead head still supplies combined activation");
    }

    private static void CavalrySteelGenerations()
    {
        using var f = new Fixture(16);
        var source = f.Activate<BaitAwayChargeTether>("RawSteel");
        CavalryTether(f, 2, 57, 100); source.Update(); f.Sync();
        var original = f.M.SteelBaits.Single();
        f.Remove(100);
        f.Remove(2);
        f.Add(100, 0, f.Center + Vector3.UnitZ * 20, ActorType.Player); f.Sync(); source.Update();
        Check(ReferenceEquals(f.M.SteelBaits.Single(), original) && original.Source.Destroyed
            && original.Target.Destroyed && !ReferenceEquals(original.Target, f.NativeViewer),
            "CE53 missing target prevents source-destroy untether from deleting original bait");
        CompareCavalrySteel(f, source, "CE53 destroyed original actor survives same ID new viewer");
    }

    private static void CavalrySteelCallbacks()
    {
        using var f = new Fixture(16);
        var source = f.Activate<BaitAwayChargeTether>("RawSteel");
        foreach (var (id, target) in new[] { (0u, 100UL), (57u, 0UL), (57u, 999UL), (1u, 100UL), (57u, 100UL) })
        {
            // Invoke the component callback itself to distinguish repeated raw
            // callbacks from OpTether's preceding Untethered operation.
            source.OnTethered(f.W.Actors.Find(2)!, new(id, target));
            typeof(BozjaEncounter).GetMethod("CavalryTethered", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(f.M, [f.M.Actors[2], (id, target), false]);
            source.Update(); f.Sync();
            CompareCavalrySteel(f, source, "CE53 direct callback " + id + "/" + target);
        }
        Check(f.M.SteelBaits.Count == 2 && f.M.SteelTethers.Count == 2, "CE53 direct repeated callbacks preserve both records");
        f.Move(100, f.M.Actors[2].Actor.Position + Vector3.UnitZ * 15);
        source.Update();
        CompareCavalrySteel(f, source, "CE53 exact15 good bad combination");
        f.W.Actors.Find(2)!.IsDead = true; f.Set(2, a => a with { Dead = true }); f.Sync();
        f.Add(3, 0x31C6, f.Center); f.Sync();
        source.OnTethered(f.W.Actors.Find(3)!, new(57, 101));
        typeof(BozjaEncounter).GetMethod("CavalryTethered", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(f.M, [f.M.Actors[3], (57u, 101UL), false]);
        source.Update(); f.Sync();
        CompareCavalrySteel(f, source, "CE53 raw dead-source own bait still participates extra text");
    }

    private static void CavalrySteelParty()
    {
        using var f = new Fixture(16);
        var source = f.Activate<BaitAwayChargeTether>("RawSteel");
        f.Add(102, 0, f.Center + Vector3.UnitZ * 20, ActorType.Player);
        f.W.Execute(new PartyState.OpModify(2, new(102, 102, false, "Third")));
        f.Party.Clear();
        f.Party.AddRange([new(0, "", 100), new(2, "", 102), new(1, "", 101)]);
        f.Sync();
        CavalryTether(f, 2, 57, 100); source.Update(); f.Sync();
        CompareCavalrySteel(f, source, "CE53 source slot order even with DTO2 before1");
        f.Move(2, new(float.NaN, 0, f.Center.Z));
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky)))
            && !hints.Any(h => h.Item1 == "Tether is stretched!"), "CE53 NaN distance is not greater-equal15");
    }

    private static void CavalryCreatedDead()
    {
        Initialize();
        using var session = new MechanicSession("CE53-hot-created-dead");
        var feed = new MechanicWorldFeed(session);
        var model = new BozjaEncounter(BozjaCatalog.Definitions[16]);
        var center = model.Center;
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x31C7, Kind = 2, SubKind = 5, Position = center, Hp = 1000, InCombat = true },
            new() { Id = 2, Oid = 0x233C, Kind = 2, SubKind = 5, Position = center - Vector3.UnitZ * 10, Hp = 1000 },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 15, Hp = 1000 },
        };
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        for (var step = 0; step < 3; ++step)
        {
            if (step == 1) actors.RemoveAt(3);
            if (step == 2) actors.Add(new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 15,
                Dead = true, Tethers = [new(57, 2)] });
            var now = START.AddSeconds(step);
            session.BeginFrame(now);
            feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, ""), new(1, 101, 101, "")], [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            feed.Flush(); session.Update();
            model.Synchronize(actors, [new(0, "", 100), new(1, "", 101)], 100, now);
        }
        var module = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31C7);
        var source = module.Components.OfType<BaitAwayChargeTether>().Single();
        Check(source.CurrentBaits.Single().Source.InstanceID == 2 && source.CurrentBaits.Single().Target.InstanceID == 101
            && model.SteelBaits.Single().Source.Actor.Id == 2 && model.SteelBaits.Single().Target.Actor.Id == 101,
            "CE53 new player tether sides are observed before shared dead");
        var ai = new AIHints(); source.AddAIHints(0, session.World.Actors.Find(100)!, default, ai);
        Check(ai.PredictedDamage.Single().Players.Raw == 2
            && model.Damage(model.Actors[100]).Single().Mask == 2, "CE53 dead target still predicted current party slot");
    }

    private static void CavalryPreScan(bool changeParty)
    {
        Initialize();
        using var session = new MechanicSession("CE53-prescan-" + changeParty);
        var feed = new MechanicWorldFeed(session);
        var model = new BozjaEncounter(BozjaCatalog.Definitions[16]);
        var center = model.Center;
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x31C7, Kind = 2, SubKind = 5, Position = center, Hp = 1000, InCombat = true },
            new() { Id = 2, Oid = 0x233C, Kind = 2, SubKind = 5, Position = center - Vector3.UnitZ * 10, Hp = 1000 },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
        };
        if (changeParty)
        {
            actors.Add(new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 15, Hp = 1000 });
            actors.Add(new() { Id = 102, Kind = 1, Position = center - Vector3.UnitX * 15, Hp = 1000 });
        }
        ReplayPartyMember[] party = [new(0, "", 100), new(1, "", 101)];
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        for (var step = 0; step < 2; ++step)
        {
            if (step == 1)
            {
                if (changeParty)
                {
                    party = [new(0, "", 100), new(1, "", 102)];
                    actors[4] = actors[4] with { Tethers = [new(57, 2)] };
                }
                else
                {
                    actors.Add(new() { Id = 200, Oid = 0x233C, Kind = 2, SubKind = 5,
                        Position = center - Vector3.UnitZ * 5, Hp = 1000, Tethers = [new(57, 101)] });
                    actors.Add(new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 15, Hp = 1000 });
                }
            }
            var now = START.AddSeconds(step);
            if (step == 1)
            {
                var early = changeParty
                    ? actors.Select(a => a.Id == 102 ? a with { Tethers = [] } : a).ToArray()
                    : actors.ToArray();
                model.Synchronize(early, party, 100, now.AddSeconds(-.1d), update: false, observeTethers: false);
                model.Synchronize(actors, party, 100, now.AddSeconds(-.05d), update: false, observeTethers: false);
                Check(model.Party.Single(p => p.Slot == 1).EntityId == 101
                    && model.SteelBaits.Count == 0, "CE53 multiple preScans do not commit Party or early activation clock");
            }
            session.BeginFrame(now);
            feed.ApplyFrame(new(975, 778, 100, 0,
                party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            feed.Flush(); session.Update();
            model.Synchronize(actors, party, 100, now, update: false, observeTethers: false);
            model.Synchronize(actors, party, 100, now);
            // An additional identical pre-scan/formal scan must not invent
            // the skipped callback or duplicate the successful one.
            model.Synchronize(actors, party, 100, now, update: false, observeTethers: false);
            model.Synchronize(actors, party, 100, now);
        }
        var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31C7)
            .Components.OfType<BaitAwayChargeTether>().Single();
        Check(source.CurrentBaits.Select(b => (b.Source.InstanceID, b.Target.InstanceID, b.Activation))
            .SequenceEqual(model.SteelBaits.Select(b => (b.Source.Actor.Id, b.Target.Actor.Id, b.Activation))),
            "CE53 preScan source original identity order and clock");
        if (changeParty)
            Check(source.CurrentBaits.Count == 1 && model.SteelBaits.Single().Source.Actor.Id == 102
                && model.SteelBaits.Single().Target.Actor.Id == 2, "CE53 old Party decides tether sides before new roster");
        else
            Check(source.CurrentBaits.Count == 0 && model.SteelBaits.Count == 0,
                "CE53 later-created target does not retroactively retry initial tether");
    }

    private static void CavalryRegionPreScan(bool changed)
    {
        Initialize();
        using var session = new MechanicSession("CE53-region-prescan");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(975);
        var actors = new List<ReplayActorState>
        {
            new() { Id = 100, Kind = 1, Position = new(-570, 0, 510), Hp = 1000 },
        };
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        for (var step = 0; step < 2; ++step)
        {
            if (step == 1)
            {
                actors.Add(new() { Id = 2, Oid = 0x319A, Kind = 2, SubKind = 5,
                    Position = new(-390, 0, 230), Hp = 1000, Targetable = true });
                var cavalry = new ReplayActorState { Id = 1, Oid = 0x31C7, Kind = 2, SubKind = 5,
                    Position = new(-750, 0, 790), Hp = 1000, Targetable = true };
                actors.Insert(changed ? 2 : 1, cavalry);
                if (changed)
                    region.Synchronize([actors[0], cavalry], [new(0, "", 100)], 100,
                        START.AddSeconds(.9d), false);
            }
            var now = START.AddSeconds(step);
            session.BeginFrame(now);
            feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, "")], [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            feed.Flush(); session.Update();
            region.Synchronize(actors.ToArray(), [new(0, "", 100)], 100, now.AddSeconds(-.05d), false);
            region.Synchronize(actors.ToArray(), [new(0, "", 100)], 100, now, true);
        }
        Check(session.BossModules.LoadedModules.Select(m => m.PrimaryActor.OID)
            .SequenceEqual(region.Loaded.Select(m => m.Primary!.Actor.Oid)), "CE53 cross-CE source global load order");
        var first = changed ? 0x319Au : 0x31C7u;
        Check(session.BossModules.ActiveModule?.PrimaryActor.OID == first
            && region.Selected?.Primary?.Actor.Oid == first, "CE53 same-priority sticky selection respects original order");
    }

    private static void CavalryFeed()
    {
        Initialize();
        using var session = new MechanicSession("CE53-fourteen-feed");
        var feed = new MechanicWorldFeed(session);
        var model = new BozjaEncounter(BozjaCatalog.Definitions[16]);
        var center = model.Center;
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x31C7, Kind = 2, SubKind = 5, Position = center, Hp = 1000, InCombat = true },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000, Tethers = [new(57, 10)] },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 15, Hp = 1000 },
            new() { Id = 40, Oid = 0x1EB1D3, Kind = 7, Position = center + Vector3.UnitZ * 10, Hp = 1000 },
        };
        foreach (var (action, i) in new uint[] { 23931, 23932, 23936, 23934, 23937, 24872, 23939, 23940, 23948, 23945, 23947, 23951, 23952 }
            .Select((a, i) => (a, i)))
            actors.Add(new() { Id = (ulong)(10 + i), Oid = 0x233C, Kind = 2, SubKind = 5,
                Position = center + Vector3.UnitZ * -10, Hp = 1000, Cast = new(action, 100, center, .4f, START,
                    START.AddSeconds(5.3f), false, false) { TotalTime = 5, IsSpell = true },
                Tethers = i == 0 ? [new(57, 100)] : [] });
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var seconds in new[] { 0d, 1d, 100d })
        {
            var now = START.AddSeconds(seconds);
            session.BeginFrame(now);
            feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, ""), new(1, 101, 101, "")], [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            feed.Flush(); session.Update();
            model.Synchronize(actors, [new(0, "", 100), new(1, "", 101)], 100, now);
            var module = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31C7);
            Check(module.Components.Count() == 14 && model.Active, "CE53 all fourteen cold untargetable");
            var ai = new AIHints();
            foreach (var component in module.Components)
                component.AddAIHints(0, session.World.Actors.Find(100)!, default, ai);
            ai.Normalize();
            var zones = model.NavigationForHost(model.Actors[100]);
            Check(ai.ForbiddenZones.Count == zones.Length && ai.ForbiddenZones.Select(z => z.Item2)
                .SequenceEqual(zones.Select(z => z.Activation)), "CE53 normalized merged clocks");
            for (var i = 0; i < zones.Length; ++i)
            {
                var expected = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, center, 25, out _);
                var actual = zones[i].Convert(center, 25).ToArray();
                Check(expected.Count == actual.Length, "CE53 Feed actual conversion count " + i);
                for (var x = -24f; x < 24; x += 1.31f)
                    for (var z = -24f; z < 24; z += 1.67f)
                    {
                        var point = center + new Vector3(x, 0, z);
                        Check(expected.Any(s => s.Template.Resolve(s.Position, s.Rotation, null).Contains(point, 0))
                            == actual.Any(s => s.Contains(point, 0)), "CE53 Feed converted geometry " + i);
                    }
            }
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(model.DamageForHost(model.Actors[100]).Select(d => (d.Mask, d.Activation, d.Type))),
                "CE53 normalized merged damage");
        }
    }
}
