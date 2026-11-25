using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class EurekaTwintaniaRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-600, 0, -300);
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(1102);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x3D1D, Center, InCombat: true, Hp: 100),
            new(2, 0x233C, Center),
            new(100, 0, Center + Vector3.UnitZ * 8, PartySlot: 0, Player: true),
            new(101, 0, Center + Vector3.UnitX * 8, PartySlot: 1, Player: true),
        ];
        internal DateTime Now = Start;
        internal Fixture()
        {
            E.CurrentPartyMembers = [new(0, "First", 100), new(1, "Second", 101)];
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.First(a => a.Id == 100).Position, Now, playerId: 100);
        internal void Cast(uint action, float duration = 5, ulong source = 2, ulong target = 0)
        {
            Now = Now.AddMilliseconds(10);
            Set(source, a => a with { Cast = new(action, a.Position, 0, Now, Now.AddSeconds(duration + .3), target, TotalTime: duration) });
            Sync();
        }
        internal void End(ulong source = 2)
        {
            Set(source, a => a with { Cast = null });
            Sync();
        }
    }
    internal static void Run(Action<string, Action> run)
    {
        run("Eureka40 eight activated components exact circle and primary-only ownership", Catalog);
        run("Eureka40 independent twister pre-end actual Update sampling float clock and no Finish resample", Sampling);
        run("Eureka40 early Finish fallback samples living source party now plus point4", EarlyFinish);
        run("Eureka40 State7 blocks Update Created clears once independent predictions survive old object", Objects);
        run("Eureka40 source-index Finish and Created interleave and event refresh never consumes component Update", EventOrder);
        run("Eureka40 Timeline latch repeated lock original pose and any real Dive Finish withdrawal", Dive);
        run("Eureka40 Gust raw target event point9 live quantization one-match per actor per Update", Wind);
        run("Eureka40 Turbine ignores immunity unbounded15 no wall source model and intersection not union", Turbine);
        run("Eureka40 production output actual solver preview navigation and owner withdrawal", Output);
    }
    private static void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); }
    private static void Catalog()
    {
        var f = new Fixture();
        Check(f.E.Active?.BossOid == 0x3D1D && ArrDungeonCatalog.Owns(0x3D1D, 1102)
            && !EurekaOrthosCatalog.Owns(1102, 0x3D1E), "Ownership includes floor or helper.");
        Check(f.E.Arena!.Position == Center && f.E.Arena.Contains(Center + Vector3.UnitX * 20, 0)
            && !f.E.Arena.Contains(Center + Vector3.UnitX * 20.1f, 0), "Arena differs.");
        f.Cast(31466);
        Check(f.E.OutputCasts.Single().Value.Rule.Shape == new CircleTemplate(5), "Squall real radius changed.");
    }
    private static void Sampling()
    {
        var f = new Fixture(); f.Cast(31468);
        var deadline = f.E.EurekaTwister.PredictStart;
        Check(f.E.EurekaTwister.Cast is { Source: 2, Observed: true }
            && deadline == f.Now.AddSeconds(5.3).AddSeconds(-.25), "Source cast identity NPC delay or threshold changed.");
        f.Now = deadline.AddTicks(-1); f.Sync();
        Check(f.E.EurekaTwister.Positions.Count == 0, "Sampled before source threshold.");
        f.Set(100, a => a with { Position = Center + Vector3.UnitZ * 11.137f });
        f.Now = deadline.AddMilliseconds(27); f.Sync();
        var activation = f.Now.AddSeconds((double)(.25f + .4f));
        Check(f.E.EurekaTwister.Positions.Count == 2 && f.E.EurekaTwister.Positions[0].Z == Center.Z + 11.137f
            && f.E.EurekaTwister.Activation == activation && f.E.EurekaDiveTwister.Positions.Count == 0,
            "Update failed actual unquantized positions independent schedule or float delay.");
        f.Set(100, a => a with { Position = Center }); f.End();
        Check(f.E.EurekaTwister.Activation == activation && f.E.EurekaTwister.Positions[0] != Center,
            "Finish resampled completed prediction.");
        f.Now = f.Now.AddSeconds(100); f.Sync();
        Check(f.E.EurekaTwister.Positions.Count == 2, "Prediction expired without Created.");
    }
    private static void EarlyFinish()
    {
        var f = new Fixture(); f.Cast(31468);
        f.Set(101, a => a with { Dead = true });
        f.Now = f.Now.AddSeconds(1); f.End();
        Check(f.E.EurekaTwister.Positions.Count == 1
            && f.E.EurekaTwister.Activation == f.Now.AddSeconds((double).4f), "Early Finish lost alive party or used forecast time.");
    }
    private static void Objects()
    {
        var f = new Fixture();
        f.Actors.Add(new(20, 0x1E8910, Center, EventState: 7)); f.Sync(); f.Cast(31468);
        f.Now = f.Now.AddSeconds(6); f.Sync();
        Check(f.E.EurekaTwister.Positions.Count == 0, "State7 did not block Update.");
        f.End();
        Check(f.E.EurekaTwister.Positions.Count == 2, "Existing object blocked Finish fallback.");
        f.Sync();
        Check(f.E.EurekaTwister.Positions.Count == 2, "Old object cleared new prediction every frame.");
        f.Cast(31471); f.End();
        Check(f.E.EurekaDiveTwister.Positions.Count == 2, "Second component schedule merged.");
        f.Actors.Add(new(21, 0x1E8910, Center, EventState: 7)); f.Sync();
        Check(f.E.EurekaTwister.Positions.Count == 0 && f.E.EurekaDiveTwister.Positions.Count == 0,
            "New State7 Created failed both clears.");
        f.Cast(31468); f.Now = f.Now.AddSeconds(6); f.Sync();
        f.Actors.RemoveAll(a => a.Oid == 0x1E8910); f.Sync();
        Check(f.E.EurekaTwister.Positions.Count == 2, "Destroy did not unblock pending Update.");
    }
    private static void EventOrder()
    {
        var f = new Fixture();
        f.Set(2, a => a with { ObjectIndex = 10 });
        f.Actors.Add(new(20, 0x1E8910, Center, EventState: 7, ObjectIndex: 20)); f.Sync();
        f.Cast(31468); f.Now = f.Now.AddSeconds(1);
        f.Set(2, a => a with { Cast = null });
        f.Actors.Insert(0, new(21, 0x1E8910, Center, EventState: 7, ObjectIndex: 21)); f.Sync();
        Check(f.E.EurekaTwister.Positions.Count == 0, "Later Created failed to clear earlier caster Finish prediction.");
        var reverse = new Fixture();
        reverse.Set(2, a => a with { ObjectIndex = 21 });
        reverse.Actors.Add(new(20, 0x1E8910, Center, EventState: 7, ObjectIndex: 20)); reverse.Sync();
        reverse.Cast(31468); reverse.Now = reverse.Now.AddSeconds(1);
        reverse.Set(2, a => a with { Cast = null });
        reverse.Actors.Add(new(21, 0x1E8910, Center, EventState: 7, ObjectIndex: 10)); reverse.Sync();
        Check(reverse.E.EurekaTwister.Positions.Count == 2,
            "Earlier Created must precede later caster Finish even when inserted last.");
        var wind = new Fixture();
        wind.Actors.Add(new(30, 0x3D1E, Center)); wind.Sync();
        var adapter = new DungeonEventAdapter(wind.E, wind.Sync, _ => 0, _ => { });
        var packet = new NativeActionEffectEvent { SourceId = 2, ActionId = 31463, MainTargetId = 2, Targets = [], ActionType = 1 };
        adapter.Handle(packet, wind.Now); adapter.Handle(packet, wind.Now);
        Check(wind.E.AmbientAoes().Count(a => a.Id.StartsWith("trial/voidzone/event/")) == 2,
            "Event input refresh consumed source component updates.");
        wind.Sync();
        Check(wind.E.AmbientAoes().Count(a => a.Id.StartsWith("trial/voidzone/event/")) == 1,
            "One actual Update did not consume exactly one prediction per live source.");
        var pose = new Fixture(); pose.E.Timeline(1, 0x1E3A, pose.Now);
        pose.Set(1, a => a with { Position = Center + Vector3.UnitX * 3, Rotation = .9f });
        var timelineAdapter = new DungeonEventAdapter(pose.E, pose.Sync, _ => 0, _ => { });
        timelineAdapter.Handle(new PromeRotation.LogSystem.LogSystemPlayActionTimelineEvent
            { SourceId = 1, TimelineId = 0x1E43 }, pose.Now);
        var dive = pose.E.AmbientAoes().Single(a => a.Id == "eureka/dive");
        Check(dive.Position == DungeonEncounter.QuantizedDungeonPosition(Center + Vector3.UnitX * 3)
            && dive.Rotation == .9f, "Known-primary Timeline froze stale cached pose.");
    }
    private static void Dive()
    {
        var f = new Fixture();
        f.E.Timeline(1, 0x1E43, f.Now);
        Check(!f.E.AmbientAoes().Any(a => a.Id == "eureka/dive"), "Unprepared Timeline created dive.");
        f.E.Timeline(1, 0x1E3A, f.Now); f.E.Timeline(2, 0x1E43, f.Now);
        Check(!f.E.AmbientAoes().Any(a => a.Id == "eureka/dive"), "Non-primary Timeline created dive.");
        f.E.Timeline(1, 0x1E43, f.Now);
        f.Now = f.Now.AddSeconds(1); f.Set(1, a => a with { Position = Center + Vector3.UnitX, Rotation = 1.25f }); f.Sync();
        f.E.Timeline(1, 0x1E43, f.Now.AddSeconds(-10));
        var dive = f.E.AmbientAoes().Single(a => a.Id == "eureka/dive");
        Check(dive.Position == DungeonEncounter.QuantizedDungeonPosition(Center + Vector3.UnitX) && dive.Rotation == 1.25f
            && dive.Activation == f.Now.AddSeconds(6.9), "Repeated lock did not use consuming clock and current original pose.");
        f.Cast(31466); f.End();
        Check(f.E.AmbientAoes().Any(a => a.Id == "eureka/dive"), "Other Finish cleared dive.");
        f.Cast(31471); f.End();
        Check(!f.E.AmbientAoes().Any(a => a.Id == "eureka/dive"), "Helper Dive Finish failed clear.");
    }
    private static void Wind()
    {
        var f = new Fixture(); f.Cast(31463, target: 101);
        f.Set(101, a => a with { Position = Center - Vector3.UnitX * 5 }); f.Sync();
        Check(f.E.AmbientAoes().Single(a => a.Id.StartsWith("trial/voidzone/cast/")).Position == Center - Vector3.UnitX * 5,
            "Cast target not dynamic.");
        var adapter = new DungeonEventAdapter(f.E, f.Sync, _ => 0, _ => { });
        var packet = new NativeActionEffectEvent { SourceId = 2, ActionId = 31463, MainTargetId = 101, GlobalSequence = 7, Targets = [] };
        adapter.Handle(packet, f.Now);
        adapter.Handle(packet, f.Now);
        var predictions = f.E.AmbientAoes().Where(a => a.Id.StartsWith("trial/voidzone/event/")).ToArray();
        Check(predictions.Length == 2 && predictions.All(a => a.Activation == f.Now.AddSeconds((double).9f)),
            "Repeated effects or source float delay changed.");
        f.End();
        f.Actors.Add(new(20, 0x3D1E, Center - Vector3.UnitX * 5, Dead: true, Targetable: false)); f.Sync();
        Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("trial/voidzone/event/")) == 1, "Live consumed more than one prediction.");
        Check(f.E.AmbientAoes().Single(a => a.Id.StartsWith("trial/voidzone/live/")).Position
            == DungeonEncounter.QuantizedDungeonPosition(Center - Vector3.UnitX * 5), "Live not quantized.");
        f.Set(20, a => a with { EventState = 7 }); f.Sync();
        Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("trial/voidzone/event/")) == 1
            && !f.E.AmbientAoes().Any(a => a.Id.StartsWith("trial/voidzone/live/")), "State7 consumed event prediction.");
        var packetOnly = new Fixture();
        packetOnly.E.StartCast(2, new(31463, Center, 0, Start, Start.AddSeconds(4.3), 101), Start);
        Check(!packetOnly.E.AmbientAoes().Any(a => a.Id.StartsWith("trial/voidzone/cast/")),
            "Packet-only cast invented a source raw wind.");
    }
    private static void Turbine()
    {
        var f = new Fixture(); f.Cast(31467);
        var source = f.E.EurekaTurbines.Single();
        Check(source.IgnoreImmunes && !source.StopAtWall && source.Distance == 15 && source.Cast.Source == 2,
            "Knockback model discarded source parameters.");
        var movement = f.E.EurekaTurbineMovements(f.Actors.Single(a => a.Id == 100)).Single();
        Check(movement.To == Center + Vector3.UnitZ * 23 && movement.Unsafe, "Wall clamped or range qualified away movement.");
        Check(DungeonOutput.EurekaTurbineDistance(Center, [], Center.X, Center.Z + 8) < 0
            && DungeonOutput.EurekaTurbineDistance(Center, [], Center.X, Center.Z + 3) > 0, "No-wind constraint differs.");
        Vector3[] opposed = [Center + Vector3.UnitZ * 10, Center - Vector3.UnitZ * 10];
        Check(DungeonOutput.EurekaTurbineDistance(Center, opposed, Center.X, Center.Z + 8) > 0,
            "Opposite cones became union instead of intersection.");
        Vector3[] forward = [Center + Vector3.UnitZ * 10];
        Check(DungeonOutput.EurekaTurbineDistance(Center, forward, Center.X, Center.Z + 8) < 0
            && DungeonOutput.EurekaTurbineDistance(Center, forward, Center.X, Center.Z - 8) > 0,
            "Source cone normals reversed the wind direction.");
    }
    private static void Output()
    {
        var f = new Fixture(); f.Cast(31467);
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = f.Now };
        var draw = new ScriptDrawApi(); var output = new DungeonOutput(safe, draw, safe.Manager);
        output.Apply(f.E, f.Now, false);
        var snapshot = safe.Manager.BuildSnapshot(f.Now);
        Check(snapshot.DangerAreas.Count > 0 && draw.Previews.Values.Any(p => p.Id.Contains("eureka/turbine/")),
            "Production adapter lost intersection or movement preview.");
        var result = new SafeZoneSolver().Solve(snapshot,
            new(Center + Vector3.UnitZ * 8, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions(), f.Now);
        Check(result.IsSuccess && !result.IsLeastDangerFallback, "Solver failed no-wind center constraint.");
        output.Clear();
        Check(draw.Previews.Count == 0 && safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 0, "Owner cleanup retained constraints.");
    }
}
