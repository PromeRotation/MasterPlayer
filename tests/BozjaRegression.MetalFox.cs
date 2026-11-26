using System.Numerics;
using BossMod;
using MasterPlayer.MechanicHost;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Spatial.SafeZone;

internal static partial class BozjaRegression
{
    private static void RunMetalFox(Action<string, Action> run)
    {
        run("Bozja native CE31 actual client manifest standard shapes diagnostic square", MetalFoxStandard);
        run("Bozja native CE31 bit three modes strict time gates count colours and default risky", MetalFoxBits);
        run("Bozja native CE31 bit lazy original actors before gate dead movement generations mode retention", MetalFoxMembership);
        run("Bozja native CE31 Rush original targets Update length dynamic rotation raw Finish damage", MetalFoxRush);
        run("Bozja native CE31 Rush missing target self zero length NaN near buddy actual conversion", MetalFoxRushEdges);
        run("Bozja native CE31 actual cold first cast world order bits and square pull", MetalFoxCold);
        run("Bozja native CE31 Rush live target mask retains dead first bait activation", MetalFoxRushDamage);
        run("Bozja native CE31 Rush source destroy duplicate cold activation original generations", MetalFoxRushLifecycle);
    }

    private static void MetalFoxStandard()
    {
        Initialize();
        Console.WriteLine("INFO CE31 actual DynamicEvent13=" +
            data!.GetExcelSheet<Lumina.Excel.Sheets.DynamicEvent>()!.GetRow(13).Name);
        Check(data.GetExcelSheet<Lumina.Excel.Sheets.DynamicEvent>()!.GetRow(13).Name.ToString()
            == BozjaCatalog.Definitions[6].Name, "CE31 actual client name.");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.CriticalEngagement.CE31MetalFoxChaos.AID>())
            Check(new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE31 actual extra " + action);
        foreach (var rule in BozjaCatalog.Definitions[6].Rules.Where(r => r.Kind == BozjaRuleKind.Aoe))
        {
            using var f = new Fixture(6);
            var source = f.Activate<BossMod.Components.GenericAOEs>(rule.Component);
            var spell = f.Start(source, rule.Actions[0]);
            CompareMetalFoxBits(f, source, rule.Component, rule.Component);
            f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareMetalFoxBits(f, source, rule.Component, "no TTL");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, rule.Component, "watched Finish");
        }
        using var arena = new Fixture(6);
        var converted = MechanicHintsConvert.ConvertBounds(arena.Source.Arena.Bounds, arena.Source.Arena.Center, 0)!;
        for (var x = -31f; x <= 31; x += .53f)
            for (var z = -31f; z <= 31; z += .71f)
            {
                var p = arena.Center + new Vector3(x, 0, z);
                Check(converted.Contains(p, 0) == arena.M.Arena.Contains(p, 0), "CE31 true square bounds.");
            }
        Damage(6);
    }

    private static void MetalFoxBits()
    {
        foreach (var action in new uint[] { 20137, 20138, 20140 })
        {
            using var f = new Fixture(6);
            var source = f.Activate<BossMod.Components.GenericAOEs>("MagitekBitLasers");
            // Shower reads Actor rotation, Diffractive reads spell rotation.
            f.W.Actors.Find(2)!.PosRot = new(f.W.Actors.Find(2)!.Position.ToVec3(), .731f);
            f.Set(2, a => a with { Rotation = .731f }); f.Sync();
            var start = action == 20138 ? .4f + MathF.PI : .731f;
            var directions = new[] { start, start + MathF.PI / 2, start - MathF.PI / 2, start + MathF.PI,
                start + MathF.PI / 180f, start + MathF.PI / 180f + .00001f, start + MathF.Tau };
            for (var i = 0; i < directions.Length; ++i)
            {
                var actor = f.Add((ulong)(20 + i), 0x2DB6, f.Center + new Vector3(i * 2.123f, 0, -10.257f));
                actor.PosRot = new(actor.Position.ToVec3(), directions[i]);
                f.Set(actor.InstanceID, a => a with { Rotation = directions[i] });
            }
            f.Sync();
            f.Start(source, action);
            // The fixture activates only the bit component on the source side.
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            var threshold = action == 20137 ? 2.5d : action == 20138 ? 2d : 0d;
            foreach (var time in new[] { threshold, threshold + .000001d, 100d })
            {
                f.Now = START.AddSeconds(time); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
                CompareMetalFoxBits(f, source, "MagitekBitLasers", "time " + action + "/" + time);
            }
            f.Start(source, action == 20140 ? 20138u : 20140u);
            f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, "MagitekBitLasers", "nonempty mode ignores replacement Start");
            for (var i = 1; i <= 14; ++i)
            {
                source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(20141));
                f.M.Effect(2, 20141);
                CompareMetalFoxBits(f, source, "MagitekBitLasers", "Effect " + action + "/" + i);
            }
            Check(!f.M.Visible().Any(a => a.Cast.Rule.Kind == BozjaRuleKind.MagitekBitLasers), "CE31 exactly14 resets mode.");
        }
    }

    private static void CompareMetalFoxBits(Fixture f, BossMod.Components.GenericAOEs source, string component, string step,
        uint normalColor = 0, bool complementGoals = false)
    {
        var expected = source.ActiveAOEs(0, f.Viewer).ToArray();
        var actual = f.M.Visible(f.NativeViewer).Where(a => a.Cast.Rule.Component == component).ToArray();
        Check(expected.Length == actual.Length, step + " count.");
        for (var i = 0; i < expected.Length; ++i)
        {
            var e = expected[i]; var a = actual[i];
            Check(e.Origin.ToVec3() == a.Cast.Origin && e.Rotation.Rad == a.Cast.Rotation
                && e.Activation == a.Cast.Activation && e.Risky == a.Risky
                && e.Color == (a.Danger ? Colors.Danger : normalColor), step + " exact fields/order.");
            for (var x = -35f; x <= 35; x += 2.31f)
                for (var z = -35f; z <= 35; z += 2.67f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(e.Check(new(p.X, p.Z)) == a.Cast.Contains(p), step + " raw shape.");
                }
        }
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        var zones = f.M.Navigation(f.NativeViewer).Where(z => z.Component == component).ToArray();
        CompareMetalFoxZones(f, ai, zones, step, complementGoals);
        ai.Normalize();
        CompareMetalFoxZones(f, ai, f.M.NavigationForHost(f.NativeViewer).Where(z => z.Component == component).ToArray(),
            step + " actual Normalize RefSort", complementGoals);
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Any() == actual.Any(a => a.Risky && a.Cast.Contains(f.NativeViewer.Actor.Position)), step + " personal hint.");
    }

    private static void CompareMetalFoxZones(Fixture f, AIHints ai, BozjaZone[] zones, string step, bool complementGoals = false)
    {
        Check(ai.ForbiddenZones.Count == zones.Length, step + " FZ count.");
        for (var i = 0; i < zones.Length; ++i)
        {
            var e = ai.ForbiddenZones[i];
            Check(e.Item2 == zones[i].Activation, step + " FZ clock.");
            var expected = MechanicHintsConvert.ConvertForbiddenZone(e.Item1, 0, f.Center, f.M.SampleRadius, out var kind);
            if (complementGoals && kind == MechanicNavZoneKind.ComplementSkipped
                && MechanicHintsConvert.TryConvertComplement(e.Item1, 0, out var goals))
            {
                expected = goals;
                Check(zones[i].Goal, step + " complement remains a source goal");
            }
            var actual = zones[i].Convert(f.Center, f.M.SampleRadius).ToArray();
            Check(expected.Count == actual.Length, step + " true conversion count.");
            for (var x = -32f; x <= 32; x += 1.97f)
                for (var z = -32f; z <= 32; z += 2.13f)
                {
                    var p = f.Center + new Vector3(x, 0, z);
                    Check(expected.Any(s => s.Template.Resolve(s.Position, s.Rotation, null).Contains(p, 0))
                        == actual.Any(s => s.Contains(p, 0)), step + " actual converted shape.");
                }
        }
    }

    private static void MetalFoxMembership()
    {
        using var f = new Fixture(6);
        var source = f.Activate<BossMod.Components.GenericAOEs>("MagitekBitLasers");
        f.Add(20, 0x2DB6, f.Center); f.Sync();
        f.Start(source, 20137); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareMetalFoxBits(f, source, "MagitekBitLasers", "invisible first query initializes enemies");
        f.Add(21, 0x2DB6, f.Center + Vector3.UnitX); f.Sync();
        f.Remove(20); f.Add(22, 0x2DB6, f.Center + Vector3.UnitZ); f.Sync();
        f.Now = START.AddSeconds(3); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CompareMetalFoxBits(f, source, "MagitekBitLasers", "cached order after slot reuse");
        f.W.Actors.Find(21)!.IsDead = true;
        f.W.Actors.Find(21)!.PosRot = new(f.Center + new Vector3(12.123f, 0, -9.257f), 1.1f);
        f.Set(21, a => a with { Dead = true, Position = f.Center + new Vector3(12.123f, 0, -9.257f), Rotation = 1.1f });
        f.Sync();
        CompareMetalFoxBits(f, source, "MagitekBitLasers", "dead current moving bit remains");
        for (var i = 0; i < 14; ++i) { source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(20141)); f.M.Effect(2, 20141); }
        for (var i = 0; i < 5; ++i) { source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(20141)); f.M.Effect(2, 20141); }
        f.Start(source, 20140); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareMetalFoxBits(f, source, "MagitekBitLasers", "mode None retains cache and counts before new Start");
    }

    private static void CompareMetalFoxRush(Fixture f, BossMod.Components.GenericBaitAway source, string step)
    {
        var expected = source.ActiveBaits;
        var actual = f.M.Rushes.Where(b => !b.Source.Actor.Dead).ToArray();
        Check(expected.Count == actual.Length, step + " active bait count.");
        for (var i = 0; i < actual.Length; ++i)
        {
            var e = expected[i]; var a = BozjaEncounter.RushView(actual[i]);
            Check(e.Source.Position.ToVec3() == a.Origin && e.Rotation.Rad == a.Rotation && e.Activation == a.Activation
                && ((AOEShapeRect)e.Shape).LengthFront == ((RayRectangleTemplate)a.Shape).Length, step + " bait exact fields.");
            var rect = (AOEShapeRect)e.Shape;
            foreach (var along in new[] { -.001f, 0, .001f, rect.LengthFront - .001f, rect.LengthFront, rect.LengthFront + .001f })
                foreach (var side in new[] { -7.001f, -7, -6.999f, 0, 6.999f, 7, 7.001f })
                {
                    var (sin, cos) = Math.SinCos(a.Rotation);
                    var p = a.Origin + new Vector3(along * (float)sin + side * (float)cos, 0,
                        along * (float)cos - side * (float)sin);
                    Check(e.Shape.Check(new(p.X, p.Z), e.Source.Position, e.Rotation) == a.Contains(p),
                        step + " exact rotated rectangle boundary.");
                }
        }
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))),
            step + " bait exact text.");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).ToArray(), step);
        Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
            .SequenceEqual(f.M.Damage(f.NativeViewer).Select(d => (d.Mask, d.Activation, d.Type))), step + " exact damage.");
    }

    private static void MetalFoxRush()
    {
        using var f = new Fixture(6);
        var source = f.Activate<BossMod.Components.GenericBaitAway>("Rush");
        var spell = f.Start(source, 20139);
        CompareMetalFoxRush(f, source, "Start");
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        CompareMetalFoxRush(f, source, "duplicate Start");
        f.W.Actors.Find(100)!.PosRot = new(f.Center + new Vector3(10, 0, 20), .3f);
        f.Set(100, a => a with { Position = f.Center + new Vector3(10, 0, 20) }); f.Sync(false);
        CompareMetalFoxRush(f, source, "event moves direction but not length");
        source.Update(); f.Sync();
        CompareMetalFoxRush(f, source, "Update length");
        f.W.Actors.Find(100)!.IsDead = true; f.Set(100, a => a with { Dead = true }); f.Sync(); source.Update();
        CompareMetalFoxRush(f, source, "dead target permitted");
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(20139)); f.M.Effect(2, 20139);
        CompareMetalFoxRush(f, source, "Effect not Finish");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        Check(f.M.Rushes.Count == 1, "CE31 Finish deletes first only.");
        CompareMetalFoxRush(f, source, "FIFO Finish");
        f.W.Actors.Find(2)!.IsDead = true; f.Set(2, a => a with { Dead = true }); f.Sync(); source.Update();
        CompareMetalFoxRush(f, source, "dead source hides");
        f.W.Actors.Find(2)!.IsDead = false; f.Set(2, a => a with { Dead = false }); f.Sync(); source.Update();
        CompareMetalFoxRush(f, source, "source revival restores");
        f.Remove(100); f.Add(100, 0, f.Center + new Vector3(-10, 0, 0), ActorType.Player); f.Sync(); source.Update();
        // The fixture resolves Viewer to the new Actor on each access.
        CompareMetalFoxRush(f, source, "same ID viewer new reference avoids old bait");
        Check(!ReferenceEquals(f.M.Rushes[0].Target, f.NativeViewer), "CE31 old target reference retained.");
    }

    private static void MetalFoxRushEdges()
    {
        foreach (var target in new ulong[] { 2, 100, 999 })
        {
            using var f = new Fixture(6);
            var source = f.Activate<BossMod.Components.GenericBaitAway>("Rush");
            f.Move(2, f.Center); f.Move(100, f.Center);
            f.Start(source, 20139, target: target);
            CompareMetalFoxRush(f, source, "self/coincident/missing " + target);
            if (target == 999)
            {
                f.Add(999, 0, f.Center, ActorType.Player); f.Sync(); source.Update();
                CompareMetalFoxRush(f, source, "late target cannot retroactively create bait");
            }
            foreach (var distance in new[] { 6.999f, 7f, 7.001f, 40f })
            {
                f.Move(101, f.Center + new Vector3(distance, 0, 0)); source.Update();
                CompareMetalFoxRush(f, source, "buddy distance " + target + "/" + distance);
            }
        }
    }

    private static void MetalFoxCold()
    {
        foreach (var helperFirst in new[] { false, true })
        {
            using var f = new Fixture(6, helperFirst);
            if (!helperFirst) f.Add(3, 0x233C, f.Center);
            f.Add(30, 0x2DB6, f.Center);
            foreach (var (id, action) in new[] { (2UL, 20137u), (3UL, 20140u) })
            {
                f.W.Actors.Find(id)!.CastInfo = new() { Action = new(ActionType.Spell, action), TotalTime = 4,
                    TargetID = 100, Location = f.Center, Rotation = new(.4f) };
                f.Set(id, a => a with { Cast = new(action, 100, f.Center, .4f, START, START.AddSeconds(4.3f), false, false)
                    { IsSpell = true, TotalTime = 4 } });
            }
            f.M.Clear(); f.Sync();
            var source = f.Activate<BossMod.Components.GenericAOEs>("MagitekBitLasers");
            // Only the bit component is active on the oracle.
            foreach (var actor in f.M.Actors.Values.Where(a => a.Cast != null).ToArray()) f.M.Finish(actor, actor.Cast!);
            f.Now = START.AddSeconds(3); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareMetalFoxBits(f, source, "MagitekBitLasers", "cold helper order " + helperFirst);
            f.W.Actors.Find(1)!.InCombat = true;
            var checkPull = f.Source.GetType().GetMethod("CheckPull",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            foreach (var offset in new[] { new Vector3(30, 0, 30), new Vector3(30.001f, 0, 0) })
            {
                f.Viewer.PosRot = new(f.Center + offset, 0);
                var model = new BozjaEncounter(f.M.Definition);
                model.Synchronize(f.Actors.Select(a => a.Id == 100 ? a with { Position = f.Center + offset } : a),
                    f.Party.ToArray(), 100, START);
                Check(model.Active == (bool)checkPull.Invoke(f.Source, null)!, "CE31 actual square pull " + offset);
            }
        }
    }

    private static void MetalFoxRushDamage()
    {
        using var f = new Fixture(6);
        var source = f.Activate<BossMod.Components.GenericBaitAway>("Rush");
        f.Start(source, 20139);
        f.Add(3, 0x233C, f.Center + new Vector3(10, 0, 0)); f.Sync();
        f.Now = START.AddSeconds(1); f.W.Frame.Timestamp = f.Now;
        f.Start(source, 20139, id: 3, target: 101);
        f.W.Actors.Find(2)!.IsDead = true; f.Set(2, a => a with { Dead = true }); f.Sync(); source.Update();
        CompareMetalFoxRush(f, source, "dead first bait still owns predicted clock");
        var damage = f.M.Damage(f.NativeViewer).Single();
        Check(damage.Mask == 2 && damage.Activation == f.M.Rushes[0].Activation,
            "CE31 mask from living source but activation from raw first bait.");
    }

    private static void MetalFoxRushLifecycle()
    {
        using var f = new Fixture(6);
        var spell = new ActorCastInfo { Action = new(ActionType.Spell, 20139), TotalTime = 4,
            TargetID = 100, Rotation = new(.4f), Location = f.Center };
        f.W.Actors.Find(2)!.CastInfo = spell;
        f.Set(2, a => a with { Cast = new(20139, 100, f.Center, .4f, START, START.AddSeconds(4.3f), false, false)
            { IsSpell = true, TotalTime = 4 } });
        f.M.Clear(); f.Sync();
        var source = f.Activate<BossMod.Components.GenericBaitAway>("Rush");
        CompareMetalFoxRush(f, source, "cold actual existing cast");
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        var oldSource = f.M.Actors[2];
        f.Remove(2); source.Update();
        Check(f.M.Rushes.Count == 1 && ReferenceEquals(f.M.Rushes[0].Source, oldSource)
            && oldSource.Destroyed, "CE31 source destruction finishes only first duplicate.");
        CompareMetalFoxRush(f, source, "destroy current cast with duplicate retained");
        f.Add(2, 0x233C, f.Center + new Vector3(15, 0, 0)); f.Sync(); source.Update();
        CompareMetalFoxRush(f, source, "new source generation does not replace frozen source");
        var unrelated = new ActorCastInfo { Action = new(ActionType.Spell, 999) };
        source.OnCastFinished(f.W.Actors.Find(2)!, unrelated);
        f.M.Finish(f.M.Actors[2], new(999, 0, f.Center, 0, START, START, false, false));
        CompareMetalFoxRush(f, source, "unrelated action cannot finish");
        source.OnCastFinished(f.W.Actors.Find(1)!, spell);
        f.M.Finish(f.M.Actors[1], new(20139, 100, f.Center, 0, START, START, false, false));
        CompareMetalFoxRush(f, source, "unrelated caster cannot finish");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell);
        f.M.Finish(f.M.Actors[2], new(20139, 100, f.Center, 0, START, START, false, false));
        CompareMetalFoxRush(f, source, "same ID new source finishes retained old first bait");
        Check(f.M.Rushes.Count == 0, "CE31 new source Finish matches original source ID.");
    }
}
