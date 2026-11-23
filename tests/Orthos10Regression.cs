using System.Numerics;
using System.Reflection;
using System.Text.Json;
using BossMod;
using BossMod.Components;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;
using MasterPlayer.Spatial.SafeZoneReborn;

internal static class Orthos10Regression
{
    private static Lumina.GameData? data;
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
    public static void Run(Action<string, Action> run)
    {
        run("EO10 self-targeted native casts retain real mine origins", () =>
        {
            using var f = new Fight();
            f.Cast(2, 31478, new(-287, 0, -287), 4.7f);
            var a = f.Aoes().Single();
            Check(Vector3.Distance(a.Origin.ToVec3(), new(-287, 0, -287)) < .01f, $"Mine AOE moved to {a.Origin} instead of caster.");
            Check(Math.Abs((a.Activation - Start).TotalSeconds - 5) < .001, "Mine hit timing lost.");
        });
        run("EO10 staggered waves retain all nine pending mines", () =>
        {
            using var f = new Fight();
            for (var i = 0; i < 9; ++i)
            {
                f.At((i / 3) * 2);
                f.Cast((ulong)(i + 2), 32700, new(-313 + i % 3 * 13, 0, -313 + i / 3 * 13), 7.7f);
            }
            Check(f.Aoes().Length == 9, $"Only {f.Aoes().Length} of nine mines are exposed to navigation.");
            Check(f.Aoes().Select(a => a.Activation).Distinct().Count() == 3, "Staggered deadlines collapsed.");
        });
        run("EO10 cancel, duplicate, effect, death and timeout cleanup", () =>
        {
            using var f = new Fight();
            f.Cast(2, 31478, new(-300, 0, -300), 4.7f);
            var actor = f.Session.World.Actors.Find(2)!;
            var component = f.Module.Components.OfType<GenericAOEs>().Single(c => c.WatchedAction == 31478);
            component.OnCastStarted(actor, actor.CastInfo!);
            Check(f.Aoes().Length == 1, "Duplicate cast duplicated danger.");
            f.Session.Execute(new ActorState.OpCastInfo(2, null));
            Check(f.Aoes().Length == 0, "Cancelled cast left danger.");
            f.Cast(2, 31478, new(-300, 0, -300), 4.7f);
            f.At(4.7);
            f.Session.Execute(new ActorState.OpCastInfo(2, null));
            Check(f.Aoes().Length == 1, "Native cast ending removed danger before the real hit.");
            f.At(5);
            f.Hit(2, 31478);
            Check(f.Aoes().Length == 0, "Actual effect retained danger.");
            f.Cast(2, 31478, new(-300, 0, -300), 4.7f);
            component.OnActorDeath(actor);
            Check(f.Aoes().Length == 0, "Dead mine retained danger.");
            f.Cast(2, 31478, new(-300, 0, -300), 4.7f);
            component.OnActorDestroyed(actor);
            Check(f.Aoes().Length == 0, "Destroyed mine retained danger.");
            f.Cast(3, 31478, new(-287, 0, -287), 4.7f);
            f.At(11);
            Check(f.Aoes().Length == 0, "Missing effect leaked danger indefinitely.");
        });
        run("EO10 ARR footprints feed MP solver at normal speed", () =>
        {
            var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/EurekaOrthos/eo10-001327.json")));
            var count = 0;
            foreach (var wave in fixture.RootElement.GetProperty("casts").EnumerateArray().GroupBy(c => c.GetProperty("t").GetDouble()))
            {
                using var f = new Fight();
                var action = wave.First().GetProperty("action").GetUInt32();
                foreach (var c in wave)
                {
                    var p = c.GetProperty("pos").EnumerateArray().Select(x => x.GetSingle()).ToArray();
                    f.Cast(action == 31479 ? 1UL : c.GetProperty("source").GetUInt64() + 10,
                        action, new(p[0], p[1], p[2]), c.GetProperty("seconds").GetSingle());
                }
                var aoes = f.Aoes();
                Check(aoes.Length == wave.Count(), "Recorded wave lost AOE instances.");
                foreach (var a in aoes)
                {
                    var radius = action == 31479 ? 20 : 10;
                    Check(a.Shape is AOEShapeCircle circle && circle.Radius == radius, "BMR radius changed.");
                    var pos = a.Origin.ToVec3();
                    var hints = new AIHints();
                    foreach (var c in f.Module.Components) c.AddAIHints(0, f.Player, default, hints);
                    var dangers = MechanicNavAreaDiffer.TimedRequests(hints, 0, new(-300, 0, -300), 28)
                        .SelectMany((z, i) => z.Primitives.Select(p => new DangerAreaSnapshot(new((ulong)i + 1, AreaKind.Danger),
                            p.Template.Resolve(p.Position, p.Rotation, null), [z.ExplicitWindow!.Value]))).ToArray();
                    var snapshot = new SafeZoneSnapshot(1, Start, new RectangleGeometry(new(-300, 0, -300), 0, 39, 39), [], dangers, []);
                    var result = new SafeZoneSolver().Solve(snapshot, new(pos, 0, 6, false, true, 2.8f, true, 1),
                        new(6, 6, 0, false, 6, 6), new() { ArenaSafetyMargin = 0 }, Start);
                    Check(result.IsSuccess && result.ExecutablePath.Count > 1, $"AID {action}: {result.Status}, no escape from {pos}.");
                    var end = result.ExecutablePath[^1];
                    Check(!dangers.Any(d => d.Geometry.Contains(end.Position)), $"AID {action}: endpoint remains inside a recorded danger.");
                    foreach (var point in result.ExecutablePath)
                        Check(snapshot.ArenaBoundary!.Contains(point.Position), "Escape left the boss arena.");
                    ++count;
                }
            }
            Console.WriteLine($"  ARR: {count} recorded AOE escape solves; 6 y/s, casting BLM, 39x39 arena");
        });
        run("EO10 full ARR sequence dispatches safe movement while BLM casts", () => SimulateRecording("eo10-001327.json", false));
        run("EO10 waiting refuge stays still until the first mine wave opens", () => SimulateRecording("eo10-025331.json", true));
        run("EO10 recorded approach holds the third wave then crosses the first", () => SimulateRecording("eo10-025331.json", true, true));
        run("EO10 waiting refuge remains stable with a 300ms worker delay", () => SimulateRecording("eo10-025331.json", true, true, .3));
    }
    private static void SimulateRecording(string fixture, bool inspectWaiting, bool recordedApproach = false, double workerDelay = .1)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/EurekaOrthos/" + fixture)));
        var casts = document.RootElement.GetProperty("casts").EnumerateArray()
            .Where(c => !recordedApproach || c.GetProperty("t").GetDouble() is > 157 and < 164).ToArray();
        using var f = new Fight();
        using var manager = new SafeZoneManager(new() { EnableSafeZone = true, EnableAutoMovement = true });
        var now = Start;
        var registry = new SafeZoneRegistry(manager, () => now);
        var differ = new MechanicNavAreaDiffer();
        registry.SetArenaBoundary(new RectangleGeometry(new(-300, 0, -300), 0, 39, 39));
        var pair = ((object, IGreenMoveSystem))typeof(LateDodgeRegression).GetMethod("CreateMovement", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        using var movement = (SafeZoneRebornMovementSystem)pair.Item1;
        var advance = pair.Item2.GetType().GetMethod("Advance")!;
        var dispatch = movement.GetType().GetMethod("Dispatch", BindingFlags.Instance | BindingFlags.NonPublic)!;
        // The cropped approach starts from the second ARR's last player packet
        // before this wave. Full sequences keep the original neutral test start.
        var position = recordedApproach ? new Vector3(-290.45548f, 0, -283.3753f) : new Vector3(-299.916f, 0, -284.23f);
        var started = new HashSet<int>();
        var finished = new HashSet<int>();
        var hit = new HashSet<int>();
        var hints = new AIHints();
        SafeZoneSolution? latest = null, pending = null;
        var ready = 0d;
        var collisions = 0;
        var moves = 0;
        Vector3? refuge = null;
        var refugeAt = 0d;
        var waitDrift = 0f;
        double? crossedAt = null;
        // This sequence is the north/south column pattern around boss-relative
        // 2:51. X > -308 is inside the center column that explodes first.
        var firstHit = !inspectWaiting ? 0 : casts.Where(c => c.GetProperty("action").GetUInt32() == 32700 && c.GetProperty("t").GetDouble() is > 157 and < 163)
            .Min(c => c.GetProperty("hit").GetDouble());
        var until = casts.Max(c => c.GetProperty("t").GetDouble() + c.GetProperty("seconds").GetDouble()) + 2;
        for (var tick = recordedApproach ? 3140 : 0; tick * .05 <= until; ++tick)
        {
            var t = tick * .05;
            now = Start.AddSeconds(t);
            f.At(t);
            for (var i = 0; i < casts.Length; ++i)
            {
                var c = casts[i]; var at = c.GetProperty("t").GetDouble();
                var action = c.GetProperty("action").GetUInt32();
                var id = action == 31479 ? 1UL : c.GetProperty("source").GetUInt64() + 10;
                var p = c.GetProperty("pos").EnumerateArray().Select(x => x.GetSingle()).ToArray();
                var origin = new Vector3(p[0], 0, p[2]);
                if (t >= at && started.Add(i)) f.Cast(id, action, origin, c.GetProperty("seconds").GetSingle());
                if (t >= at + c.GetProperty("seconds").GetDouble() && finished.Add(i))
                    f.Session.Execute(new ActorState.OpCastInfo(id, null));
                if (c.GetProperty("hit").ValueKind == JsonValueKind.Number && t >= c.GetProperty("hit").GetDouble() && hit.Add(i))
                {
                    if (Vector3.Distance(position, origin) <= (action == 31479 ? 20 : 10)) ++collisions;
                    f.Hit(id, action);
                }
            }
            f.Session.Execute(new ActorState.OpMove(100, new(position, 0)));
            hints.Clear();
            foreach (var component in f.Module.Components) component.AddAIHints(0, f.Player, default, hints);
            var requests = MechanicNavAreaDiffer.TimedRequests(hints, 0, new(-300, 0, -300), 28).ToArray();
            differ.Sync(registry, manager, requests, [], .5f, null);
            var snapshot = manager.BuildSnapshot(now);
            if (pending != null && t >= ready) { latest = pending; pending = null; }
            var player = new PlayerState(position, 0, 6, pair.Item2.IsRunning, true, 2.8f - (float)(t % 2.8), true, snapshot.Version);
            if (pending == null && snapshot.DangerAreas.Count > 0)
            {
                pending = new SafeZoneSolver().Solve(snapshot, player, new(6, 6, 0, false, 6, 6), new() { ArenaSafetyMargin = 0 }, now, latest);
                ready = t + workerDelay;
            }
            dispatch.Invoke(movement, [latest, player, snapshot, now]);
            if (inspectWaiting && t is > 162.2 and < 166.1 && refuge == null
                && latest is { UsesTemporalPlanning: true, ExecutablePath.Count: > 0 }
                && latest.ExecutablePath[^1] is { IsWaitPoint: true } wait
                && Vector3.Distance(position, wait.Position) <= .1f)
            {
                refuge = position;
                refugeAt = t;
            }
            if (pair.Item2.IsRunning) ++moves;
            position = (Vector3)advance.Invoke(pair.Item2, [position, .05f])!;
            if (inspectWaiting && refuge is { } held)
            {
                if (t < firstHit - .05) waitDrift = MathF.Max(waitDrift, Vector3.Distance(position, held));
                if (t > firstHit && t < firstHit + 4 && position.X > -308 && crossedAt == null) crossedAt = t;
            }
            Check(snapshot.ArenaBoundary!.Contains(position), $"Movement left arena at {t:F2}s.");
        }
        if (inspectWaiting)
        {
            Console.WriteLine($"  ARR waiting: reached refuge at {refugeAt:F2}s; first wave {firstHit:F3}s; drift={waitDrift:F3}y; crossed={crossedAt:F2}s");
            Check(refuge != null && firstHit - refugeAt > 1 && waitDrift < .01f && crossedAt < firstHit + 3.5,
                "The player must hold the refuge, then cross the cleared first column before the third wave.");
        }
        Console.WriteLine($"  ARR closed loop: {hit.Count} effects; {collisions} avoidable hits; {moves} moving frames; {workerDelay * 1000:F0}ms worker delay");
        Check(hit.Count == casts.Count(c => c.GetProperty("hit").ValueKind == JsonValueKind.Number) && moves > 0 && collisions == 0, "Full recording still takes avoidable hits.");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class Fight : IDisposable
    {
        public readonly MechanicSession Session;
        public readonly BossModule Module;
        public Actor Player => Session.World.Party[0]!;
        public Fight()
        {
            if (data == null && !MechanicRuntimeEnvironment.IsInitialized)
            {
                data = new(Environment.GetEnvironmentVariable("FFXIV_SQPACK") ?? throw new Exception("Set FFXIV_SQPACK."),
                    new Lumina.LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified });
                MechanicRuntimeEnvironment.Initialize(data, DispatchProxy.Create<Dalamud.Plugin.Services.IPluginLog, Orthos10Log>());
            }
            Session = new("EO10 ARR regression");
            Session.BeginFrame(Start);
            Session.Execute(new ActorState.OpCreate(100, 0, 0, 0, "Player", 0, ActorType.Player, Class.BLM, 90,
                new(-300, 0, -300, 0), .5f, new(100000, 100000, 0, 10000, 10000), true, true, 0, 0, 0));
            Session.Execute(new PartyState.OpModify(0, new(100, 100, false, "Player")));
            Spawn(1, 0x3D54, new(-300, 0, -300));
            Session.Execute(new ActorState.OpCombat(1, true));
            Session.Update();
            Module = Session.BossModules.ActiveModule ?? throw new Exception("EO10 module not active.");
        }
        private void Spawn(ulong id, uint oid, Vector3 pos) => Session.Execute(new ActorState.OpCreate(id, oid, (int)id * 2, 0,
            "Enemy", 0, ActorType.Enemy, Class.None, 90, new(pos, 0), 1.5f,
            new(100000, 100000, 0, 10000, 10000), true, false, 0, 0, 0));
        public void Cast(ulong id, uint action, Vector3 pos, float duration)
        {
            if (Session.World.Actors.Find(id) == null) Spawn(id, 0x3D55, pos);
            else Session.Execute(new ActorState.OpMove(id, new(pos, 0)));
            Session.Execute(new ActorState.OpCastInfo(id, new() { Action = new(ActionType.Spell, action), TargetID = id,
                Location = Vector3.Zero, TotalTime = duration }));
            Session.Update();
        }
        public void At(double time) { Session.BeginFrame(Start.AddSeconds(time)); Session.Update(); }
        public void Hit(ulong id, uint action) => Session.Execute(new ActorState.OpCastEvent(id, new(new(ActionType.Spell, action), id, 0, 0, default, 1, 0, default)));
        public GenericAOEs.AOEInstance[] Aoes() => Module.Components.OfType<GenericAOEs>().SelectMany(c => c.ActiveAOEs(0, Player).ToArray()).ToArray();
        public void Dispose() => Session.Dispose();
    }
}
public class Orthos10Log : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => null;
}