using System.Numerics;
using BossMod;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

internal static partial class BozjaRegression
{
    private static void RunHoof(Action<string, Action> run)
    {
        run("Bozja native CE34 client table ordinary shapes actual bounds source extra", HoofStandard);
        run("Bozja native CE34 Arena Finish clock overwrite Created shrink cold actor order", HoofArena);
        run("Bozja native CE34 Glimmer original birth point exact ten Effects first position no TTL", HoofGlimmer);
        run("Bozja native CE34 Gaze exact eyes inversion hints directions draw lines facing", HoofGaze);
        run("Bozja native CE34 Gaze cold source ordering empty inversion Finish resets", HoofGazeCold);
        run("Bozja native CE34 BullHorn original target slot fixed clock Finish retained Effect FIFO", HoofDelay);
        run("Bozja native CE34 R8 min8 stack raw converted navigation actual source", HoofStacks);
        run("Bozja native CE34 module priority legal candidates pending dead and nearest current", HoofTarget);
        run("Bozja native CE34 cold dead untargetable Glimmer no capacity limit current callback clock", HoofGlimmerCold);
    }

    private static void HoofStandard()
    {
        Initialize();
        Console.WriteLine("INFO CE34 DynamicEvent11=" + data!.GetExcelSheet<Lumina.Excel.Sheets.DynamicEvent>()!.GetRow(11).Name);
        Check(data.GetExcelSheet<Lumina.Excel.Sheets.DynamicEvent>()!.GetRow(11).Name.ToString()
            == BozjaCatalog.Definitions[7].Name, "CE34 actual client name.");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.CriticalEngagement.CE34TrampledUnderHoof.AID>())
            Check(new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE34 actual extra " + action);
        foreach (var rule in BozjaCatalog.Definitions[7].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
        {
            using var f = new Fixture(7);
            var source = f.Activate<BossMod.Components.GenericAOEs>(rule.Component);
            var spell = f.Start(source, rule.Actions[0]);
            CompareMetalFoxBits(f, source, rule.Component, "CE34 Start");
            f.Now = START.AddSeconds(99); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareMetalFoxBits(f, source, rule.Component, "CE34 no TTL");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, rule.Component, "CE34 Finish");
        }
        using var arena = new Fixture(7);
        var converted = MechanicHintsConvert.ConvertBounds(arena.Source.Arena.Bounds, arena.Source.Arena.Center, 0)!;
        foreach (var angle in Enumerable.Range(0, 360).Select(i => i * MathF.PI / 180))
            foreach (var radius in new[] { 24f, 24.4f, 24.5f, 24.6f, 25f })
            {
                var p = arena.Center + new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * radius;
                Check(converted.Contains(p, 0) == arena.M.Arena.Contains(p, 0), "CE34 actual 32gon bounds: "
                    + $"p={p}, expected={converted.Contains(p, 0)}, actual={arena.M.Arena.Contains(p, 0)}, "
                    + $"source center={arena.Source.Arena.Center}, native center={arena.Center}, "
                    + $"expected={string.Join(';', converted.GetContours().SelectMany(c => c))}, "
                    + $"actual={string.Join(';', arena.M.Arena.GetContours().SelectMany(c => c))}");
            }
    }

    private static void HoofArena()
    {
        using var f = new Fixture(7);
        var source = f.Activate<BossMod.Components.GenericAOEs>("ArenaChange");
        var spell = f.Start(source, 20968);
        CompareMetalFoxBits(f, source, "ArenaChange", "no prediction at Start");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareMetalFoxBits(f, source, "ArenaChange", "Finish plus6");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareMetalFoxBits(f, source, "ArenaChange", "replacement not duplicate");
        f.Add(30, 0x1EB030, f.Center); f.Sync();
        CompareMetalFoxBits(f, source, "ArenaChange", "Created clears");
        var expected = MechanicHintsConvert.ConvertBounds(f.Source.Arena.Bounds, f.Source.Arena.Center, 0)!;
        Check(f.M.Arena is CircleGeometry { Radius: 20 } && f.M.Center == f.Source.Arena.Center.ToVec3(),
            "CE34 shrink to20 and quantized center.");
        for (var x = -21f; x <= 21; x += .71f)
            for (var z = -21f; z <= 21; z += .63f)
                Check(expected.Contains(f.Center + new Vector3(x, 0, z), 0)
                    == f.M.Arena.Contains(f.Center + new Vector3(x, 0, z), 0), "CE34 true circle contains.");
        f.Remove(30); Check(f.M.Arena is CircleGeometry { Radius: 20 }, "CE34 wall destruction does not restore arena.");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareMetalFoxBits(f, source, "ArenaChange", "Finish after wall can restore donut");
    }

    private static void HoofGlimmer()
    {
        using var f = new Fixture(7);
        var source = f.Activate<BossMod.Components.GenericAOEs>("GlimmerInTheDark");
        var p = f.Center + new Vector3(2.123f, 0, 3.257f);
        f.Add(30, 0x2ED1, p); f.Sync();
        f.Add(31, 0x2ED1, p); f.Sync();
        CompareMetalFoxBits(f, source, "GlimmerInTheDark", "duplicate births");
        f.Move(30, f.Center + new Vector3(10, 0, 0));
        source.OnEventCast(f.W.Actors.Find(30)!, PatriotEvent(20963)); f.M.Effect(30, 20963);
        Check(f.M.Bursts.All(b => b.NumCasts == 0), "CE34 moved source cannot consume old point.");
        f.Move(30, p);
        for (var i = 1; i <= 10; ++i)
        {
            source.OnEventCast(f.W.Actors.Find(30)!, PatriotEvent(20963)); f.M.Effect(30, 20963);
            CompareMetalFoxBits(f, source, "GlimmerInTheDark", "ten Effects " + i);
        }
        Check(f.M.Bursts.Count == 1, "CE34 identical points consume only first.");
        f.Remove(30); f.Remove(31);
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CompareMetalFoxBits(f, source, "GlimmerInTheDark", "destroyed actors and expired prediction remain");
        f.Add(30, 0x2ED1, p); f.Sync();
        f.Add(32, 0x233C, f.M.Bursts[0].Cast.Origin + new Vector3(.09f, 0, .09f)); f.Sync();
        source.OnEventCast(f.W.Actors.Find(32)!, PatriotEvent(20963)); f.M.Effect(32, 20963);
        Check(f.M.Bursts[0].NumCasts == 1 && f.M.Bursts[1].NumCasts == 0,
            "CE34 arbitrary source square tolerance consumes old before reborn generation.");
        CompareMetalFoxBits(f, source, "GlimmerInTheDark", "same ID rebirth and arbitrary source");
    }

    private static void HoofGlimmerCold()
    {
        using var f = new Fixture(7);
        for (ulong id = 30; id <= 32; ++id)
        {
            var actor = f.Add(id, 0x2ED1, f.Center + new Vector3((id - 30) * 7, 0, 0));
            actor.IsDead = true;
            actor.IsTargetable = false;
            f.Set(id, a => a with { Dead = true, Targetable = false });
        }
        f.Now = START.AddSeconds(5); f.W.Frame.Timestamp = f.Now;
        f.M.Clear(); f.Sync();
        var source = f.Activate<BossMod.Components.GenericAOEs>("GlimmerInTheDark");
        CompareMetalFoxBits(f, source, "GlimmerInTheDark", "cold all existing dead actors");
        Check(f.M.Bursts.Count == 3 && f.M.Bursts.All(b => b.Cast.Activation == f.Now.AddSeconds(4.9d)),
            "CE34 cold callback clock and no two-entry cap.");
    }

    private static void CompareHoofGaze(Fixture f, BossMod.Components.GenericGaze source, string step)
    {
        var eyes = source.ActiveEyes(0, f.Viewer).ToArray();
        Check(eyes.Select(e => (e.Position.ToVec3(), e.Activation, e.Inverted))
            .SequenceEqual(f.M.Eyes.Select(e => (e.Position, e.Activation, e.Inverted))), step + " exact eyes.");
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
            step + " text.");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        Check(ai.ForbiddenDirections.Select(d => (d.Item1.Rad, d.Item2.Rad, d.Item3))
            .SequenceEqual(f.M.GazeDirections(f.NativeViewer.Actor)), step + " exact forbidden directions.");
        var sink = new MechanicDrawSink(new());
        using (MechanicDrawSink.Install(sink)) source.DrawArenaForeground(0, f.Viewer);
        var drawn = sink.Commands.OfType<MechanicDrawEye>().ToArray();
        Check(drawn.Select(e => e.Danger).SequenceEqual(f.M.Eyes.Select(e => BozjaEncounter.EyeDanger(e, f.NativeViewer.Actor))),
            step + " actual eye danger including coincident viewer.");
        var arcs = sink.Commands.OfType<MechanicDrawPolyline>().ToArray();
        var nativeArcs = f.M.Eyes.Where(e => BozjaEncounter.DistanceSquared(e.Position, f.NativeViewer.Actor.Position) <= 1e8f)
            .Select(e => BozjaEncounter.EyeArc(e, f.NativeViewer.Actor)).ToArray();
        Check(arcs.Length == nativeArcs.Length, step + " actual arc count.");
        for (var i = 0; i < arcs.Length; ++i)
            Check(arcs[i].Vertices.Select(p => p.ToVec3(f.NativeViewer.Actor.Position.Y)).SequenceEqual(nativeArcs[i]),
                step + " exact actual arc vertices.");
        var facing = f.M.Facing(f.NativeViewer.Actor);
        if (facing is { } rot)
            Check(f.M.GazeDirections(f.NativeViewer.Actor).Where(d => d.Activation <= f.Now.AddSeconds(.75))
                .All(d => MathF.Abs(MathF.Atan2(MathF.Sin(rot - d.Center), MathF.Cos(rot - d.Center))) > d.HalfWidth),
                step + " planned facing avoids all imminent directions.");
    }

    private static void HoofGaze()
    {
        using var f = new Fixture(7);
        var source = f.Activate<BossMod.Components.GenericGaze>("DemonEye");
        f.Move(2, f.Center + new Vector3(10, 12, 0));
        f.Start(source, 20962); CompareHoofGaze(f, source, "normal");
        f.Add(3, 0x233C, f.Center + new Vector3(-10, 0, 0)); f.Sync();
        f.Start(source, 20961, 3); CompareHoofGaze(f, source, "invert existing");
        f.Now = START.AddSeconds(2.6); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CompareHoofGaze(f, source, "imminent inverted facing");
        Check(f.M.Facing(f.NativeViewer.Actor) is { } imminent && MathF.Sin(imminent) >= .707107f,
            "CE34 single inverted eye forces facing toward original eye.");
        f.Add(4, 0x233C, f.Center + new Vector3(0, 0, 10)); f.Sync();
        f.Start(source, 20962, 4); CompareHoofGaze(f, source, "new eye inherits inversion");
        foreach (var rotation in new[] { 0f, MathF.PI / 4, MathF.PI, -MathF.PI / 2 })
        {
            f.W.Actors.Find(100)!.PosRot = new(f.Center, rotation);
            f.Set(100, a => a with { Rotation = rotation }); f.Sync();
            CompareHoofGaze(f, source, "viewer rotation " + rotation);
        }
        f.Move(100, f.Center + new Vector3(0, 0, 10)); CompareHoofGaze(f, source, "coincident inverted");
        source.OnCastFinished(f.W.Actors.Find(3)!, f.W.Actors.Find(3)!.CastInfo!);
        f.M.Finish(f.M.Actors[3], f.M.Actors[3].Cast!); CompareHoofGaze(f, source, "boss Finish retains eyes");
        source.OnCastFinished(f.W.Actors.Find(4)!, f.W.Actors.Find(4)!.CastInfo!);
        f.M.Finish(f.M.Actors[4], f.M.Actors[4].Cast!); CompareHoofGaze(f, source, "any false eye Finish clears all");
        f.W.Actors.Find(4)!.CastInfo = null; f.Set(4, a => a with { Cast = null }); f.Sync();
        f.Start(source, 20962, 4); CompareHoofGaze(f, source, "new eye after reset normal");
        f.Move(100, f.Center + new Vector3(0, 4, 10));
        CompareHoofGaze(f, source, "viewer Y adapts source XZ only arc");
    }

    private static void HoofGazeCold()
    {
        foreach (var helperFirst in new[] { false, true })
        {
            using var f = new Fixture(7, helperFirst);
            if (!helperFirst) f.Add(3, 0x233C, f.Center + new Vector3(0, 0, 10));
            foreach (var (id, action) in new[] { (2UL, 20961u), (3UL, 20962u) })
            {
                f.W.Actors.Find(id)!.CastInfo = new() { Action = new(ActionType.Spell, action), TotalTime = 4,
                    TargetID = 100, Location = f.W.Actors.Find(id)!.Position.ToVec3(), Rotation = new(.4f) };
                f.Set(id, a => a with { Cast = new(action, 100, a.Position, .4f, START, START.AddSeconds(4.3f), false, false)
                    { IsSpell = true, TotalTime = 4 } });
            }
            f.M.Clear(); f.Sync();
            var source = f.Activate<BossMod.Components.GenericGaze>("DemonEye");
            CompareHoofGaze(f, source, "cold order " + helperFirst);
        }
    }

    private static void HoofDelay()
    {
        using var f = new Fixture(7);
        var source = f.Activate<BossMod.Components.SingleTargetInstant>("BullHorn");
        var spell = f.Start(source, 20968);
        var locked = f.M.DelayedHits.Single();
        void Compare(string step)
        {
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(f.M.Damage(f.NativeViewer).Select(d => (d.Mask, d.Activation, d.Type))), step + " delay damage.");
            var hints = new BossComponent.GlobalHints(); source.AddGlobalHints(hints);
            Check(hints.SequenceEqual(f.M.GlobalHints()), step + " delay global hint.");
        }
        Compare("Start");
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]); Compare("duplicate");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Compare("Finish leaves delay");
        f.Party[0] = new(5, "", 100); f.W.Execute(new PartyState.OpModify(0, new(0, 0, false, "")));
        f.W.Execute(new PartyState.OpModify(5, new(100, 100, false, "Viewer")));
        f.Remove(100); f.Add(100, 0, f.Center, ActorType.Player); f.Sync();
        Check(ReferenceEquals(locked, f.M.DelayedHits[0]) && !ReferenceEquals(locked.Target, f.NativeViewer)
            && locked.Slot == 0, "CE34 delay captures original target and original slot.");
        Compare("slot changes and target generation retained");
        for (var i = 0; i < 2; ++i)
        {
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(20968));
            f.M.Effect(1, 20968, 100); Compare("actual target FIFO " + i);
        }
        f.Start(source, 20968, target: 999); Compare("missing target no prediction");
        f.Start(source, 20968, target: 1); Compare("existing nonparty target keeps zero mask");
        Check(f.M.Damage(f.NativeViewer).Single().Mask == 0, "CE34 out of party slot does not wrap to bit63.");
    }

    private static void HoofStacks()
    {
        using var f = new Fixture(7);
        var source = f.Activate<BossMod.Components.GenericStackSpread>("AccursedLight");
        f.Start(source, 20967);
        foreach (var offset in new[] { 7.999f, 8f, 8.001f, 20f })
        {
            f.Move(101, f.Center + new Vector3(offset, 0, 0));
            var hints = new BossComponent.TextHints(); source.AddHints(1, f.W.Actors.Find(101)!, hints);
            Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.M.Actors[101]).Select(h => (h.Text, h.Risky))),
                "CE34 true R8/min8 stack hint " + offset);
            var ai = new AIHints(); source.AddAIHints(1, f.W.Actors.Find(101)!, default, ai);
            var converted = ai.ForbiddenZones.SelectMany(z =>
            {
                var shapes = MechanicHintsConvert.ConvertForbiddenZone(z.Item1, 0, f.Center, f.M.SampleRadius, out var kind);
                return kind == MechanicNavZoneKind.ComplementSkipped
                    && MechanicHintsConvert.TryConvertComplement(z.Item1, 0, out var goals) ? goals : shapes;
            }).ToArray();
            var actual = f.M.Navigation(f.M.Actors[101]).SelectMany(z => z.Convert(f.Center, f.M.SampleRadius)).ToArray();
            foreach (var x in new[] { -8.001f, -8, -7.999f, 0f, 7.999f, 8, 8.001f })
            {
                var p = f.Center + new Vector3(x, 0, 0);
                Check(converted.Any(c => c.Template.Resolve(c.Position, c.Rotation, null).Contains(p, 0))
                    == actual.Any(c => c.Contains(p, 0)), "CE34 R8 actual goal conversion.");
            }
            var ownAi = new AIHints(); source.AddAIHints(0, f.Viewer, default, ownAi);
            CompareMetalFoxZones(f, ownAi, f.M.Navigation(f.NativeViewer).ToArray(),
                "CE34 own source R4 buddy " + offset);
        }
    }

    private static void HoofTarget()
    {
        using var f = new Fixture(7);
        var calculate = f.Source.GetType().GetMethod("CalculateModuleAIHints",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var ai = new AIHints();
        foreach (var id in new ulong[] { 1, 2 })
        {
            ai.PotentialTargets.Add(new(f.W.Actors.Find(id)!, 0, false));
            f.Set(id, a => a with { IsEnemy = true, ObjectIndex = (ushort)(id * 2), Hp = 1000 });
        }
        calculate.Invoke(f.Source, [0, f.Viewer, default(PartyRolesConfig.Assignment), ai]); f.Sync();
        Check(ai.PotentialTargets.Select(e => e.Priority).SequenceEqual(new[] { 0, 1 })
            && ai.PotentialTargets.Select(e => e.Priority)
                .SequenceEqual(new[] { f.M.TargetPriority(f.M.Actors[1].Actor)!.Value, f.M.TargetPriority(f.M.Actors[2].Actor)!.Value }),
            "CE34 module source priority.");
        var actors = f.Actors.ToArray();
        Check(QuestTargetPriority.Select(920, actors, 1, f.Center, assignedPriority: f.M.TargetPriority) == 2,
            "CE34 add supersedes current boss.");
        Check(QuestTargetPriority.Select(920, actors.Select(a => a.Id == 2 ? a with { Hp = 1 } : a), 2,
            f.Center, assignedPriority: f.M.TargetPriority) == 1, "CE34 PendingDead remains protected before setter.");
        Check(QuestTargetPriority.Select(920, actors, 1, f.Center) == 0,
            "CE34 shared selector does not enable unrelated territory ownership.");
        var dying = actors.Select(a => a.Id == 2 ? a with { Hp = 1 } : a).ToArray();
        Check(QuestTargetPriority.Select(920, dying, 2, f.Center with { Y = 20 },
            assignedPriority: f.M.TargetPriority, playerHeight: 0) == 1,
            "CE34 Party0 normalization height preserves Pointless while viewing another height.");
        foreach (var invalid in new[]
        {
            f.M.Actors[2].Actor with { Dead = true },
            f.M.Actors[2].Actor with { Targetable = false },
            f.M.Actors[2].Actor with { IsEnemy = false },
            f.M.Actors[2].Actor with { ObjectIndex = 3 },
        })
            Check(QuestTargetPriority.Select(920, [f.M.Actors[1].Actor, invalid], 2, f.Center,
                assignedPriority: f.M.TargetPriority) == 1, "CE34 excludes illegal candidate.");
    }
}
