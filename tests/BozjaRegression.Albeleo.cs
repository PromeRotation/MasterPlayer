using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private static void RunAlbeleo(Action<string, Action> run)
    {
        run("Bozja pending CLL2 client six components full Feed cold activation and exact arena", AlbeleoManifest);
        foreach (var rule in BozjaCatalog.Definitions[34].Rules)
            run("Bozja pending CLL2 real Feed differential " + rule.Component, () => AlbeleoCasts(rule));
        run("Bozja pending CLL2 exact 35 range gaze original eye and watched Finish", AlbeleoGaze);
        run("Bozja pending CLL2 HP1 retreat HP0 alive retained default pull and original primary", AlbeleoLifecycle);
        run("Bozja pending CLL2 source all potential targets priority and interrupt diagnostic", AlbeleoTargets);
        run("Bozja pending CLL2 original interrupt references versus ID AOE consumption", AlbeleoReferences);
        run("Bozja pending CLL2 actual DrawEnemies capture Actor markers old bridge skips", AlbeleoMarkers);
        run("Bozja pending CLL2 rotated ray exact source origin versus legacy shifted rectangle boundary", AlbeleoRayBoundary);
    }

    private sealed class AlbeleoFixture : IDisposable
    {
        internal readonly MechanicSession Session;
        private readonly MechanicWorldFeed feed;
        internal readonly BozjaEncounter Native = new(BozjaCatalog.Definitions[34]);
        internal readonly List<ReplayActorState> Actors = [];
        internal Vector3 Center => Native.Definition.Center;
        internal BossModule Source => Session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x2F06);
        internal Actor Viewer => Session.World.Actors.Find(100)!;
        internal BozjaActor NativeViewer => Native.Actors[100];
        private readonly MethodInfo converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.Static | BindingFlags.NonPublic)!;

        internal AlbeleoFixture()
        {
            Initialize();
            Session = new("CLL2-source-differential");
            feed = new(Session);
            Actors.Add(new() { Id = 1, Oid = 0x2F06, Kind = 2, SubKind = 5, Position = Center,
                Hp = 1000, Targetable = true, InCombat = true, HitboxRadius = .5f });
            Actors.Add(new() { Id = 100, Kind = 1, Position = Center, Hp = 1000 });
            Actors.Add(new() { Id = 20, Oid = 0x2EE3, Kind = 2, SubKind = 5, Position = Center,
                Hp = 1000, Targetable = true, IsEnemy = true, HitboxRadius = .5f });
        }

        internal void Change(ulong id, Func<ReplayActorState, ReplayActorState> change)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = change(Actors[i]);
        }

        internal ReplayCast Spell(uint action, double at = 0, float total = 5)
            => new(action, 100, Center, .4f, START.AddSeconds(at), START.AddSeconds(at + total + .3f), false, false)
            { IsSpell = true, TotalTime = total };

        internal void Frame(double at, MechanicFeedEvent? ev = null, Action? nativeEvent = null, bool compare = true)
        {
            var now = START.AddSeconds(at);
            Session.BeginFrame(now);
            feed.ApplyFrame(new(920, 735, 100, 0, [new(0, 100, 100, "")], [],
                Actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            if (ev != null) feed.Enqueue(ev);
            feed.Flush();
            Session.Update();
            Native.Synchronize(Actors, [new(0, "", 100)], 100, now, update: false);
            nativeEvent?.Invoke();
            Native.Update(now);
            if (compare) Compare();
        }

        internal void Compare()
        {
            Check(Source.Components.Select(c => c.GetType().Name).SequenceEqual(Native.ActiveRules.Select(r => r.Component)),
                "CLL2 active six components and activation order");
            foreach (var component in Source.Components.OfType<GenericAOEs>())
            {
                var expected = component.ActiveAOEs(0, Viewer).ToArray();
                var actual = Native.Visible(NativeViewer).Where(v => v.Cast.Rule.Component == component.GetType().Name).ToArray();
                Check(expected.Length == actual.Length, "CLL2 AOE count " + component.GetType().Name);
                for (var i = 0; i < expected.Length; ++i)
                {
                    var e = expected[i]; var a = actual[i];
                    Check(e.Origin.ToVec3() == a.Cast.Origin && e.Rotation.Rad == a.Cast.Rotation
                        && e.Activation == a.Cast.Activation && e.Risky == a.Risky && (e.Color == Colors.Danger) == a.Danger,
                        "CLL2 shape fields and queue " + component.GetType().Name);
                    for (var x = -42f; x <= 42; x += 1.371f)
                        for (var z = -42f; z <= 42; z += 2.719f)
                        {
                            var p = Center + new Vector3(x, 0, z);
                            Check(e.Check(new(p.X, p.Z)) == a.Cast.Contains(p), "CLL2 shape contains " + p);
                        }
                }
            }
            var hints = new BossComponent.TextHints();
            var ai = new AIHints();
            foreach (var c in Source.Components) { c.AddHints(0, Viewer, hints); c.AddAIHints(0, Viewer, default, ai); }
            ai.Normalize();
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(Native.Hints(NativeViewer).Select(h => (h.Text, h.Risky))),
                "CLL2 ordered hints");
            Check(Source.CalculateGlobalHints().SequenceEqual(Native.GlobalHints()), "CLL2 global interrupt hint");
            Check(ai.ForbiddenDirections.Select(d => (d.Item1.Rad, d.Item2.Rad, d.Item3))
                .SequenceEqual(Native.GazeDirections(NativeViewer.Actor)), "CLL2 exact gaze directions range and clock");
            var zones = Native.NavigationForHost(NativeViewer);
            Check(ai.ForbiddenZones.Count == zones.Length, "CLL2 navigation count");
            for (var i = 0; i < zones.Length; ++i)
            {
                Check(ai.ForbiddenZones[i].Item2 == zones[i].Activation, "CLL2 zone activation");
                var expected = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[i].Item1, 0,
                    Center, Source.Arena.Bounds.Radius, out _);
                var actual = zones[i].Convert(Center, Native.SampleRadius).ToArray();
                Check(expected.Count == actual.Length, "CLL2 real host conversion count");
                for (var x = -35f; x < 35; x += 2.713f)
                    for (var z = -35f; z < 35; z += 3.119f)
                    {
                        var p = Center + new Vector3(x, 0, z);
                        Check(expected.Any(g => g.Template.Resolve(g.Position, g.Rotation, null).Contains(p, 0))
                            == actual.Any(g => g.Contains(p, 0)), "CLL2 converted navigation " + p);
                    }
            }
            Check(ai.PredictedDamage.Count == 0 && !Native.DamageForHost(NativeViewer).Any(), "CLL2 no invented damage");
        }

        public void Dispose() => Session.Dispose();
    }

    private static void AlbeleoManifest()
    {
        using var f = new AlbeleoFixture();
        f.Frame(0);
        var name = Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(9433)?.Singular.ToString();
        Console.WriteLine("INFO CLL2 BNpcName9433=" + name);
        Check(name == BozjaCatalog.Definitions[34].Name, "CLL2 actual client display name");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(735)?.TerritoryType.RowId == 920
            && Service.LuminaRow<Lumina.Excel.Sheets.TerritoryType>(920)?.ContentFinderCondition.RowId == 735, "CLL2 actual CFC territory");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.CastrumLacusLitore.CLL2AlbeleoTheMaleficient.AID>())
            Check(new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CLL2 client action extra " + action);
        Check(f.Native.Center == f.Source.Arena.Center.ToVec3() && f.Native.SampleRadius == f.Source.Arena.Bounds.Radius, "CLL2 exact arena center and radius");
        var converted = MechanicHintsConvert.ConvertBounds(f.Source.Arena.Bounds, f.Source.Arena.Center, 0)!;
        for (var x = -31f; x <= 31; x += .317f)
            for (var z = -35f; z <= 35; z += .431f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                Check(converted.Contains(p, 0) == f.Native.Arena.Contains(p, 0), "CLL2 real irregular corridor arena " + p);
            }
        foreach (var vertex in converted.GetContours().SelectMany(c => c))
            foreach (var x in new[] { MathF.BitDecrement(vertex.X), vertex.X, MathF.BitIncrement(vertex.X) })
                foreach (var z in new[] { MathF.BitDecrement(vertex.Z), vertex.Z, MathF.BitIncrement(vertex.Z) })
                    Check(converted.Contains(new(x, 0, z), 0) == f.Native.Arena.Contains(new(x, 0, z), 0),
                        "CLL2 every corridor turn adjacent ULP");
        Check(f.Native.Arena.Contains(new(80, 0, -380), 0) && !f.Native.Arena.Contains(new(100, 0, -380), 0),
            "CLL2 corridor concavity not bounding rectangle");
        Check(BozjaCatalog.Owns(920, 0x2F06) && !BozjaCatalog.Owns(975, 0x2F06)
            && !BozjaCatalog.Owns(920, 0x2F07), "CLL2 exact primary ownership");
    }

    private static void AlbeleoCasts(BozjaRule rule)
    {
        foreach (var cold in new[] { false, true })
        {
            using var f = new AlbeleoFixture();
            if (!cold) f.Frame(0);
            f.Change(20, a => a with { Cast = f.Spell(rule.Actions[0], cold ? 0 : .5) });
            f.Frame(cold ? 0 : .5);
            Check(rule.Kind == BozjaRuleKind.CastGaze ? f.Native.Eyes.Count == 1
                : f.Native.Casts.Count == 1, "CLL2 cold or hot cast actually observed");
            f.Frame(1, new MechanicEventActionEffect(20, 1, rule.Actions[0], 100, 0, f.Center, 0, 0, 0, []),
                () => f.Native.Effect(20, rule.Actions[0], 100, f.Center));
            f.Change(20, a => a with { Position = a.Position + Vector3.UnitX * 11, Rotation = 2 });
            f.Frame(99);
            f.Change(20, a => a with { Cast = null });
            f.Frame(100);
            Check(f.Native.Eyes.Count == 0 && f.Native.Casts.Count == 0, "CLL2 only actual Finish clears");
        }
    }

    private static void AlbeleoGaze()
    {
        using var f = new Fixture(34);
        var source = f.Activate<CastGaze>("BalefulGaze");
        var spell = f.Start(source, 21508);
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        foreach (var distance in new[] { 0f, MathF.BitDecrement(35f), 35f, MathF.BitIncrement(35f), 36f })
        {
            f.Move(100, f.Center - Vector3.UnitX * distance);
            var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))), "CLL2 exact range hints " + distance);
            Check(ai.ForbiddenDirections.Select(d => (d.Item1.Rad, d.Item2.Rad, d.Item3))
                .SequenceEqual(f.M.GazeDirections(f.NativeViewer.Actor)), "CLL2 range directions " + distance);
            Check(f.M.Eyes.All(e => e.Range == 35), "CLL2 eye keeps finite range outside range");
        }
        source.OnCastFinished(f.W.Actors.Find(2)!, new() { Action = new(ActionType.Spell, 21509) });
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast! with { ActionId = 21509 });
        Check(source.Eyes.Count == 2 && f.M.Eyes.Count == 2, "CLL2 unrelated Finish retains duplicates");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Check(source.Eyes.Count == 1 && f.M.Eyes.Count == 1, "CLL2 watched Finish removes first matching ID");
    }

    private static void AlbeleoLifecycle()
    {
        foreach (var hp in new uint[] { 0, 1, 2 })
        {
            using var f = new AlbeleoFixture();
            f.Change(100, a => a with { Position = f.Center + Vector3.UnitX * 100 });
            f.Frame(0);
            Check(f.Native.Active, "CLL2 default pull has no arena proximity gate");
            var source = f.Source;
            f.Change(1, a => a with { Hp = hp });
            f.Frame(1, compare: hp != 1);
            Check(f.Native.Active == (hp != 1), "CLL2 HP1 retreat exact predicate " + hp);
            Check((source.StateMachine.ActiveState != null) == f.Native.Active, "CLL2 source completion parity " + hp);
        }
        using var cold = new AlbeleoFixture();
        cold.Change(1, a => a with { Hp = 1 });
        cold.Frame(0, compare: false);
        Check(!cold.Native.Active && cold.Native.Completed && cold.Source.StateMachine.ActiveState == null,
            "CLL2 HP1 first pull enters and completes in same Update");
        cold.Change(1, a => a with { Hp = 1000 });
        cold.Frame(1, compare: false);
        Check(!cold.Native.Active, "CLL2 HP1 cold completion cannot repull same original");
        using var passive = new AlbeleoFixture();
        passive.Frame(0);
        passive.Change(1, a => a with { InCombat = false, Targetable = false });
        passive.Frame(1);
        Check(passive.Native.Active, "CLL2 no inferred combat reset");
    }

    private static void AlbeleoTargets()
    {
        using var f = new Fixture(34);
        var ai = new AIHints();
        foreach (var actor in f.W.Actors)
            ai.PotentialTargets.Add(new(actor, 0, false));
        f.Source.GetType().GetMethod("CalculateModuleAIHints", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(f.Source, [0, f.Viewer, default(PartyRolesConfig.Assignment), ai]);
        foreach (var enemy in ai.PotentialTargets)
            Check(enemy.Priority == f.M.TargetPriority(f.Actors.Single(a => a.Id == enemy.Actor.InstanceID)),
                "CLL2 all source potential targets receive priority");
        f.Set(1, a => a with { IsEnemy = true, ObjectIndex = 2 });
        f.Set(2, a => a with { IsEnemy = true, ObjectIndex = 4 });
        f.Sync();
        Check(QuestTargetPriority.Select(920, f.Actors, 1, f.Center, assignedPriority: f.M.TargetPriority) == 2,
            "CLL2 legal unknown add higher priority than boss");
        Check(QuestTargetPriority.Select(920, f.Actors.Select(a => a.Id == 2 ? a with { Hp = 1 } : a),
            2, f.Center, assignedPriority: f.M.TargetPriority) == 1, "CLL2 Pointless protected before priority setter");
        var source = f.Activate<CastInterruptHint>("AbyssalCry");
        f.Start(source, 21510);
        var hints = new AIHints();
        var caster = f.W.Actors.Find(2)!;
        var interruptEnemy = new AIHints.Enemy(caster, 0, false);
        hints.PotentialTargets.Add(interruptEnemy); hints.Enemies[caster.CharacterSpawnIndex] = interruptEnemy;
        source.AddAIHints(0, f.Viewer, default, hints);
        Check(hints.PotentialTargets[0].ShouldBeInterrupted && f.M.Casts.Single().Source.Actor.Id == 2,
            "CLL2 original interrupt caster diagnostic only");
    }

    private static void AlbeleoReferences()
    {
        using var f = new Fixture(34);
        var source = f.Activate<CastInterruptHint>("AbyssalCry");
        var spell = f.Start(source, 21510);
        var original = f.W.Actors.Find(2)!;
        source.OnCastStarted(original, spell); f.M.Start(f.M.Actors[2]);
        f.Remove(2);
        Check(source.Casters.Count == 1 && f.M.Casts.Count == 1, "CLL2 destroy real Finish removes one duplicate");
        f.Add(2, 0x2F0A, f.Center); f.Sync();
        var replacement = f.Start(source, 21510);
        source.OnCastFinished(f.W.Actors.Find(2)!, replacement);
        f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Check(source.Casters.Count == 1 && ReferenceEquals(source.Casters[0], original)
            && f.M.Casts.Count == 1 && !ReferenceEquals(f.M.Casts[0].Source, f.M.Actors[2]),
            "CLL2 interrupt Finish must not consume same ID old reference");
        Check(f.M.GlobalHints().SequenceEqual(new[] { "Interrupt!" }), "CLL2 cached hint fixed after replacement");
    }

    private static void AlbeleoMarkers()
    {
        using var f = new Fixture(34);
        f.Add(30, 0x2F07, f.Center);
        f.Add(31, 0x2EDE, f.Center + Vector3.UnitX * 3);
        var sink = new MechanicDrawSink(new());
        using (MechanicDrawSink.Install(sink))
            f.Source.GetType().GetMethod("DrawEnemies", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(f.Source, [0, f.Viewer]);
        var markers = sink.Commands.OfType<MechanicDrawActorMarker>().ToArray();
        Check(markers.Length == 2 && sink.Commands.Count == 2, "CLL2 source primary and listed add only, Predator omitted");
        var shapes = new List<MechanicPreviewShape>();
        var lines = new List<MechanicPreviewLine>();
        var extras = new List<MechanicRadarExtra>();
        Check(MechanicDrawConvert.Convert(sink.Commands, 0, shapes, lines, extras) == 2
            && shapes.Count == 0 && lines.Count == 0 && extras.Count == 0,
            "CLL2 real old drawing bridge does not materialize Actor markers");
    }

    private static void AlbeleoRayBoundary()
    {
        using var f = new Fixture(34);
        var source = f.Activate<GenericAOEs>("MagitekRay");
        var caster = f.W.Actors.Find(2)!;
        var spell = new ActorCastInfo { Action = new(ActionType.Spell, 21502), TotalTime = 5,
            Location = f.Center, Rotation = new(3f), TargetID = 100 };
        caster.CastInfo = spell;
        source.OnCastStarted(caster, spell);
        f.Set(2, a => a with { Cast = new(21502, 100, f.Center, 3f, START, START.AddSeconds(5 + .3f), false, false)
            { IsSpell = true, TotalTime = 5 } });
        f.Sync();
        var raw = source.ActiveAOEs(0, f.Viewer).ToArray().Single();
        var native = f.M.Visible(f.NativeViewer).Single().Cast;
        Check(raw.Check(new(f.Center.X, f.Center.Z)) && native.Contains(f.Center),
            "CLL2 source and native ray include original zero-local point");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        var oldNavigation = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones.Single().Item1, 0,
            f.Center, f.M.SampleRadius, out _);
        var nativeNavigation = f.M.NavigationForHost(f.NativeViewer).Single().Convert(f.Center, f.M.SampleRadius).Single();
        Check(!oldNavigation.Single().Template.Resolve(oldNavigation[0].Position, oldNavigation[0].Rotation, null).Contains(f.Center, 0)
            && nativeNavigation.Contains(f.Center, 0), "CLL2 actual legacy center-shift rounding differs at original ray rear edge");
        var sink = new MechanicDrawSink(new());
        using (MechanicDrawSink.Install(sink)) source.DrawArenaBackground(0, f.Viewer);
        var shapes = new List<MechanicPreviewShape>();
        MechanicDrawConvert.Convert(sink.Commands, 0, shapes, [], []);
        Check(shapes.Count == 1 && !shapes[0].Shape.Contains(f.Center, 0),
            "CLL2 actual old drawing rectangle repeats center-shift rounding");
        Console.WriteLine("INFO CLL2 ray rotation3 original point: source raw/native=True, legacy navigation/draw=False; native preserves existing raw Ray adaptation");
    }
}
