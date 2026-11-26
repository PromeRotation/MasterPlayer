using System.Collections;
using System.Numerics;
using System.Reflection;
using BossMod;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Replay;

internal static partial class BozjaRegression
{
    private const string TIME_COMPONENT = "TimeEruptionBombReproduce";

    private static void RunTime(Action<string, Action> run)
    {
        run("Bozja native CE52 actual client thirteen AIDs four source components", TimeClient);
        run("Bozja native CE52 clocks two three nine ten raw ListSort safe background independent", TimeClocks);
        run("Bozja native CE52 three way suppression empty bombs dynamic original cache no Bomb2", TimePriority);
        run("Bozja native CE52 cyclone FIFO cross caster Effect ordinary Finish destroy retain empty failure", TimeCyclones);
        run("Bozja native CE52 clock Effect caster axis tolerance first hidden delete safe clear", TimeClockEffects);
        run("Bozja native CE52 current elapsed cold primary bomb starts now plus23.2", TimeCold);
        run("Bozja native CE52 standard R8 Eruption exact raw Finish", TimeStandard);
        run("Bozja native CE52 source dynamic current raw damage not raidwide Effect", () => Damage(12));
        run("Bozja native CE52 actual Feed targetable pending animation before activation lost", TimeFeed);
        run("Bozja native CE52 ray rear ULP preserves source Check and SDF explicit bridge rounding difference", TimeRayBoundary);
    }

    private static void TimeRayBoundary()
    {
        using var f = new Fixture(12);
        var source = f.Activate<BossMod.Components.GenericAOEs>(TIME_COMPONENT);
        var origin = new Vector3(-550, 0, 1e-7f);
        f.Move(2, origin);
        var spell = f.Start(source, 23959);
        spell.Rotation = default;
        // Replace the fixture's initial .4 direction on both independent sides.
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(23959));
        f.M.Effect(2, 23959);
        f.Set(2, a => a with { Cast = a.Cast! with { Rotation = 0 } });
        source.OnCastStarted(f.W.Actors.Find(2)!, spell);
        f.Sync();
        var point = new Vector3(-550, 0, 0);
        var aoe = source.ActiveAOEs(0, f.Viewer)[0];
        var zone = f.M.Navigation(f.NativeViewer).Single();
        var ai = new AIHints();
        source.AddAIHints(0, f.Viewer, default, ai);
        Check(!aoe.Check(new(point.X, point.Z)) && !f.M.Visible().Single().Cast.Contains(point)
            && zone.Distance(point.X, point.Z) == 1e-7f
            && !zone.Convert(f.Center, 29.5f).Single().Contains(point, 0),
            "CE52 rear boundary retains the source origin rather than rounded rectangle center.");
        var bridge = MechanicHintsConvert.ConvertForbiddenZone(ai.ForbiddenZones[0].Item1,
            0, f.Center, f.M.SampleRadius, out _).Single();
        Check(bridge.Template.Resolve(bridge.Position, bridge.Rotation, null).Contains(point, 0),
            "CE52 documented adapter difference: old bridge rounds originZ+30 to30, including this rear point.");
    }

    private static void TimeClient()
    {
        Initialize();
        Check(Service.LuminaRow<Lumina.Excel.Sheets.DynamicEvent>(26)?.Name.ToString()
            == BozjaCatalog.Definitions[12].Name, "CE52 actual DE26.");
        foreach (var action in Enum.GetValues<BossMod.Shadowbringers.Foray.CriticalEngagement.CE52TimeToBurn.AID>())
            Check(new ActionID(ActionType.Spell, (uint)action).CastTimeExtra() == 0, "CE52 source AID extra " + action);
        Check(BozjaCatalog.Definitions[12].Rules.Select(r => r.Component).SequenceEqual(
            new[] { TIME_COMPONENT, "Eruption", "FireTankbuster", "FireRaidwide" }), "CE52 activation order.");
    }

    private static void TimeAnimation(Fixture f, BossMod.Components.GenericAOEs source, ulong id, uint state)
    {
        source.OnActorEAnim(f.W.Actors.Find(id)!, state);
        f.M.EAnim(id, state);
    }

    private static void TimeCompare(Fixture f, BossMod.Components.GenericAOEs source, string step)
    {
        CompareMetalFoxBits(f, source, TIME_COMPONENT, "CE52 " + step);
        var safe = (List<WPos>)source.GetType().GetField("_eruptionSafeSpots", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(source)!;
        Check(safe.Select(p => p.ToVec3()).SequenceEqual(f.M.TimeSafeSpots), "CE52 safe-only background positions/order.");
        var sink = new MechanicDrawSink(new());
        using (MechanicDrawSink.Install(sink)) source.DrawArenaBackground(0, f.Viewer);
        var rectangles = sink.Commands.OfType<MechanicDrawZoneRect>().Where(r => r.Color == Colors.SafeFromAOE).ToArray();
        Check(rectangles.Select(r => r.Origin.ToVec3()).SequenceEqual(f.M.TimeSafeSpots)
            && rectangles.All(r => r.LenFront == 10 && r.LenBack == 10 && r.HalfWidth == 10 && !r.Outline),
            "CE52 actual source safe-only background geometry/color stays outside AI.");
    }

    private static void TimeClocks()
    {
        using var f = new Fixture(12);
        var source = f.Activate<BossMod.Components.GenericAOEs>(TIME_COMPONENT);
        for (ulong id = 10; id < 20; ++id) f.Add(id, 0x1EB17A, f.Center + new Vector3((int)id - 15, 0, 7));
        f.Sync();
        TimeAnimation(f, source, 2, 0x10002);
        TimeAnimation(f, source, 10, 0x99999);
        f.Start(source, 23955);
        for (ulong id = 10; id < 20; ++id)
        {
            TimeAnimation(f, source, id, id is 10 or 11 ? 0x10020u : 0x10002u);
            TimeCompare(f, source, "clock count " + (id - 9));
            Check(f.M.Visible().Count() == (id - 9 > 2 ? (int)id - 11 : (int)id - 9), "CE52 count>2 subtract exactly two.");
        }
        Check(f.M.TimeSafeSpots.Count == 2, "CE52 only exactly nine creates two safe backgrounds.");
        f.Move(10, f.Center + Vector3.UnitX * 20);
        f.Remove(11);
        TimeCompare(f, source, "move/destroy cannot move saved clock positions");
        f.Now = START.AddSeconds(10); f.W.Frame.Timestamp = f.Now; f.Sync();
        source.OnCastStarted(f.W.Actors.Find(2)!, f.W.Actors.Find(2)!.CastInfo!); f.M.Start(f.M.Actors[2]);
        TimeCompare(f, source, "repeated StartTime restarts all stored delay clocks");
        Check(f.M.Visible().All(a => a.Cast.Activation >= f.Now.AddSeconds(9.7d)), "CE52 clock start is current time.");
    }

    private static void TimePriority()
    {
        using var f = new Fixture(12);
        var source = f.Activate<BossMod.Components.GenericAOEs>(TIME_COMPONENT);
        for (ulong id = 10; id < 19; ++id)
        {
            f.Add(id, 0x1EB17A, f.Center + Vector3.UnitX * (id - 9)); f.Sync();
            TimeAnimation(f, source, id, id < 17 ? 0x10002u : 0x10020u);
        }
        f.Start(source, 23955);
        f.Start(source, 23959);
        TimeCompare(f, source, "cyclone hides clock");
        f.Start(source, 23957, 2);
        TimeCompare(f, source, "nonprimary bomb visual cannot suppress");
        f.Start(source, 23957, 1);
        TimeCompare(f, source, "active empty bomb cache suppresses cyclone and clock");
        Check(!f.M.Visible().Any() && f.M.TimeSafeSpots.Count == 2,
            "CE52 empty bomb cache still suppresses dangers while safe-only drawing remains.");
        f.Add(20, 0x1EB17B, f.Center + Vector3.UnitZ * 4);
        f.Add(21, 0x1EB1D4, f.Center - Vector3.UnitZ * 4); f.Sync();
        TimeCompare(f, source, "Bomb1 only, Bomb2 ignored");
        Check(f.M.Visible().Count() == 1, "CE52 source consumes only Bomb1 OID.");
        f.Set(20, a => a with { Dead = true, Rotation = 1.2f });
        f.W.Actors.Find(20)!.IsDead = true; f.W.Actors.Find(20)!.PosRot.W = 1.2f; f.Sync();
        f.Move(20, f.Center + Vector3.UnitX * 13.123f);
        TimeCompare(f, source, "dead bomb original current pose no quantization");
        f.Remove(20); TimeCompare(f, source, "all bombs gone still hides");
        f.Add(20, 0x1EB17B, f.Center + Vector3.UnitZ * 15); f.Sync();
        TimeCompare(f, source, "same ID bomb new cache generation");
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(23958)); f.M.Effect(2, 23958);
        TimeCompare(f, source, "bomb Effect promotes cyclone");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23959)); f.M.Effect(1, 23959);
        TimeCompare(f, source, "cross source cyclone Effect promotes clock");
    }

    private static void TimeCyclones()
    {
        using var f = new Fixture(12);
        var source = f.Activate<BossMod.Components.GenericAOEs>(TIME_COMPONENT);
        f.Add(10, 0x233C, f.Center + Vector3.UnitX * 10); f.Sync();
        var first = f.Start(source, 23959, 2, total: 3);
        var second = f.Start(source, 23959, 10, total: 8);
        source.OnCastFinished(f.W.Actors.Find(10)!, second); f.M.Finish(f.M.Actors[10], f.M.Actors[10].Cast!);
        f.Remove(10); TimeCompare(f, source, "normal Finish and destruction retain both queue entries");
        source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23959)); f.M.Effect(1, 23959);
        TimeCompare(f, source, "unrelated Effect removes FIFO first");
        Check(f.M.Casts.Single().Source.Actor.Id == 10, "CE52 removed first entry regardless of Effect caster.");
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(23959)); f.M.Effect(2, 23959);
        TimeCompare(f, source, "second Effect empties");
        var sourceFailed = false; var nativeFailed = false;
        try { source.OnEventCast(f.W.Actors.Find(1)!, PatriotEvent(23959)); }
        catch (ArgumentOutOfRangeException) { sourceFailed = true; }
        try { f.M.Effect(1, 23959); }
        catch (ArgumentOutOfRangeException) { nativeFailed = true; }
        Check(sourceFailed && nativeFailed && f.M.TimeSequenceError?.Contains("空队列") == true,
            "CE52 malformed empty FIFO must report failure on both sides.");
    }

    private static void TimeClockEffects()
    {
        using var f = new Fixture(12);
        var source = f.Activate<BossMod.Components.GenericAOEs>(TIME_COMPONENT);
        for (ulong id = 10; id < 19; ++id)
        {
            f.Add(id, 0x1EB17A, f.Center + new Vector3((int)id - 10, 0, 5)); f.Sync();
            TimeAnimation(f, source, id, id < 17 ? 0x10002u : 0x10020u);
        }
        f.Start(source, 23955);
        Check(f.M.TimeSafeSpots.Count == 2, "CE52 nine-clock safe backgrounds.");
        f.Move(2, f.Center + new Vector3(20, 0, 20));
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(23956)); f.M.Effect(2, 23956, position: f.Center + Vector3.UnitZ * 5);
        TimeCompare(f, source, "no matching caster still clears safe, ignores target position");
        Check(f.M.TimeSafeSpots.Count == 0 && f.M.Visible().Count() == 7, "CE52 unmatched Effect clears only drawing.");
        f.Move(2, f.Center + new Vector3(.9f, 0, 5.9f));
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(23956)); f.M.Effect(2, 23956);
        TimeCompare(f, source, "axis tolerance removes first of multiple close clocks");
        f.Move(2, f.Center + new Vector3(9, 0, 6));
        source.OnEventCast(f.W.Actors.Find(2)!, PatriotEvent(23956)); f.M.Effect(2, 23956);
        TimeCompare(f, source, "exact axis1 can remove hidden final clock");
    }

    private static void TimeCold()
    {
        using var f = new Fixture(12);
        f.Now = START.AddSeconds(20); f.W.Frame.Timestamp = f.Now;
        f.W.Actors.Find(1)!.CastInfo = new() { Action = new(ActionType.Spell, 23957), TotalTime = 3, ElapsedTime = 3 };
        f.Set(1, a => a with { Cast = new(23957, 1, f.Center, 0, START, START.AddSeconds(3.3), false, true)
            { TotalTime = 3, IsSpell = true } });
        f.M.Clear(); f.Sync();
        f.Add(20, 0x1EB17B, f.Center + Vector3.UnitX * 5); f.Sync();
        var source = f.Activate<BossMod.Components.GenericAOEs>(TIME_COMPONENT);
        TimeCompare(f, source, "cold elapsed primary visual starts new current23.2 timer");
        Check(f.M.Visible().Single().Cast.Activation == f.Now.AddSeconds(23.2d), "CE52 cold bomb timer is not old Finish.");
    }

    private static void TimeStandard()
    {
        using var f = new Fixture(12);
        var source = f.Activate<BossMod.Components.GenericAOEs>("Eruption");
        var spell = f.Start(source, 23960);
        CompareMetalFoxBits(f, source, "Eruption", "CE52 standard R8");
        source.OnCastFinished(f.W.Actors.Find(2)!, spell); f.M.Finish(f.M.Actors[2], f.M.Actors[2].Cast!);
        CompareMetalFoxBits(f, source, "Eruption", "CE52 standard watched Finish");
    }

    private static void TimeFeed()
    {
        Initialize();
        using var session = new MechanicSession("CE52-pending-eanim-feed");
        var feed = new MechanicWorldFeed(session);
        var region = new BozjaRegion(975);
        var model = region.Encounters.Single(e => e.Definition.PrimaryOid == 0x31C8);
        var center = model.Center;
        var actors = new ReplayActorState[]
        {
            new() { Id = 1, Oid = 0x31C8, Kind = 2, SubKind = 5, Position = center, Hp = 1000, InCombat = true, Targetable = false },
            new() { Id = 100, Kind = 1, Position = center, Hp = 1000 },
            new() { Id = 10, Oid = 0x1EB17A, Kind = 7, Position = center + Vector3.UnitZ * 4, Hp = 1000 },
            new() { Id = 2, Oid = 0x233C, Kind = 2, SubKind = 5, Position = center, Hp = 1000,
                Cast = new(23955, 1, center, 0, START, START.AddSeconds(3.3), false, true) { TotalTime = 3, IsSpell = true } },
        };
        var converter = typeof(ReplayFeedAdapter).GetMethod("ConvertActor", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Frame(double seconds, bool animation)
        {
            var now = START.AddSeconds(seconds);
            session.BeginFrame(now);
            feed.ApplyFrame(new(975, 778, 100, 0, [new(0, 100, 100, "")], [],
                actors.Select((a, i) => (MechanicActorSnapshot)converter.Invoke(null, [a, i, now])!).ToArray()));
            if (animation) feed.Enqueue(new MechanicEventEObjAnimation(10, 1, 2));
            feed.Flush(); session.Update();
            region.Synchronize(actors, [new(0, "", 100)], 100, now, true,
                () => { if (animation) region.EAnim(10, 0x10002); });
        }
        Frame(0, true);
        Check(model.Primary == null && region.Loaded.Count == 0 && session.BossModules.LoadedModules.Count == 0,
            "CE52 untargetable module stays pending, not merely inactive loaded.");
        actors[0] = actors[0] with { Targetable = true };
        Frame(1, true);
        Check(model.Active && !model.Visible().Any(), "CE52 animation before first activation lost, not cold-replayed.");
        Frame(2, true);
        var module = session.BossModules.LoadedModules.Single(m => m.PrimaryActor.OID == 0x31C8);
        var source = module.Components.OfType<BossMod.Components.GenericAOEs>().Single(c => c.GetType().Name == TIME_COMPONENT);
        var expected = source.ActiveAOEs(0, session.World.Actors.Find(100)!).ToArray();
        Check(expected.Select(a => (a.Origin.ToVec3(), a.Activation))
            .SequenceEqual(model.Visible().Select(a => (a.Cast.Origin, a.Cast.Activation))) && expected.Length == 1,
            "CE52 active true Feed animation and cold StartTime clock match.");
        Frame(3, true);
        Check(model.Visible().Count() == 2 && source.ActiveAOEs(0, session.World.Actors.Find(100)!).Length == 2,
            "CE52 repeated same clock state event adds a second record.");
        Frame(4, true);
        Check(model.Visible().Count() == 1 && source.ActiveAOEs(0, session.World.Actors.Find(100)!).Length == 1,
            "CE52 third duplicate clock makes the last two hidden without deduplication.");
    }
}
