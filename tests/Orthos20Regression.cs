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

internal static class Orthos20Regression
{
    private static Lumina.GameData? data;
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
    internal sealed record RecordedEvent(double T, string Kind, ulong Source, uint Action, float[] Pos, float Rotation, float Seconds, double Hit);
    private static RecordedEvent[] Recording() => JsonSerializer.Deserialize<RecordedEvent[]>(
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/EurekaOrthos/eo20-040612.json"))).RootElement.GetProperty("events").GetRawText(),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    public static void Run(Action<string, Action> run)
    {
        run("EO20 native laser uses caster origin and cast rotation", () =>
        {
            using var f = new Fight();
            f.Cast(1, 32547, new(-300, 0, -300), 2.2f, 1.2f);
            var a = f.Aoes().Single();
            Check(a.Origin.AlmostEqual(new(-300, -300), .01f) && Math.Abs(a.Rotation.Rad - 1.2f) < .001f, "Laser origin/rotation lost.");
            Check(Math.Abs((a.Activation - Start).TotalSeconds - 2.5) < .001, "Laser timing lost.");
        });
        run("EO20 native breath corrects cue and survives missing cue", () =>
        {
            using var f = new Fight();
            f.Cue(2, new(-269, 3, -269), -2.3561945f);
            f.At(5.1);
            f.Cast(3, 32864, new(-285, 0, -285), 3.2f, -2.3561945f);
            var a = f.Aoes().Single();
            Check(a.ActorID == 3 && a.Origin.AlmostEqual(new(-285,-285), .01f) && Math.Abs((a.Activation - Start).TotalSeconds - 8.6) < .001, "Helper did not authoritatively replace prediction.");
            f.Cast(4, 32864, new(-300, 0, -322), 3.2f, 0);
            Check(f.Aoes().Length == 2, "Missing visual cue lost real breath.");
        });
        run("EO20 relay exposes both waves and handles cue before command", () =>
        {
            using var f = new Fight();
            f.Cue(2, new(-269, 3, -269), -2.3561945f);
            f.Cast(1, 32545, new(-300, 0, -300), 7.7f);
            Check(Math.Abs((f.Aoes().Single().Activation - Start).TotalSeconds - 11.4) < .01, "Relay command did not retime preceding cue.");
            f.At(4);
            f.Cue(3, new(-300, 3, -344), 0);
            Check(f.Aoes().Length == 2 && Math.Abs((f.Aoes()[1].Activation - Start).TotalSeconds - 15.4) < .01, "Relay second wave hidden/lost.");
        });
        run("EO20 repeated cue and late module activation remain bounded", () =>
        {
            using var f = new Fight();
            f.Cue(2, new(-331, 3, -331), .7853982f);
            f.Cue(2, new(-331, 3, -331), .7853982f);
            Check(f.Aoes().Length == 1, "Repeated cue duplicated prediction.");
            Check(f.Aoes()[0].Origin.AlmostEqual(new(-315.5f,-315.5f), .05f), "NW inner-ring offset lost.");
            f.At(5.1);
            f.Cast(3, 32864, new(-315.5f, 0, -315.5f), 3.2f, .7853982f);
            Check(f.Aoes().Length == 1 && Math.Abs((f.Aoes()[0].Activation-Start).TotalSeconds-8.6)<.001, "Mid-fight cast failed to correct unknown command history.");
            f.At(10);
            Check(f.Aoes().Length == 0, "Prediction/effect timeout leaked.");
        });
        run("EO20 native cancellation effect identity and expiry", () =>
        {
            using var f = new Fight();
            f.Cast(3, 32864, new(-285, 0, -285), 3.2f, -2.35f);
            f.Cast(4, 32864, new(-300, 0, -322), 3.2f, 0);
            f.At(3.2);
            f.Session.Execute(new ActorState.OpCastInfo(4, null));
            Check(f.Aoes().Length == 2, "Native cast end cleared breath early.");
            f.Hit(4, 32864);
            Check(f.Aoes().Single().ActorID == 3, "Out-of-order effect removed wrong breath.");
            f.Hit(3, 32864);
            Check(f.Aoes().Length == 0, "Effect did not clear breath.");
            f.Cast(3, 32864, new(-285, 0, -285), 3.2f);
            f.Session.Execute(new ActorState.OpCastInfo(3, null));
            Check(f.Aoes().Length == 0, "Cancelled breath retained danger.");
            f.Cast(3, 32864, new(-285, 0, -285), 3.2f);
            var c = f.Module.Components.OfType<GenericAOEs>().First();
            c.OnCastStarted(f.Session.World.Actors.Find(3)!, f.Session.World.Actors.Find(3)!.CastInfo!);
            Check(f.Aoes().Length == 1, "Duplicate cast created danger twice.");
            c.OnActorDeath(f.Session.World.Actors.Find(3)!);
            Check(f.Aoes().Length == 0, "Death retained danger.");
            f.Cast(4, 32864, new(-300, 0, -322), 3.2f);
            c.OnActorDestroyed(f.Session.World.Actors.Find(4)!);
            Check(f.Aoes().Length == 0, "Despawn retained danger.");
            f.Cast(5, 32547, new(-300, 0, -300), 2.2f);
            f.At(8);
            Check(f.Aoes().Length == 0, "Missing effect leaked danger.");
        });
        run("EO20 ARR all 62 real hit footprints survive native cast end", VerifyRecording);
        run("EO20 ARR closed loop BLM 100ms worker", () => Simulate(.1));
        run("EO20 ARR closed loop BLM 300ms worker", () => Simulate(.3));
        run("EO20 ARR center east south west starts 300ms worker", () =>
        {
            foreach (var p in new[] { new Vector3(-300,0,-300), new(-284,0,-300), new(-300,0,-316), new(-316,0,-300) }) Simulate(.3, p);
        });
    }
    private static void Apply(Fight f, RecordedEvent e)
    {
        var p = new Vector3(e.Pos[0], 0, e.Pos[2]);
        if (e.Kind == "cast") f.Cast(e.Source, e.Action, p, e.Seconds, e.Rotation);
        else if (e.Kind == "cue") f.Cue(e.Source, p, e.Rotation);
        else f.Hit(e.Source, e.Action);
    }
    private static void VerifyRecording()
    {
        var events = Recording();
        using var f = new Fight();
        var ended = new HashSet<RecordedEvent>();
        var covered = 0;
        foreach (var e in events)
        {
            f.At(e.T);
            foreach (var c in events.Where(c => c.Kind == "cast" && c.Action is 32864 or 32547 && c.T + c.Seconds <= e.T))
                if (ended.Add(c)) f.Session.Execute(new ActorState.OpCastInfo(c.Source, null));
            if (e.Kind == "effect")
            {
                var a = f.Aoes().SingleOrDefault(a => a.ActorID == e.Source);
                Check(a.Shape != null && a.Origin.AlmostEqual(new(e.Pos[0], e.Pos[2]), .05f), $"Uncovered {e.Action} effect at {e.T:F3}s.");
                Check(Math.Abs((a.Activation - Start).TotalSeconds - e.T) < .16, $"Wrong activation at {e.T:F3}s.");
                ++covered;
            }
            Apply(f, e);
        }
        Check(covered == 62 && f.Aoes().Length == 0, "Effects missing or stale danger remained.");
        Console.WriteLine($"  ARR coverage: {covered}/62 real spatial effects");
    }
    private static void Simulate(double workerDelay, Vector3? initial = null)
    {
        var events = Recording();
        var casts = events.Where(e => e.Kind == "cast" && e.Action is 32864 or 32547).ToArray();
        using var f = new Fight();
        using var manager = new SafeZoneManager(new() { EnableSafeZone = true, EnableAutoMovement = true });
        var now = Start;
        var registry = new SafeZoneRegistry(manager, () => now);
        var differ = new MechanicNavAreaDiffer();
        registry.SetArenaBoundary(new CircleGeometry(new(-300, 0, -300), 19.5f));
        var pair = ((object, IGreenMoveSystem))typeof(LateDodgeRegression).GetMethod("CreateMovement", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        using var movement = (SafeZoneRebornMovementSystem)pair.Item1;
        var advance = pair.Item2.GetType().GetMethod("Advance")!;
        var dispatch = movement.GetType().GetMethod("Dispatch", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var position = initial ?? new Vector3(-300, 0, -284);
        var ended = new HashSet<RecordedEvent>();
        var hints = new AIHints();
        SafeZoneSolution? latest = null, pending = null;
        var cursor = 0; var hits = 0; var collisions = 0; var ready = 0d;
        for (var tick = 0; tick * .05 < events[^1].T + 2; ++tick)
        {
            var t = tick * .05;
            now = Start.AddSeconds(t); f.At(t);
            foreach (var c in casts)
                if (t >= c.T + c.Seconds && ended.Add(c)) f.Session.Execute(new ActorState.OpCastInfo(c.Source, null));
            while (cursor < events.Length && events[cursor].T <= t)
            {
                var e = events[cursor++];
                if (e.Kind == "effect")
                {
                    AOEShape shape = e.Action == 32864 ? new AOEShapeCone(50, 15.Degrees()) : new AOEShapeRect(40, 2.5f);
                    // Use the raw BMR point-in-AOE predicate, independent of inflated navigation hints.
                    if (shape.Check(new(position.X, position.Z), new(e.Pos[0], e.Pos[2]), e.Rotation.Radians()))
                    {
                        ++collisions;
                        Console.WriteLine($"  collision source={e.Source} origin={string.Join(",",e.Pos)} rot={e.Rotation} {e.Action} at {e.T:F3}s pos={position} distance={shape.Distance(new(e.Pos[0],e.Pos[2]),e.Rotation.Radians()).Distance(new(position.X,position.Z)):F4} solution={latest?.Status} target={latest?.TargetPosition}");
                    }
                    ++hits;
                }
                Apply(f, e);
            }
            f.Session.Execute(new ActorState.OpMove(100, new(position, 0)));
            hints.Clear();
            foreach (var c in f.Module.Components) c.AddAIHints(0, f.Player, default, hints);
            var requests = hints.ForbiddenZones.Select((z, i) => new MechanicNavZoneRequest($"{z.Source}/{z.activation.Ticks}/{i}",
                MechanicHintsConvert.ConvertForbiddenZone(z.shapeDistance, 0, new(-300, 0, -300), 28, out _),
                (float)Math.Max(0, (z.activation - now).TotalSeconds), z.activation <= now, MechanicHintsConvert.SignatureOf(z.shapeDistance) + "|" + z.activation.Ticks)).ToArray();
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
            position = (Vector3)advance.Invoke(pair.Item2, [position, .05f])!;
            Check(snapshot.ArenaBoundary!.Contains(position), $"Left arena at {t:F2}s.");
        }
        Console.WriteLine($"  ARR closed loop: {hits} effects; {collisions} avoidable hits; {workerDelay * 1000:F0}ms worker delay");
        Check(hits == 62 && collisions == 0, "Recording still takes avoidable hits.");
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
                MechanicRuntimeEnvironment.Initialize(data, DispatchProxy.Create<Dalamud.Plugin.Services.IPluginLog, Orthos20Log>());
            }
            Session = new("EO20 ARR regression");
            Session.BeginFrame(Start);
            Session.Execute(new ActorState.OpCreate(100, 0, 0, 0, "Player", 0, ActorType.Player, Class.BLM, 90,
                new(-300, 0, -300, 0), .5f, new(100000, 100000, 0, 10000, 10000), true, true, 0, 0, 0));
            Session.Execute(new PartyState.OpModify(0, new(100, 100, false, "Player")));
            Spawn(1, 0x3E77, new(-300, 0, -300));
            Session.Execute(new ActorState.OpCombat(1, true));
            Session.Update();
            Module = Session.BossModules.ActiveModule ?? throw new Exception("EO20 module not active.");
        }
        public void Spawn(ulong id, uint oid, Vector3 pos) => Session.Execute(new ActorState.OpCreate(id, oid, (int)id * 2, 0,
            "Enemy", 0, ActorType.Enemy, Class.None, 90, new(pos, 0), 1.5f,
            new(100000, 100000, 0, 10000, 10000), true, false, 0, 0, 0));
        public void Cast(ulong id, uint action, Vector3 pos, float duration, float rotation = 0)
        {
            if (Session.World.Actors.Find(id) == null) Spawn(id, 0x233C, pos);
            else Session.Execute(new ActorState.OpMove(id, new(pos, 0)));
            Session.Execute(new ActorState.OpCastInfo(id, new() { Action = new(ActionType.Spell, action), TargetID = id,
                Location = Vector3.Zero, Rotation = rotation.Radians(), TotalTime = duration }));
            Session.Update();
        }
        public void Cue(ulong id, Vector3 pos, float rotation)
        {
            if (Session.World.Actors.Find(id) == null) Spawn(id, 0x3E78, pos);
            Session.Execute(new ActorState.OpMove(id, new(pos, rotation)));
            Hit(id, 32552);
        }
        public void At(double time) { Session.BeginFrame(Start.AddSeconds(time)); Session.Update(); }
        public void Hit(ulong id, uint action) => Session.Execute(new ActorState.OpCastEvent(id, new(new(ActionType.Spell, action), id, 0, 0, default, 1, 0, default)));
        public GenericAOEs.AOEInstance[] Aoes() => Module.Components.OfType<GenericAOEs>().SelectMany(c => c.ActiveAOEs(0, Player).ToArray()).ToArray();
        public void Dispose() => Session.Dispose();
    }
}
public class Orthos20Log : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => null;
}
