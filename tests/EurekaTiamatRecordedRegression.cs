using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

internal static class EurekaTiamatRecordedRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private const BindingFlags PRIVATE = BindingFlags.NonPublic | BindingFlags.Instance;
    private sealed record RecordedEvent(double T, string Kind, ulong Source, uint Action, float[]? Pos,
        float Rotation, float Seconds, float[]? Loc, uint Oid, byte State);

    internal static void Run(Action<string, Action> run)
    {
        run("Eureka30 native ARR closed loop 300ms default start", () => Simulate(new(-300, 0, -284)));
        run("Eureka30 native ARR closed loop 300ms danger corner", () => Simulate(new(-282.277f, 0, -281.3f)));
    }

    private static void Simulate(Vector3 position)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures/EurekaOrthos/eo30-030529.json")));
        var events = JsonSerializer.Deserialize<RecordedEvent[]>(document.RootElement.GetProperty("events").GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var encounter = new DungeonEncounter(1101);
        var actors = new Dictionary<ulong, DungeonActor>
        {
            [1] = new(1, 0x3D9A, new(-300, 0, -320), InCombat: true, Hp: 100),
            [100] = new(100, 0, position, PartySlot: 0, Player: true),
        };
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = .5f, UtcNowForTest = Start };
        var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
        var pair = ((object, IGreenMoveSystem))typeof(LateDodgeRegression).GetMethod("CreateMovement",
            BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
        using var movement = (SafeZoneRebornMovementSystem)pair.Item1;
        var setting = new SafeZoneSetting { EnableSafeZone = true, EnableAutoMovement = true };
        movement.GetType().GetField("_setting", PRIVATE)!.SetValue(movement, setting);
        movement.GetType().GetField("_manager", PRIVATE)!.SetValue(movement, safe.Manager);
        var dispatch = movement.GetType().GetMethod("Dispatch", PRIVATE)!;
        var optionsMethod = movement.GetType().GetMethod("CaptureSolveOptions", PRIVATE)!;
        var latestField = movement.GetType().GetField("_latestSolution", PRIVATE)!;
        var usable = movement.GetType().GetMethod("IsSolutionUsableForDispatch",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var advance = pair.Item2.GetType().GetMethod("Advance")!;
        var rawEnds = new Dictionary<ulong, double>();
        var footprints = new Dictionary<(ulong, uint), (Vector3 Position, float Rotation)>();
        SafeZoneSolution? latest = null, pending = null;
        var ready = 0d;
        var cursor = 0;
        var checks = 0;
        var collisions = 0;
        var headChecks = 0;
        var headHits = 0;
        var chaseRemaining = 0;
        var chaseOrigin = Vector3.Zero;
        var now = Start;
        void Sync()
        {
            actors[100] = actors[100] with { Position = position };
            encounter.Synchronize(actors.Values, position, now, playerId: 100);
        }
        static Vector3 Point(float[]? value) => value is { Length: 3 } ? new(value[0], 0, value[2]) : Vector3.Zero;
        for (var tick = 0; tick * .05 < events[^1].T + 2; ++tick)
        {
            var t = tick * .05;
            now = Start.AddSeconds(t);
            safe.UtcNowForTest = now;
            // The fixture records cast starts, not raw ends; emulate raw disappearance at the displayed duration.
            foreach (var source in rawEnds.Keys.Where(s => rawEnds[s] <= t).ToArray())
            {
                actors[source] = actors[source] with { Cast = null };
                rawEnds.Remove(source);
            }
            Sync();
            while (cursor < events.Length && events[cursor].T <= t)
            {
                var e = events[cursor++];
                var p = Point(e.Pos);
                switch (e.Kind)
                {
                    case "spawn":
                        actors[e.Source] = new(e.Source, e.Oid, p, Rotation: e.Rotation, InCombat: e.Source == 1, Hp: 100);
                        break;
                    case "move":
                        if (actors.TryGetValue(e.Source, out var moved))
                            actors[e.Source] = moved with { Position = p, Rotation = e.Rotation };
                        break;
                    case "state":
                        actors[e.Source] = actors[e.Source] with { AnimationState1 = e.State };
                        break;
                    case "icon":
                        // Fixture IDs are anonymous; every recorded player icon belongs to simulated player 100.
                        encounter.Icon(100, e.Action, now, 100);
                        break;
                    case "cast":
                        var location = e.Action is 31844 or 31846 ? p : Point(e.Loc);
                        if (e.Action == 31847)
                        {
                            location = position;
                            chaseOrigin = position;
                            chaseRemaining = 5;
                        }
                        if (!actors.TryGetValue(e.Source, out var caster))
                            caster = new(e.Source, 0x233C, p);
                        actors[e.Source] = caster with
                        {
                            Position = p, Rotation = e.Rotation,
                            Cast = new(e.Action, location, e.Rotation, Start.AddSeconds(e.T),
                                Start.AddSeconds(e.T + e.Seconds + .3), TotalTime: e.Seconds),
                        };
                        rawEnds[e.Source] = e.T + e.Seconds;
                        footprints[(e.Source, e.Action)] = (location, e.Rotation);
                        break;
                    case "effect":
                        if (e.Action is 31847 or 31848)
                        {
                            if (chaseRemaining < 5)
                            {
                                var delta = position - chaseOrigin;
                                chaseOrigin = delta.Length() > 6 ? chaseOrigin + Vector3.Normalize(delta) * 6 : position;
                            }
                            p = chaseOrigin;
                            --chaseRemaining;
                        }
                        var hit = false;
                        if (e.Action is 31844 or 31846)
                        {
                            // Several effect packets contain zero coordinates: use the independently locked cast footprint.
                            var footprint = footprints[(e.Source, e.Action)];
                            var delta = position - footprint.Position;
                            var forward = new Vector3(MathF.Sin(footprint.Rotation), 0, MathF.Cos(footprint.Rotation));
                            var along = Vector3.Dot(delta, forward);
                            var across = delta.X * forward.Z - delta.Z * forward.X;
                            hit = along >= 0 && along <= 40 && MathF.Abs(across) <= 8;
                        }
                        else hit = Vector3.DistanceSquared(position, p) <= 36;
                        ++checks;
                        if (hit)
                        {
                            ++collisions;
                            Console.WriteLine($"  native collision action={e.Action} t={e.T:F3} position={position} origin={p} target={latest?.TargetPosition}");
                        }
                        encounter.Effect(e.Source, e.Action, 1, now, location: p);
                        break;
                }
                Sync();
            }
            foreach (var head in actors.Values.Where(a => a.Oid == 0x3D9B && a.AnimationState1 == 1))
            {
                ++headChecks;
                if (Vector3.DistanceSquared(position, head.Position) < 2.15f * 2.15f)
                {
                    if (headHits++ < 3)
                        Console.WriteLine($"  native head collision t={t:F2} position={position} head={head.Position} target={latest?.TargetPosition}");
                }
            }
            output.Apply(encounter, now, false);
            var snapshot = safe.Manager.BuildSnapshot(now);
            if (pending != null && t >= ready)
            {
                if ((bool)usable.Invoke(null, [pending, snapshot])!) latest = pending;
                pending = null;
            }
            var player = new PlayerState(position, 0, 6, pair.Item2.IsRunning, false, 0, true, snapshot.Version);
            if (pending == null && (snapshot.DangerAreas.Count > 0 || snapshot.GoalAreas.Count > 0))
            {
                var options = (SolveOptions)optionsMethod.Invoke(movement, null)!;
                pending = new SafeZoneSolver().Solve(snapshot, player, new(6, 6, 0, false, 6, 6), options, now, latest);
                ready = t + .3;
            }
            latestField.SetValue(movement, latest);
            dispatch.Invoke(movement, [(bool)usable.Invoke(null, [latest, snapshot])! ? latest : null, player, snapshot, now]);
            latest = movement.LatestSolution;
            position = (Vector3)advance.Invoke(pair.Item2, [position, .05f])!;
            if (!snapshot.ArenaBoundary!.Contains(position))
                throw new InvalidOperationException($"Native EO30 left arena at {t:F2}s.");
        }
        Console.WriteLine($"  native ARR: {checks} effects, {collisions} collisions; heads: {headChecks} samples, {headHits} overlaps; worker=300ms");
        output.Clear();
        if (checks != 106 || checks != events.Count(e => e.Kind == "effect") || collisions != 0 || headHits != 0)
            throw new InvalidOperationException("Native EO30 recorded navigation still takes avoidable hits.");
    }
}
