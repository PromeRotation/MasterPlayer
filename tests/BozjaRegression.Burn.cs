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
    private static void RunBurn(Action<string, Action> run)
    {
        run("Bozja native CE64 client eight components square pull real action manifest", BurnClient);
        run("Bozja native CE64 standard two shapes actual watched Finish no Effect TTL", BurnStandard);
        run("Bozja native CE64 icon FIFO first two hidden promotion locked original positions", BurnMissile);
        run("Bozja native CE64 boss four Effects repeated Start preserves counter helper raw rotation", BurnBoss);
        run("Bozja native CE64 escort per caster participant cache original references Lose retained", BurnTracking);
        run("Bozja native CE64 escort six independent matching position Effects ordinary Finish retained", BurnEscort);
        run("Bozja native CE64 weakpoint front back original caster earliest equal clock Lose and arc", BurnWeakpoint);
        run("Bozja native CE64 raw nonSpell residual weakpoint float frame clock Destroy original reference", BurnRawClock);
        run("Bozja native CE64 cold actor order duplicate true status slots tracker Destroy and source draw order", BurnColdOrder);
        run("Bozja native CE64 real Feed eight components cold per actor 60slots events navigation facing", BurnFeed);
        run("Bozja native CE64 live raw Feed zero and short TotalTime identity reset exact NPC ticks", BurnRawFeed);
    }

    private static void BurnClient()
    {
        using var f = new Fixture(25);
        Check(f.M.IsBurn && f.M.Definition.Territory == 975 && f.M.Definition.DynamicEventId == 18
            && f.M.Definition.Rules.Length == 8, "CE64 identity");
        Check(Service.LuminaRow<Lumina.Excel.Sheets.DynamicEvent>(18)?.Name.ToString() == f.M.Definition.Name
            && Service.LuminaRow<Lumina.Excel.Sheets.TerritoryType>(975)?.ContentFinderCondition.RowId == 778
            && Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(778)?.TerritoryType.RowId == 975,
            "CE64 actual client DynamicEvent and bidirectional CFC mapping");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.CriticalEngagement.CE64FeelingTheBurn.AID>())
            Check(data!.GetExcelSheet<Lumina.Excel.Sheets.Action>()!.TryGetRow((uint)action, out _)
                && new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE64 action " + action);
        for (var x = -25f; x <= 25; x += .317f)
            for (var z = -25f; z <= 25; z += .431f)
            {
                var p = f.Center + new Vector3(x, 0, z);
                Check(f.Source.Arena.InBounds(new(p.X, p.Z)) == f.M.Arena.Contains(p, 0), "CE64 square arena");
            }
        f.M.Clear();
        f.Set(100, a => a with { Position = f.Center + new Vector3(24.01f, 0, 0) }); f.Sync();
        Check(!f.M.Active, "CE64 outside square does not pull");
        f.Set(100, a => a with { Position = f.Center + new Vector3(24, 0, 24) }); f.Sync();
        Check(f.M.Active, "CE64 inclusive square corner pulls");
    }

    private static void BurnStandard()
    {
        foreach (var (name, action) in new[] { ("DiveFormation", 23606u), ("SurfaceMissile", 23615u) })
        {
            using var f = new Fixture(25);
            var source = f.Activate<GenericAOEs>(name);
            var spell = f.Start(source, action);
            CompareMetalFoxBits(f, source, name, "CE64 standard Start");
            source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(action)); f.M.Effect(2, action);
            f.Now = START.AddSeconds(20); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            CompareMetalFoxBits(f, source, name, "CE64 standard no Effect/TTL");
            source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
            CompareMetalFoxBits(f, source, name, "CE64 standard watched Finish");
        }
    }

    private static void BurnMissile()
    {
        using var f = new Fixture(25);
        var source = f.Activate<GenericAOEs>("AntiPersonnelMissile");
        for (var i = 0; i < 4; ++i)
        {
            f.Now = START.AddSeconds(i); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
            f.Move(2, f.Center + new Vector3(i * 7.123f, 0, i * 3.231f));
            source.OnEventIcon(f.W.Actors.Find(2)!, 261, 101); f.M.Icon(2, 261, 101);
            CompareMetalFoxBits(f, source, "AntiPersonnelMissile", "CE64 icon " + i);
        }
        f.Move(2, f.Center + Vector3.UnitX * 20);
        f.Now = START.AddSeconds(100); f.W.Frame.Timestamp = f.Now; f.M.Advance(f.Now);
        CompareMetalFoxBits(f, source, "AntiPersonnelMissile", "CE64 locked no timeout");
        Check(f.M.Visible().Count() == 2 && f.M.Casts.Count == 4, "CE64 hidden tail preserved");
        for (var i = 0; i < 5; ++i)
        {
            source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23610)); f.M.Effect(1, 23610);
            CompareMetalFoxBits(f, source, "AntiPersonnelMissile", "CE64 cross source FIFO " + i);
        }
    }

    private static void BurnBoss()
    {
        using var f = new Fixture(25);
        var source = f.Activate<GenericAOEs>("ChainCannonBoss");
        var spell = f.Start(source, 24658);
        CompareMetalFoxBits(f, source, "ChainCannonBoss", "CE64 boss first Start plus1");
        for (var i = 0; i < 3; ++i)
        {
            f.Move(2, f.Center + new Vector3(.123f * i, 0, .456f * i));
            source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(24659)); f.M.Effect(2, 24659);
            CompareMetalFoxBits(f, source, "ChainCannonBoss", "CE64 boss Effect " + i);
        }
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        Check(f.M.BurnBossHits == 3, "CE64 repeat Start does not reset count");
        CompareMetalFoxBits(f, source, "ChainCannonBoss", "CE64 replacement Start still third");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareMetalFoxBits(f, source, "ChainCannonBoss", "CE64 ordinary Finish retains");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(24659)); f.M.Effect(1, 24659);
        CompareMetalFoxBits(f, source, "ChainCannonBoss", "CE64 fourth clears across sources");
        Check(f.M.BurnBossHits == 0, "CE64 fourth reset");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(24659)); f.M.Effect(1, 24659);
        CompareMetalFoxBits(f, source, "ChainCannonBoss", "CE64 Effect without Start reconstructs");
    }

    private static void BurnGain(Fixture f, BossComponent source, ulong id, uint statusId, bool gain = true, int slot = 59)
    {
        var status = new ActorStatus(statusId, 0, START.AddSeconds(30), 0);
        if (gain) source.OnStatusGain(f.W.Actors.Find(id)!, ref status);
        else source.OnStatusLose(f.W.Actors.Find(id)!, ref status);
        f.Set(id, a => a with
        {
            Statuses = gain ? [new(statusId, 0, 0, status.ExpireAt) { SlotIndex = slot }] : [],
        });
        f.Sync();
    }

    private static void BurnTracking()
    {
        using var f = new Fixture(25);
        var source = f.Activate<GenericAOEs>("ChainCannonEscort");
        foreach (var id in new ulong[] { 20, 21 })
        {
            var caster = f.Add(id, 0x32FD, f.Center - Vector3.UnitZ * (10 + id - 20));
            caster.TargetID = 100;
            f.Set(id, a => a with { TargetId = 100 }); f.Sync();
            BurnGain(f, source, id, 2056);
        }
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        var zones = f.M.Navigation(f.NativeViewer).Where(z => z.Component == "ChainCannonEscort").ToArray();
        Check(ai.ForbiddenZones.Count == 1 && zones.Length == 1, "CE64 second caster sees participant cache on FIRST call");
        CompareMetalFoxZones(f, ai, zones, "CE64 first call per caster cache");
        ai = new(); source.AddAIHints(0, f.Viewer, default, ai);
        CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).ToArray(), "CE64 second query both cached");
        BurnGain(f, source, 20, 2056, false);
        BurnGain(f, source, 21, 2056, false);
        Check(f.M.BurnTrackingViews(f.NativeViewer).Count() == 2, "CE64 Lose leaves trackers");
        f.Remove(101); f.Add(101, 0, f.Center + Vector3.UnitX * 18, ActorType.Player); f.Sync();
        ai = new(); source.AddAIHints(0, f.Viewer, default, ai);
        CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).ToArray(), "CE64 participants retain original destroyed buddy");
        f.Start(source, 23614, 1);
        ai = new(); source.AddAIHints(0, f.Viewer, default, ai);
        var stopped = f.M.Navigation(f.NativeViewer).ToArray();
        Check(stopped.Length == 2 && stopped.All(z => z.Shape is MasterPlayer.Spatial.SafeZone.RayRectangleTemplate),
            "CE64 SurfaceMissile stops tracking early");
        CompareMetalFoxZones(f, ai, stopped, "CE64 stopped emits rectangles");
        Check(f.M.BurnTrackingViews(f.NativeViewer).All(v => v.Outline), "CE64 drawing still outlines target despite inactive tracking");
    }

    private static void BurnEscort()
    {
        using var f = new Fixture(25);
        var source = f.Activate<GenericAOEs>("ChainCannonEscort");
        f.Add(20, 0x32FD, f.Center + Vector3.UnitZ * 4); f.Sync();
        f.Start(source, 23612, 20); f.Start(source, 23612, 2);
        CompareMetalFoxBits(f, source, "ChainCannonEscort", "CE64 two independent escorts");
        f.Set(20, a => a with { Cast = null }); f.W.Actors.Find(20)!.CastInfo = null; f.Sync();
        CompareMetalFoxBits(f, source, "ChainCannonEscort", "CE64 Finish retains fixed line");
        for (var i = 0; i < 6; ++i)
        {
            source.OnEventCast(f.W.Actors.Find(20)!, PatriotEvent(23613)); f.M.Effect(20, 23613);
            CompareMetalFoxBits(f, source, "ChainCannonEscort", "CE64 escort independent hit " + i);
        }
        Check(f.M.Casts.Count(c => c.Rule.Kind == BozjaRuleKind.BurnEscort) == 1, "CE64 first six remove only match");
        for (var i = 0; i < 6; ++i)
        {
            source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(23613)); f.M.Effect(2, 23613);
        }
        CompareMetalFoxBits(f, source, "ChainCannonEscort", "CE64 second six clears");
    }

    private static void CompareBurnEyes(Fixture f, GenericGaze source, string step)
    {
        var expected = source.ActiveEyes(0, f.Viewer).ToArray();
        var actual = f.M.BurnEyes(f.NativeViewer).ToArray();
        Check(expected.Select(e => (e.Position.ToVec3(), e.Activation, e.Forward.Rad, e.Inverted))
            .SequenceEqual(actual.Select(e => (e.Position, e.Activation, e.Forward, e.Inverted))), step + " exact selected eye");
        var hints = new BossComponent.TextHints(); source.AddHints(0, f.Viewer, hints);
        Check(hints.Select(h => (h.Item1, h.Item2)).SequenceEqual(f.M.Hints(f.NativeViewer).Select(h => (h.Text, h.Risky))), step + " text");
        var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
        Check(ai.ForbiddenDirections.Select(d => (d.Item1.Rad, d.Item2.Rad, d.Item3))
            .SequenceEqual(f.M.GazeDirections(f.NativeViewer.Actor)), step + " directions");
        var sink = new MechanicDrawSink(new());
        using (MechanicDrawSink.Install(sink)) source.DrawArenaForeground(0, f.Viewer);
        Check(sink.Commands.OfType<MechanicDrawEye>().Select(e => e.Danger)
            .SequenceEqual(actual.Select(e => BozjaEncounter.EyeDanger(e, f.NativeViewer.Actor))), step + " actual eye colors");
        var arcs = sink.Commands.OfType<MechanicDrawPolyline>().ToArray();
        Check(arcs.Length == actual.Length, step + " actual arc count");
        for (var i = 0; i < arcs.Length; ++i)
            Check(arcs[i].Vertices.Select(p => p.ToVec3(f.NativeViewer.Actor.Position.Y))
                .SequenceEqual(BozjaEncounter.EyeArc(actual[i], f.NativeViewer.Actor)), step + " exact forward offset arc");
    }

    private static void BurnWeakpoint()
    {
        using var f = new Fixture(25);
        var source = f.Activate<GenericGaze>("PreciseStrike");
        f.Move(2, f.Center - Vector3.UnitZ * 10);
        f.Add(20, 0x31A1, f.Center - Vector3.UnitZ * 20); f.Sync();
        var spells = new Dictionary<ulong, ActorCastInfo>();
        foreach (var id in new ulong[] { 2, 20 })
        {
            var spell = f.Start(source, 23619, id, total: 5);
            spell.Rotation = default; spells[id] = spell;
            f.Set(id, a => a with { Cast = a.Cast! with { Rotation = 0 } }); f.Sync();
        }
        foreach (var status in new uint[] { 2644, 1709 })
        {
            BurnGain(f, source, 100, status);
            foreach (var rotation in new[] { 0f, MathF.PI / 4, MathF.PI, -MathF.PI / 2 })
            {
                f.Viewer.PosRot = new(f.Center, rotation); f.Set(100, a => a with { Rotation = rotation }); f.Sync();
                CompareBurnEyes(f, source, "CE64 weakpoint " + status + "/" + rotation);
            }
            Check(f.M.BurnEyes(f.NativeViewer).Single().Position == f.M.Actors[2].Actor.Position,
                "CE64 equal remaining preserves first caster");
        }
        f.W.Actors.Find(20)!.CastInfo!.ElapsedTime = 2;
        f.M.Actors[20].Elapsed = 2;
        f.M.Actors[20].RawElapsed = 2;
        CompareBurnEyes(f, source, "CE64 earlier actual remaining caster");
        Check(f.M.BurnEyes(f.NativeViewer).Single().Position == f.M.Actors[20].Actor.Position, "CE64 earliest remaining");
        BurnGain(f, source, 100, 1709, false); CompareBurnEyes(f, source, "CE64 Lose removes map");
        BurnGain(f, source, 100, 2644); BurnGain(f, source, 100, 1709);
        var lose = new ActorStatus(2644, 0, START.AddSeconds(30), 0);
        source.OnStatusLose(f.Viewer, ref lose);
        // Remove the first of two REAL status slots while retaining the other.
        f.Set(100, a => a with { Statuses = [new(2644, 0, 0, START.AddSeconds(30)) { SlotIndex = 0 },
            new(1709, 0, 0, START.AddSeconds(30)) { SlotIndex = 59 }] }); f.Sync();
        f.Set(100, a => a with { Statuses = a.Statuses.Where(s => s.Id != 2644).ToArray() }); f.Sync();
        CompareBurnEyes(f, source, "CE64 unrelated weakpoint Lose still clears ID map");
    }

    private static void BurnRawClock()
    {
        using var f = new Fixture(25);
        var source = f.Activate<GenericGaze>("PreciseStrike");
        f.Move(2, f.Center - Vector3.UnitZ * 10);
        f.Move(100, f.M.Actors[2].Actor.Position + new Vector3(10 * (float)Math.Sin(.4f), 0, 10 * (float)Math.Cos(.4f)));
        BurnGain(f, source, 100, 2644);
        var spell = f.Start(source, 23619, total: 5);
        source.OnCastStarted(f.W.Actors.Find(2)!, spell); f.M.Start(f.M.Actors[2]);
        source.OnCastFinished(f.W.Actors.Find(2)!, spell);
        var raw = new ActorCastInfo { Action = new(ActionType.Ability, 6499), TotalTime = 5,
            ElapsedTime = 1.9f, Rotation = new(.4f), Location = f.M.Actors[2].Actor.Position };
        f.W.Actors.Find(2)!.CastInfo = raw;
        f.Set(2, a => a with { Cast = new(6499, 0, a.Position, .4f, START.AddSeconds(-1.9), START.AddSeconds(3.4), false, false)
            { TotalTime = 5, IsSpell = false } });
        f.Sync();
        CompareBurnEyes(f, source, "CE64 raw ability exact NPC addition order");
        Check(f.M.BurnEyes(f.NativeViewer).Single().Activation == f.Now.AddSeconds(raw.NPCRemainingTime), "CE64 actual raw NPC ticks");
        raw.TotalTime = 2; raw.ElapsedTime = 0;
        f.Set(2, a => a with { Cast = a.Cast! with { TotalTime = 2, StartedAt = START, FinishAt = START.AddSeconds(2.3) } });
        f.Sync();
        for (var i = 1; i <= 10; ++i)
        {
            f.Now = START.AddSeconds(i * .1);
            f.W.Execute(new WorldState.OpFrameStart(new(f.Now, (ulong)f.Now.Ticks, f.W.Frame.Index + 1, .1f, .1f, 1),
                default, f.W.Client.GaugePayload, f.W.Client.CameraAzimuth));
            f.Sync();
            CompareBurnEyes(f, source, "CE64 raw ability incremental frame " + i);
        }
        Check(raw.ElapsedTime != 1f && f.M.Actors[2].RawElapsed == raw.ElapsedTime, "CE64 accumulates float rather than recomputing elapsed");
        f.Remove(2);
        CompareBurnEyes(f, source, "CE64 destroyed duplicate keeps original fallback eye");
        f.Add(2, 0x233C, f.Center + Vector3.UnitX * 20); f.Sync();
        CompareBurnEyes(f, source, "CE64 new same ID does not replace original weakpoint caster");
    }

    private static void BurnColdOrder()
    {
        foreach (var trackerFirst in new[] { false, true })
        {
            using var f = new Fixture(25);
            foreach (var id in trackerFirst ? new ulong[] { 20, 21 } : [21UL, 20UL])
                f.Add(id, 0x32FD, f.Center - Vector3.UnitZ * (10 + id - 20));
            var tracker = f.W.Actors.Find(20)!;
            tracker.TargetID = 101;
            tracker.Statuses[0] = new(2056, 0, START.AddSeconds(10), 0);
            tracker.Statuses[59] = new(2056, 0, START.AddSeconds(30), 0);
            f.Set(20, a => a with { TargetId = 101, Statuses = [
                new(2056, 0, 0, START.AddSeconds(10)) { SlotIndex = 0 },
                new(2056, 0, 0, START.AddSeconds(30)) { SlotIndex = 59 }] });
            var shooter = f.W.Actors.Find(21)!;
            shooter.CastInfo = new() { Action = new(ActionType.Spell, 23612), TotalTime = 5, Rotation = default, Location = shooter.Position.ToVec3() };
            f.Set(21, a => a with { Cast = new(23612, 0, a.Position, 0, START, START.AddSeconds(5.3f), false, false) { TotalTime = 5, IsSpell = true } });
            f.M.Clear(); f.Sync();
            var source = f.Activate<GenericAOEs>("ChainCannonEscort");
            Check(f.M.BurnTrackingViews(f.NativeViewer).Count() == (trackerFirst ? 0 : 2), "CE64 per Actor Start then 60slot Gains");
            var ai = new AIHints(); source.AddAIHints(0, f.Viewer, default, ai);
            CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).ToArray(), "CE64 cold ordered duplicate tracker");
            if (trackerFirst) continue;
            var sink = new MechanicDrawSink(new());
            using (MechanicDrawSink.Install(sink)) source.DrawArenaBackground(0, f.Viewer);
            var drawn = sink.Commands.OfType<MechanicDrawZoneRect>().ToArray();
            Check(drawn.Length == 3, "CE64 source static AOE then two dynamic tracking backgrounds");
            Check(drawn[0].Origin == shooter.Position && drawn[1].Origin == tracker.Position && drawn[2].Origin == tracker.Position,
                "CE64 exact source static-before-tracking draw order");
            f.Remove(20); f.Add(20, 0x32FD, f.Center + Vector3.UnitX * 23); f.Sync();
            Check(f.M.BurnTrackingViews(f.NativeViewer).All(v => v.Cast.Origin == tracker.Position.ToVec3()),
                "CE64 destroyed trackers retain original geometry across same ID replacement");
            ai = new(); source.AddAIHints(0, f.Viewer, default, ai);
            CompareMetalFoxZones(f, ai, f.M.Navigation(f.NativeViewer).ToArray(), "CE64 destroyed tracker original cache");
        }
    }

    private static void BurnRawFeed()
    {
        using var f = new Fixture(25);
        using var session = new MechanicSession("CE64-live-raw-clock");
        var feed = new MechanicWorldFeed(session);
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        f.Move(2, f.Center - Vector3.UnitZ * 10);
        f.Set(1, a => a with { SubKind = 5 });
        f.Set(100, a => a with { Statuses = [new(2644, 0, 0, START.AddSeconds(30)) { SlotIndex = 59 }] });
        void Frame(uint action, bool spell, float total, float elapsed, double at)
        {
            var now = START.AddSeconds(at);
            f.Now = now;
            f.Set(2, a => a with { Cast = new(action, 100, a.Position, 0, now.AddSeconds(-elapsed),
                now.AddSeconds(Math.Max(0, total - elapsed) + .3), false, false) { TotalTime = total, IsSpell = spell } });
            session.BeginFrame(now);
            var snapshots = f.Actors.Select((a, i) =>
            {
                var snapshot = (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!;
                // The live sampler supplies raw ActionType, TotalTime and
                // ElapsedTime; the legacy replay converter clamps Spell.
                return a.Id != 2 ? snapshot : snapshot with
                {
                    Cast = new(action, spell, 100, 0, a.Position, total, false, elapsed)
                };
            }).ToArray();
            feed.ApplyFrame(new(975, 778, 100, 0,
                f.Party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [], snapshots));
            feed.Flush();
            session.Update();
            f.Sync();
        }
        Frame(23619, true, 5, 0, 0);
        var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31A0);
        var component = source.Components.OfType<GenericGaze>().Single();
        var caster = session.World.Actors.Find(2)!;
        // A real duplicate callback followed by one Finish leaves the original
        // caster in CastWeakpoint, which subsequently observes raw Ability.
        component.OnCastStarted(caster, caster.CastInfo!);
        f.M.Start(f.M.Actors[2]);
        foreach (var step in new[] { (0f, 0f, 1d), (.01f, .004f, 2d), (.02f, .003f, 2d), (.02f, .019f, 2.001d) })
        {
            Frame(6499, false, step.Item1, step.Item2, step.Item3);
            var raw = caster.CastInfo!;
            Check(raw.TotalTime == step.Item1 && raw.Action.Type == ActionType.Ability, "CE64 live raw snapshot retained");
            Check(f.M.Actors[2].RawElapsed == raw.ElapsedTime, "CE64 raw identity reset matches live Feed float elapsed");
            var expected = component.ActiveEyes(0, session.World.Actors.Find(100)!).ToArray().Single();
            var actual = f.M.BurnEyes(f.NativeViewer).Single();
            Check(expected.Activation == actual.Activation && actual.Activation == f.Now.AddSeconds(raw.NPCRemainingTime),
                "CE64 live short/zero TotalTime exact NPC ticks at " + step);
        }
    }

    private static void BurnFeed()
    {
        Initialize();
        using var session = new MechanicSession("CE64-source-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(975);
        var model = region.Encounters.Single(m => m.IsBurn);
        var center = model.Center;
        ReplayCast Spell(uint action, Vector3 p, float rot = 0) => new(action, 100, p, rot, START, START.AddSeconds(5.3f), false, false)
            { TotalTime = 5, IsSpell = true };
        ReplayStatus Status(uint id) => new(id, 0, 0, START.AddSeconds(30)) { SlotIndex = 59 };
        ReplayStatus[] Slots(uint id) => Enumerable.Range(0, 60).Select(i =>
            i == 59 ? Status(id) : new ReplayStatus(0, 0, 0, default) { SlotIndex = i }).ToArray();
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x31A0, Kind = 2, SubKind = 5, Position = center, Targetable = true, InCombat = true, Hp = 1000, Cast = Spell(23616, center) },
            new() { Id = 20, Oid = 0x32FD, Kind = 2, Position = center - Vector3.UnitZ * 10, TargetId = 100, Statuses = Slots(2056), Hp = 1000 },
            new() { Id = 21, Oid = 0x31A1, Kind = 2, Position = center - Vector3.UnitZ * 15, Cast = Spell(23619, center - Vector3.UnitZ * 15), Hp = 1000 },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000, Statuses = Slots(2644) },
            new() { Id = 101, Kind = 1, Position = center + Vector3.UnitX * 15, Hp = 1000 },
            new() { Id = 22, Oid = 0x233C, Kind = 2, Position = center + Vector3.UnitX * 5, Cast = Spell(23615, center + Vector3.UnitX * 5), Hp = 1000 },
        };
        ReplayPartyMember[] party = [new(0, "", 100), new(1, "", 101)];
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Frame(double at, MechanicFeedEvent[]? events = null)
        {
            var now = START.AddSeconds(at);
            session.BeginFrame(now);
            feed.ApplyFrame(new(975, 778, 100, 0, party.Select(p => new MechanicPartySlotDto(p.Slot, p.EntityId, p.EntityId, "")).ToArray(), [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            foreach (var e in events ?? []) feed.Enqueue(e);
            feed.Flush(); session.Update();
            region.Synchronize(actors.ToArray(), party, 100, now, true, () =>
            {
                foreach (var e in events ?? [])
                    switch (e)
                    {
                        case MechanicEventActionEffect effect: region.Effect(effect.CasterId, effect.ActionId, effect.MainTargetId); break;
                        case MechanicEventIcon icon: region.Icon(icon.SourceId, icon.IconId, icon.TargetId); break;
                    }
            });
            var source = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31A0);
            var viewer = session.World.Actors.Find(100)!; var native = model.Actors[100];
            Check(source.Components.Count == 8 && model.Active, "CE64 actual eight activated components");
            var expected = source.Components.OfType<GenericAOEs>().SelectMany(c => c.ActiveAOEs(0, viewer).ToArray()).ToArray();
            var actual = model.Visible(native).ToArray();
            Check(expected.Select(e => (e.Origin.ToVec3(), e.Rotation.Rad, e.Activation, e.Risky, e.Color))
                .SequenceEqual(actual.Select(a => (a.Cast.Origin, a.Cast.Rotation, a.Cast.Activation, a.Risky, a.Danger ? Colors.Danger : 0))),
                "CE64 actual Feed fields/order at " + at);
            var ai = new AIHints();
            foreach (var c in source.Components) c.AddAIHints(0, viewer, default, ai);
            var raw = model.Navigation(native).ToArray();
            var synthetic = new Fixture(25);
            try { CompareMetalFoxZones(synthetic, ai, raw, "CE64 Feed conversion at " + at); }
            finally { synthetic.Dispose(); }
            // Both previous calls have now filled their lazy participant
            // caches. Compare a second query on BOTH sides before Normalize.
            ai = new();
            foreach (var c in source.Components) c.AddAIHints(0, viewer, default, ai);
            ai.Normalize();
            var normalized = model.NavigationForHost(native);
            Check(ai.ForbiddenZones.Select(z => z.Item2).SequenceEqual(normalized.Select(z => z.Activation)), "CE64 normalized clocks");
            var eyes = source.Components.OfType<GenericGaze>().Single().ActiveEyes(0, viewer).ToArray();
            Check(eyes.Select(e => (e.Position.ToVec3(), e.Activation, e.Forward.Rad)).SequenceEqual(
                model.BurnEyes(native).Select(e => (e.Position, e.Activation, e.Forward))), "CE64 Feed weakpoint");
            Check(source.CalculateGlobalHints().SequenceEqual(model.GlobalHints())
                && source.CalculateHintsForRaidMember(0, viewer).Select(h => (h.Item1, h.Item2))
                    .SequenceEqual(model.Hints(native).Select(h => (h.Text, h.Risky))), "CE64 Feed hints");
            Check(ai.PredictedDamage.Select(d => (d.Players.Raw, d.Activation, d.Type.ToString()))
                .SequenceEqual(model.DamageForHost(native).Select(d => (d.Mask, d.Activation, d.Type))), "CE64 Feed damage");
        }
        Frame(0);
        actors[0] = actors[0] with { Cast = Spell(23607, center) };
        actors[1] = actors[1] with { Cast = Spell(23612, actors[1].Position), Statuses = [] };
        Frame(1);
        actors[0] = actors[0] with { Cast = Spell(24658, center) };
        Frame(2);
        Frame(3, [new MechanicEventIcon(261, 100, 22)]);
        Frame(4, [new MechanicEventActionEffect(22, 1, 24659, 0, 0, default, 0, 0, 0, []),
            new MechanicEventActionEffect(22, 1, 23610, 0, 0, default, 0, 0, 0, [])]);
        actors[0] = actors[0] with { Cast = Spell(23614, center) };
        actors[2] = actors[2] with { Cast = Spell(23606, actors[2].Position) };
        Frame(5);
        actors[3] = actors[3] with { Statuses = [] };
        Frame(6);
    }
}
