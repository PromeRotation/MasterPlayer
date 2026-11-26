using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using AidaAID = BossMod.Shadowbringers.Foray.CriticalEngagement.CE62LooksToDieFor.AID;

internal static partial class BozjaRegression
{
    private static void RunAida(Action<string, Action> run)
    {
        run("Bozja native CE62 actual client all24 actions two empty names eleven components exact circle", AidaClient);
        run("Bozja native CE62 standard AOEs source ray cone group99 no invented knockback", AidaStandard);
        run("Bozja native CE62 source three raidwide dynamic masks clocks target fallback pure Charybdis hint", AidaDamage);
        run("Bozja native CE62 Lightning Bolt raw duplicate Start Effect replaces first repeat resets donut clock", AidaLightning);
        run("Bozja native CE62 Lightning Clap actual caster versus TargetPos axis tolerance first removal", AidaClap);
        run("Bozja native CE62 Cloud cached two perline actor origin package direction dynamic actual position", AidaCloud);
        run("Bozja native CE62 Cloud both Effects actor not target four explosions first matching duplicate", AidaCloudEffects);
        run("Bozja native CE62 Burn raw repeated ModelState callbacks strict head clock prefix hidden promotion", AidaBurn);
        run("Bozja native CE62 Burn full tuple snapshot baseline preScan not cold replay pets only filter", AidaSnapshots);
        run("Bozja native CE62 Burn empty source FIFO throws retains history genuine MDLS recovery", AidaBurnError);
        run("Bozja native CE62 actual Feed eleven cold components missing MDLS history hot tuple before events", AidaFeed);
    }

    private static void AidaClient()
    {
        using var f = new Fixture(15);
        Check(Service.LuminaRow<Lumina.Excel.Sheets.DynamicEvent>(30)?.Name.ToString() == f.M.Definition.Name,
            "CE62 DE30 actual client");
        Check(Enum.GetValues<AidaAID>().Length == 24 && f.M.Definition.Rules.Length == 11, "CE62 all source AIDs eleven");
        foreach (var action in Enum.GetValues<AidaAID>())
            Check(new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE62 extra " + action);
        foreach (var action in new uint[] { 23965, 24711 })
            Check(Service.LuminaRow<Lumina.Excel.Sheets.Action>(action) is { } row && row.Name.ToString() == "",
                "CE62 real empty name not missing");
        Check(f.M.Definition.PullRadius == 20 && f.M.Definition.SampleRadius == 20, "CE62 real R20");
    }

    private static void AidaStandard()
    {
        foreach (var (name, action) in new[] { ("TwistingWinds", 23970u), ("Lash", 23977u), ("Lash", 23978u) })
        {
            using var f = new Fixture(15);
            var source = f.Activate<GenericAOEs>(name);
            var spell = f.Start(source, action);
            CompareMetalFoxBits(f, source, name, "CE62 standard locked");
            f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(action)); f.M.Effect(1, action);
            CompareMetalFoxBits(f, source, name, "CE62 no Effect or TTL cleanup");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, name, "CE62 true Finish");
        }
        GroupOrder(15);
    }

    private static void AidaDamage()
    {
        Damage(15);
        using var f = new Fixture(15);
        var source = f.Activate<CastHint>("Charybdis");
        var spell = f.Start(source, 23979);
        var hints = new BossComponent.GlobalHints(); source.AddGlobalHints(hints);
        Check(hints.SequenceEqual(f.M.GlobalHints()) && !f.M.Damage(f.NativeViewer).Any()
            && !f.M.Navigation(f.NativeViewer).Any(), "CE62 CastHint only");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Check(!f.M.GlobalHints().Any(), "CE62 hint watched Finish");
    }

    private static void AidaLightning()
    {
        using var f = new Fixture(15);
        var source = f.Activate<GenericAOEs>("LightningBoltDistantClap");
        var spell = f.Start(source, 23968);
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        CompareMetalFoxBits(f, source, "LightningBoltDistantClap", "CE62 two duplicate origins");
        var point = spell.Location + new Vector3(.9f, 0, .9f);
        foreach (var seconds in new[] { 1d, 10d })
        {
            f.Now = START.AddSeconds(seconds); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            source.OnEventCast(f.W.Actors.Find(1)!, HashmalEvent(23968, position: point));
            f.M.Effect(1, 23968, position: point);
            CompareMetalFoxBits(f, source, "LightningBoltDistantClap", "CE62 replace/reset first existing donut " + seconds);
            Check(f.M.Visible().First().Cast.Shape is DonutTemplate && f.M.Visible().Last().Cast.Shape is CircleTemplate,
                "CE62 first replacement keeps second raw");
        }
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareMetalFoxBits(f, source, "LightningBoltDistantClap", "CE62 Finish retains both");
    }

    private static void AidaClap()
    {
        using var f = new Fixture(15);
        var source = f.Activate<GenericAOEs>("LightningBoltDistantClap");
        f.Start(source, 23968);
        var point = f.M.Visible().Single().Cast.Origin;
        f.Move(1, point + new Vector3(1.001f, 0, 0));
        source.OnEventCast(f.W.Actors.Find(1)!, HashmalEvent(23969, position: point));
        f.M.Effect(1, 23969, position: point);
        CompareMetalFoxBits(f, source, "LightningBoltDistantClap", "CE62 correct TargetPos wrong caster no removal");
        f.Move(1, point + new Vector3(.9f, 0, .9f));
        source.OnEventCast(f.W.Actors.Find(1)!, HashmalEvent(23969, position: default));
        f.M.Effect(1, 23969, position: default);
        CompareMetalFoxBits(f, source, "LightningBoltDistantClap", "CE62 actual caster axis match");
    }

    private static void AidaCloud()
    {
        using var f = new Fixture(15);
        var source = f.Activate<Exaflare>("CloudToGround");
        f.Start(source, 23972);
        source.Update();
        CompareMetalFoxBits(f, source, "CloudToGround", "CE62 initial two", Colors.AOE);
        f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        source.Update(); f.Sync();
        CompareMetalFoxBits(f, source, "CloudToGround", "CE62 unchanged cached past future", Colors.AOE);
        f.Add(3, 0x233C, f.Center - Vector3.UnitZ * 10); f.Sync();
        f.Start(source, 23972, 3); source.Update();
        CompareMetalFoxBits(f, source, "CloudToGround", "CE62 all future before all imminent", Colors.AOE);
    }

    private static void AidaCloudEffects()
    {
        using var f = new Fixture(15);
        var source = f.Activate<Exaflare>("CloudToGround");
        f.Start(source, 23972); source.Update();
        for (var i = 0; i < 4; ++i)
        {
            var point = source.Lines[0].Next.ToVec3();
            var action = i == 0 ? 23972u : 23973u;
            source.OnEventCast(f.W.Actors.Find(1)!, HashmalEvent(action, position: point));
            f.M.Effect(1, action, position: point);
            Check(source.Lines[0].ExplosionsLeft == 4 - i && f.M.Exaflares[0].Left == 4 - i,
                "CE62 matching target alone cannot advance");
            f.Move(1, point + new Vector3(.9f, 0, .9f));
            f.Now = f.Now.AddSeconds(1); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            source.OnEventCast(f.W.Actors.Find(1)!, HashmalEvent(action, position: new(999, 0, 999)));
            f.M.Effect(1, action, position: new(999, 0, 999));
            CompareMetalFoxBits(f, source, "CloudToGround", "CE62 pre Update " + i, Colors.AOE);
            source.Update(); f.Sync();
            CompareMetalFoxBits(f, source, "CloudToGround", "CE62 actual caster anchors next " + i, Colors.AOE);
            f.Move(1, f.Center - Vector3.UnitX * 30);
        }
        Check(!source.Lines.Any() && !f.M.Exaflares.Any(), "CE62 four actual Effects");
    }

    private static void AidaBurn()
    {
        using var f = new Fixture(15);
        var source = f.Activate<GenericAOEs>("Burn");
        var actor = f.Add(30, 0x31CB, f.Center + new Vector3(1.123f, 0, 4.257f)); f.Sync();
        foreach (var seconds in new[] { 0d, .999d, 1d, .5d })
        {
            f.Now = START.AddSeconds(seconds); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            source.OnActorModelStateChange(actor, 0, 1, 0); f.M.ModelState(f.M.Actors[30], 0, 1, 0);
            CompareMetalFoxBits(f, source, "Burn", "CE62 strict consecutive prefix " + seconds);
        }
        Check(f.M.Burns.Count == 4 && f.M.Visible().Count() == 2, "CE62 noncontiguous earlier item remains hidden");
        f.Remove(30);
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23976)); f.M.Effect(1, 23976);
        CompareMetalFoxBits(f, source, "Burn", "CE62 FIFO promotion no source or timeout cleanup");
        Check(f.M.Visible().Count() == 3, "CE62 head clock changed hidden promoted");
    }

    private static void AidaModel(Fixture f, ulong id, byte model, byte first, byte second, bool preScan = false)
    {
        f.W.Execute(new ActorState.OpModelState(id, new(model, first, second)));
        f.Set(id, a => a with { ModelState = model, AnimationState1 = first, AnimationState2 = second });
        if (preScan) f.M.Synchronize(f.Actors, f.Party.ToArray(), 100, f.Now, false, observeTethers: false);
        f.Sync();
    }

    private static void AidaSnapshots()
    {
        foreach (var type in new[] { ActorType.Helper, ActorType.Player, ActorType.Pet })
        {
            using var f = new Fixture(15);
            var source = f.Activate<GenericAOEs>("Burn");
            f.Add(30, 0x31CB, f.Center, type);
            if (type == ActorType.Pet) f.Set(30, a => a with { SubKind = 2 });
            f.Sync();
            AidaModel(f, 30, 0, 1, 0, true);
            CompareMetalFoxBits(f, source, "Burn", "CE62 preScan does not consume baseline " + type);
            f.Sync();
            Check(f.M.Burns.Count == (type == ActorType.Pet ? 0 : 1), "CE62 same snapshot no repeat");
            AidaModel(f, 30, 1, 1, 0);
            AidaModel(f, 30, 1, 1, 1);
            CompareMetalFoxBits(f, source, "Burn", "CE62 full tuple and only Pet excluded " + type);
            Check(f.M.Burns.Count == (type == ActorType.Pet ? 0 : 3), "CE62 other fields retrigger");
        }
        using var cold = new Fixture(15);
        cold.Add(30, 0x31CB, cold.Center);
        cold.W.Execute(new ActorState.OpModelState(30, new(1, 1, 1)));
        cold.Set(30, a => a with { ModelState = 1, AnimationState1 = 1, AnimationState2 = 1 });
        var component = cold.Activate<GenericAOEs>("Burn");
        var native = new BozjaEncounter(cold.M.Definition);
        native.Synchronize(cold.Actors, cold.Party.ToArray(), 100, START);
        native.Synchronize(cold.Actors, cold.Party.ToArray(), 100, START.AddSeconds(1));
        Check(component.ActiveAOEs(0, cold.Viewer).Length == 0 && native.Burns.Count == 0,
            "CE62 no ModelState activation replay");
    }

    private static void AidaBurnError()
    {
        using var f = new Fixture(15);
        var source = f.Activate<GenericAOEs>("Burn");
        var sourceFailed = false; var nativeFailed = false;
        try { source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23976)); }
        catch (ArgumentOutOfRangeException) { sourceFailed = true; }
        try { f.M.Effect(1, 23976); }
        catch (ArgumentOutOfRangeException) { nativeFailed = true; }
        Check(sourceFailed && nativeFailed && f.M.SequenceError != null, "CE62 source empty FIFO not no-op");
        f.Add(30, 0x31CB, f.Center); f.Sync();
        AidaModel(f, 30, 0, 1, 0);
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23976)); f.M.Effect(1, 23976);
        Check(f.M.SequenceError == null, "CE62 real MDLS unlocks retained Effect");
        CompareMetalFoxBits(f, source, "Burn", "CE62 recovered source queue");
    }

    private static void AidaFeed()
    {
        Initialize();
        using var session = new MechanicSession("CE62-full-feed");
        var feed = new MechanicWorldFeed(session);
        var model = new BozjaEncounter(BozjaCatalog.Definitions[15]);
        var center = model.Center;
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x31C9, Kind = 2, SubKind = 5, Position = center, Hp = 1000, InCombat = true, Targetable = true },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 15, Hp = 1000 },
            new() { Id = 40, Oid = 0x31CB, Kind = 2, Position = center, Hp = 1000, AnimationState1 = 1 },
        };
        foreach (var (action, i) in new uint[] { 23964, 23968, 23970, 23972, 23974, 23977, 23979, 23981, 23984, 23985 }
            .Select((a, i) => (a, i)))
            actors.Add(new() { Id = (ulong)(10 + i), Oid = 0x233C, Kind = 2, SubKind = 5,
                Position = center, Hp = 1000, Cast = new(action, 100, center, .4f, START,
                    START.AddSeconds(5.3f), false, false) { TotalTime = 5, IsSpell = true } });
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var seconds in new[] { 0d, 1d, 100d })
        {
            var now = START.AddSeconds(seconds);
            if (seconds == 1) actors[3] = actors[3] with { ModelState = 1 };
            session.BeginFrame(now);
            feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, ""), new(1, 101, 101, "")], [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            feed.Flush(); session.Update();
            model.Synchronize(actors, [new(0, "", 100), new(1, "", 101)], 100, now);
            var module = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31C9);
            Check(module.Components.Count() == 11 && model.Active && model.Burns.Count == (seconds == 0 ? 0 : 1),
                "CE62 actual eleven cold no replay then hot MDLS");
            var ai = new AIHints();
            foreach (var component in module.Components)
            {
                component.AddAIHints(0, session.World.Actors.Find(100)!, default, ai);
                if (component is not GenericAOEs aoes) continue;
                var expected = aoes.ActiveAOEs(0, session.World.Actors.Find(100)!).ToArray();
                var actual = model.Visible().Where(a => a.Cast.Rule.Component == component.GetType().Name).ToArray();
                Check(expected.Select(a => (a.Origin.ToVec3(), a.Rotation.Rad, a.Activation, a.Risky))
                    .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky))),
                    "CE62 actual Feed full AOE " + component.GetType().Name);
            }
            ai.Normalize();
            var zones = model.NavigationForHost(model.Actors[100]);
            Check(ai.ForbiddenZones.Count == zones.Length && ai.ForbiddenZones.Select(z => z.Item2)
                .SequenceEqual(zones.Select(z => z.Activation)), "CE62 actual normalized forbidden clocks");
            for (var i = 0; i < zones.Length; ++i)
            {
                var expected = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0, center, 20, out _);
                var actual = zones[i].Convert(center, 20).ToArray();
                Check(expected.Count == actual.Length, "CE62 actual Feed converted shape count " + i);
                for (var x = -19f; x <= 19; x += 1.31f)
                    for (var z = -19f; z <= 19; z += 1.67f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        Check(expected.Any(s => s.Template.Resolve(s.Position, s.Rotation, null).Contains(p, 0))
                            == actual.Any(s => s.Contains(p, 0)), "CE62 normalized geometry order " + i);
                    }
            }
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation))
                .SequenceEqual(model.DamageForHost(model.Actors[100]).Select(d => (d.Mask, d.Activation))),
                "CE62 actual normalized damage");
        }
    }
}
