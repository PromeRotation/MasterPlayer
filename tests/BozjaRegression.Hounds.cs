using System.Numerics;
using BossMod;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static partial class BozjaRegression
{
    private static void RunHounds(Action<string, Action> run)
    {
        run("Bozja native CE12 standard actual source shapes grouped arbitrary Finish and hints", HoundsStandard);
        run("Bozja native CE12 Hellpounce actual source overwrite unpaired Finish mirror clock zero geometry", HoundsPounce);
        run("Bozja native CE12 VoidQuake actual source global FIFO reverse clock hidden promotion real replacement", HoundsQuake);
        run("Bozja native CE12 cold source world order actual casts zero history and pull polygon", HoundsCold);
        run("Bozja native CE12 CastHint actual source original caster duplicate watched Finish no damage", HoundsHint);
        run("Bozja native CE12 client thirteen consumed actions DynamicEvent exact arena", HoundsClient);
        run("Bozja native CE12 actual source single target NPC saturated clock original reference", () => Damage(2));
        run("Bozja native CE12 actual source three-action group 98 99 100 ties arbitrary Finish", () => GroupOrder(2));
        run("Bozja native CE12 actual manager cold26 radius selection primary missing source rebind", HoundsRegion);
    }

    private static void CompareHounds(Fixture f, BossMod.Components.GenericAOEs source, string step)
    {
        var expected = source.ActiveAOEs(0, f.Viewer).ToArray();
        var actual = f.M.Visible().Where(a => a.Cast.Rule.Component == source.GetType().Name).ToArray();
        Check(expected.Length == actual.Length, $"{step} AOE count {expected.Length}/{actual.Length}.");
        for (var i = 0; i < expected.Length; ++i)
        {
            var e = expected[i]; var a = actual[i].Cast;
            Check(e.Origin.X == a.Origin.X && e.Origin.Z == a.Origin.Z && e.Rotation.Rad == a.Rotation
                && e.Activation == a.Activation && e.Risky == actual[i].Risky && e.Color == 0, $"{step}[{i}] fields.");
            for (var x = f.Center.X - 65; x < f.Center.X + 65; x += 2.31f)
                for (var z = f.Center.Z - 65; z < f.Center.Z + 65; z += 1.73f)
                    Check(e.Check(new(x, z)) == a.Contains(new(x, 0, z)), $"{step}[{i}] shape {x},{z}.");
        }
        var ai = new AIHints();
        f.Source.CalculateAIHints(0, f.Viewer, default, ai);
        var sampleCenter = ai.PathfindMapCenter.ToVec3();
        var sampleRadius = ai.PathfindMapBounds!.Radius;
        Check(sampleCenter == f.Center && sampleRadius == f.M.Definition.SampleRadius,
            $"{step} actual host sample parameters {sampleCenter}/{sampleRadius}.");
        var zones = f.M.Navigation(f.NativeViewer).Where(z => z.Component == source.GetType().Name).ToArray();
        Check(ai.ForbiddenZones.Count == zones.Length, step + " raw zone count.");
        for (var i = 0; i < zones.Length; ++i)
        {
            var raw = ai.ForbiddenZones[i];
            Check(raw.Item2 == zones[i].Activation, step + " nav clock.");
            var converted = MechanicHintsConvert.ConvertForbiddenZone(raw.Item1, 0, sampleCenter, sampleRadius, out _);
            var native = zones[i].Convert(f.Center, f.M.Definition.SampleRadius).ToArray();
            Check(converted.Count == native.Length, $"{step} converted {converted.Count}/{native.Length}.");
            for (var x = f.Center.X - 31; x < f.Center.X + 31; x += .91f)
                for (var z = f.Center.Z - 31; z < f.Center.Z + 31; z += .83f)
                {
                    var p = new Vector3(x, 0, z);
                    Check(converted.Any(c => c.Template.Resolve(c.Position, c.Rotation, null).Contains(p, 0))
                        == native.Any(c => c.Contains(p, 0)), $"{step} bridge conversion {x},{z}.");
                }
        }
        var hints = new BossComponent.TextHints();
        source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer)
            .Select(h => (h.Text, h.Risky))), step + " TextHints.");
    }

    private static void HoundsStandard()
    {
        foreach (var rule in BozjaCatalog.Definitions[2].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
        {
            using var f = new Fixture(2);
            var source = f.Activate<BossMod.Components.GenericAOEs>(rule.Component);
            foreach (var action in rule.Actions)
            {
                var spell = f.Start(source, action);
                CompareHounds(f, source, rule.Component + "/" + action);
                f.Now = f.Now.AddSeconds(20); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
                CompareHounds(f, source, rule.Component + "/overdue");
                source.OnCastFinished(f.W.Actors.Find(2)!, spell);
                f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
                f.Set(2, a => a with { Cast = null }); f.W.Actors.Find(2)!.CastInfo = null;
                f.Sync();
                CompareHounds(f, source, rule.Component + "/finished");
            }
        }
    }

    private static ActorCastInfo HoundsStart(Fixture f, BossComponent source, uint action, ulong id,
        Vector3 origin, Vector3 location, float total = 3)
    {
        f.Move(id, origin);
        var spell = new ActorCastInfo { Action = new(ActionType.Spell, action), Location = location,
            Rotation = new(.731f), TotalTime = total, TargetID = 100 };
        f.W.Actors.Find(id)!.CastInfo = spell;
        source.OnCastStarted(f.W.Actors.Find(id)!, spell);
        f.Set(id, a => a with
        {
            Cast = new(action, 100, location, .731f, f.Now, f.Now.AddSeconds(total + .3f), false, false)
                { IsSpell = true, TotalTime = total },
        });
        f.Sync();
        return spell;
    }

    private static void HoundsPounce()
    {
        using var f = new Fixture(2);
        var source = f.Activate<BossMod.Components.GenericAOEs>("Hellpounce");
        f.Add(3, 0x233C, f.Center); f.Sync();
        var first = HoundsStart(f, source, 20539, 2, f.Center + new Vector3(.123f, 0, .257f),
            f.Center + new Vector3(16.37f, 0, 7.39f));
        CompareHounds(f, source, "pounce start");
        var second = HoundsStart(f, source, 20540, 3, f.Center, f.Center + new Vector3(-9, 0, 8));
        CompareHounds(f, source, "pounce overwrite");
        f.Now = f.Now.AddSeconds(2); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        source.OnCastFinished(f.W.Actors.Find(2)!, first);
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareHounds(f, source, "pounce unrelated first finish");
        Check(f.M.Casts.Single().Activation == f.Now.AddSeconds(3.7d), "Return must use callback time.");
        source.OnCastFinished(f.W.Actors.Find(3)!, second);
        f.M.Finish(f.M.Actors[3], f.M.Actors[3].Cast!);
        CompareHounds(f, source, "pounce second clear");
        source.OnCastFinished(f.W.Actors.Find(2)!, first);
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareHounds(f, source, "pounce empty first finish");
        foreach (var offset in new[] { Vector3.Zero, new Vector3(.123f, 0, .257f) })
        {
            var origin = f.Center + offset;
            HoundsStart(f, source, 20539, 2, origin, origin);
            CompareHounds(f, source, "pounce zero " + offset);
            var cast = f.M.Visible().Single().Cast;
            Check(!cast.Drawable && cast.Shape is RayRectangleTemplate { Length: 0 }, "Zero charge source model lost.");
            var raw = source.ActiveAOEs(0, f.Viewer)[0];
            Check(raw.Check(new(cast.Origin.X, cast.Origin.Z)) == cast.Contains(cast.Origin), "Zero charge exact line hint.");
            var sink = new MechanicDrawSink(new());
            using (MechanicDrawSink.Install(sink)) source.DrawArenaBackground(0, f.Viewer);
            var commands = sink.Commands.OfType<MechanicDrawZoneRect>().ToArray();
            Check(commands.Length == 1, "Zero charge raw drawing command lost.");
            var shapes = new List<MechanicPreviewShape>();
            var lines = new List<MechanicPreviewLine>();
            var extras = new List<MechanicRadarExtra>();
            var skipped = MechanicDrawConvert.Convert(sink.Commands, 0, shapes, lines, extras);
            Check(shapes.Count == 0 && skipped == 1, "Zero charge actual draw bridge refusal boundary.");
        }
    }

    private static void HoundsQuake()
    {
        using var f = new Fixture(2);
        f.Add(3, 0x233C, f.Center); f.Add(4, 0x233C, f.Center); f.Sync();
        var source = f.Activate<BossMod.Components.GenericAOEs>("VoidQuake");
        var outer = HoundsStart(f, source, 20551, 2, f.Center, f.Center + new Vector3(.123f, 0, .257f), 9);
        HoundsStart(f, source, 20550, 3, f.Center, f.Center, 3);
        HoundsStart(f, source, 20549, 4, f.Center, f.Center, 6);
        CompareHounds(f, source, "quake reverse clock");
        Check(f.M.Casts.Count == 3 && f.M.Visible().Count() == 1, "Quake hidden tail must remain.");
        var other = new ActorCastInfo { Action = new(ActionType.Spell, 20549) };
        source.OnCastFinished(f.W.Actors.Find(1)!, other);
        f.M.Finish(f.M.Actors[1], new(20549, 0, default, 0, f.Now, f.Now, false, false));
        CompareHounds(f, source, "quake cross-source finish");
        Check(f.M.Visible().Single().Cast.Shape is DonutTemplate { InnerRadius: 10 }, "Quake FIFO promotion.");
        // Real OpCastInfo replacement emits old Finish before new Start.
        // Replace B although A is the global head; both must pop A.
        var replacement = new ActorCastInfo { Action = new(ActionType.Spell, 20551), TotalTime = 2,
            Location = f.Center, Rotation = new(.2f) };
        f.W.Execute(new ActorState.OpCastInfo(4, replacement));
        f.Set(2, a => a with { Cast = f.M.Actors[2].Cast });
        f.Set(3, a => a with { Cast = f.M.Actors[3].Cast });
        f.Set(4, a => a with { Cast = new(20551, 0, f.Center, .2f, f.Now, f.Now.AddSeconds(2.3f), false, false)
            { IsSpell = true, TotalTime = 2 } });
        f.Sync();
        CompareHounds(f, source, "quake real cast replacement");
        for (var i = 0; i < 5; ++i)
        {
            source.OnCastFinished(f.W.Actors.Find(2)!, outer);
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareHounds(f, source, "quake finish " + i);
        }
    }

    private static void HoundsHint()
    {
        using var f = new Fixture(2);
        var source = f.Activate<BossMod.Components.CastHint>("VoidTornado");
        var spell = f.Start(source, 20546);
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        void Compare(string step)
        {
            var global = new BossComponent.GlobalHints(); source.AddGlobalHints(global);
            Check(global.SequenceEqual(f.M.GlobalHints()), step + " global hints.");
            Check(!f.M.Visible().Any() && !f.M.Navigation(f.NativeViewer).Any()
                && !f.M.Damage(f.NativeViewer).Any(), step + " invented AOE/nav/damage.");
        }
        Compare("hint duplicate");
        source.OnCastFinished(f.W.Actors.Find(1)!, spell);
        f.M.Finish(f.M.Actors[1], f.M.Actors[2].Cast!);
        Compare("hint wrong reference");
        for (var i = 0; i < 2; ++i)
        {
            source.OnCastFinished(f.W.Actors.Find(2)!, spell);
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            Compare("hint own Finish " + i);
        }
    }

    private static void HoundsCold()
    {
        foreach (var reverse in new[] { false, true })
        foreach (var name in new[] { "Hellpounce", "VoidQuake" })
        {
            using var f = new Fixture(2, reverse);
            if (!reverse) f.Add(3, 0x233C, f.Center + new Vector3(3, 0, 0));
            foreach (var (id, total) in new[] { (2ul, 9f), (3ul, 2f) })
            {
                var action = (name, id) switch
                {
                    ("Hellpounce", 2) => 20539u,
                    ("Hellpounce", _) => 20540u,
                    (_, 2) => 20551u,
                    _ => 20549u,
                };
                var location = f.Center + new Vector3(id == 2 ? 8 : -8, 0, 0);
                f.W.Actors.Find(id)!.CastInfo = new() { Action = new(ActionType.Spell, action),
                    TotalTime = total, Location = location, Rotation = new(.2f) };
                f.Set(id, a => a with { Cast = new(action, 0, location, .2f, START, START.AddSeconds(total + .3f), false, false)
                    { IsSpell = true, TotalTime = total } });
            }
            var castOrder = f.W.Actors.Where(a => a.CastInfo != null).Select(a => a.InstanceID).ToArray();
            Check(castOrder.SequenceEqual(reverse ? new ulong[] { 3, 2 } : [2ul, 3ul]), "Cold helper order not constructed.");
            var cold = f.Activate<BossMod.Components.GenericAOEs>(name);
            var model = new BozjaEncounter(f.M.Definition);
            var ordered = f.Actors.ToArray();
            model.Synchronize(ordered, f.Party.ToArray(), 100, f.Now);
            var e = cold.ActiveAOEs(0, f.Viewer)[0];
            var a = model.Visible().Single().Cast;
            Check(e.Origin.X == a.Origin.X && e.Origin.Z == a.Origin.Z && e.Activation == a.Activation,
                name + " cold source actor order.");
            foreach (var id in new ulong[] { 2, 3 })
            {
                f.W.Actors.Find(id)!.CastInfo = null;
                f.Set(id, value => value with { Cast = null });
            }
            cold = f.Activate<BossMod.Components.GenericAOEs>(name);
            model = new(f.M.Definition);
            model.Synchronize(f.Actors, f.Party.ToArray(), 100, f.Now);
            Check(cold.ActiveAOEs(0, f.Viewer).Length == 0 && !model.Visible().Any(),
                name + " cold history gap invented output.");
        }
        using var arena = new Fixture(2);
        for (var x = 125f; x <= 183; x += .73f)
            for (var z = 756f; z <= 814; z += .67f)
                Check(arena.Source.Arena.InBounds(new(x, z)) == arena.M.Definition.Arena.Contains(new(x, 0, z), 0),
                    $"Hounds polygon {x},{z}.");
        foreach (var distance in new[] { 24.99f, 25f, 25.01f, 29f })
        {
            var m = new BozjaEncounter(arena.M.Definition);
            m.Synchronize(arena.Actors.Select(a => a.Id == 100 ? a with { Position = arena.Center + new Vector3(distance, 0, 0) } : a),
                arena.Party.ToArray(), 100, START);
            Check(m.Active == (distance <= 25), "Hounds pull radius.");
        }
    }

    private static void HoundsClient()
    {
        Initialize();
        var d = BozjaCatalog.Definitions[2];
        Check(Service.LuminaRow<Lumina.Excel.Sheets.DynamicEvent>(2)?.Name.ToString() == d.Name, "Hounds DynamicEvent.");
        uint[] actions = [20534, 20535, 20538, 20539, 20540, 20542, 20544, 20545, 20546, 20549, 20550, 20551, 20553];
        Check(d.Rules.SelectMany(r => r.Actions).Order().SequenceEqual(actions.Order()), "Hounds thirteen actions.");
        Check(actions.All(a => new ActionID(ActionType.Spell, a).CastTimeExtra() == 0), "Hounds extra.");
        Check(d.Rules.Length == 8 && d.PullRadius == 25 && d.SampleRadius == 24.5f, "Hounds manifest.");
    }

    private static void HoundsRegion()
    {
        Initialize();
        using var session = new MechanicSession("hounds-manager-boundary");
        var feed = new MechanicWorldFeed(session);
        var model = new BozjaRegion();
        var center = BozjaCatalog.Definitions[2].Center;
        var hound = new ReplayActorState { Id = 12, Oid = 0x2E66, Kind = 2, SubKind = 5, Hostile = true,
            Targetable = true, InCombat = true, Hp = 100, MaxHp = 100, Position = center };
        var vigil = hound with { Id = 14, Oid = 0x2DBD, Position = CENTER };
        var player = new ReplayActorState { Id = 100, Kind = 1, Hp = 100, MaxHp = 100,
            Position = center + new Vector3(26, 0, 0) };
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        void Apply(string step, params ReplayActorState[] values)
        {
            session.BeginFrame(START);
            feed.ApplyFrame(new(920, 735, 100, 0, [new(0, 100, 100, "")], [],
                values.Select((v, i) => (MechanicActorSnapshot)converter.Invoke(null, [v, i, START])!).ToArray()));
            session.Update();
            model.Synchronize(values, [new(0, "", 100)], 100, START, true);
            Check(session.BossModules.LoadedModules.Select(m => m.PrimaryActor.InstanceID)
                .SequenceEqual(model.Loaded.Select(m => m.Primary!.Actor.Id)), step + " loaded.");
            Check(session.BossModules.ActiveModule?.PrimaryActor.InstanceID == model.Selected?.Primary?.Actor.Id,
                step + " selected.");
        }
        Apply("cold26", player, hound, vigil);
        Check(model.Selected?.Definition.PrimaryOid == 0x2DBD && !model.Encounters[2].Active, "26y falsely pulled.");
        player = player with { Position = center };
        Apply("pull25", player, hound, vigil);
        var version = model.Encounters[2].LoadVersion;
        Apply("destroy with source missing", vigil);
        Apply("same ID rebind before manager update", player, hound, vigil);
        Check(model.Encounters[2].LoadVersion == version, "Frozen manager rebuilt instead of rebinding.");
        Apply("destroy with source present", player, vigil);
        Apply("new module after retirement", player, hound, vigil);
        Check(model.Encounters[2].LoadVersion > version, "Retired primary failed new module.");
    }
}
