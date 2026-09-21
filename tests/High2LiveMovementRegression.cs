using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Scripting;
using PromeRotation.GreenMoveSystem;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class High2LiveMovementRegression
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run(Action<string, Action> run)
    {
        foreach (var delay in new[] { 0.05, 0.10, 0.15, 0.20 })
            run($"B37 11:29 ARR escapes three large circles with {delay * 1000:F0}ms replan latency", () =>
            {
                using var f = new Fight();
                var (movement, move) = CreateMovement();
                var latest = movement.GetType().GetField("_latestSolution", Fields)!;
                var advance = move.GetType().GetMethod("Advance")!;
                SafeZoneSolution? pending = null;
                double ready = 0, nextSolve = 0;
                var trace = new List<string>();
                string previous = "";
                var started = false;
                var stoppedInDanger = false;
                for (var tick = 0; tick < 520; ++tick)
                {
                    var t = tick * 0.01;
                    f.World.CurrentTime = f.Epoch.AddSeconds(t);
                    if (tick % 10 == 0) f.Render();
                    var snapshot = f.Snapshot;
                    if (pending != null && t + 1e-6 >= ready)
                    {
                        latest.SetValue(movement, pending);
                        pending = null;
                    }
                    var solution = (SafeZoneSolution?)latest.GetValue(movement);
                    if (pending == null && t + 1e-6 >= nextSolve)
                    {
                        // Match the host: retain its last accepted/dispatched solution as the stability reference.
                        pending = f.Solve(solution);
                        ready = t + delay;
                        nextSolve = t + 0.1;
                    }
                    Dispatch(movement, solution, snapshot, f.Player.Position.ToVec3(), f.World.CurrentTime);
                    var state = $"{solution?.Status}/{move.IsRunning}";
                    if (state != previous) trace.Add($"t={t:F2} {f.Player.Position} {state}");
                    previous = state;
                    started |= move.IsRunning;
                    stoppedInDanger |= started && !move.IsRunning && f.InBlast();
                    f.Player.Position = WPos.FromVec3((Vector3)advance.Invoke(move, [f.Player.Position.ToVec3(), 0.01f])!);
                    Check(snapshot.ArenaBoundary!.Contains(f.Player.Position.ToVec3()), "escape left the square arena");
                    if (t >= 5.19) Check(!f.InBlast(), "still in recorded damage at impact: " + string.Join("; ", trace));
                }
                Check(!stoppedInDanger, "rejected background candidate stopped a valid escape: " + string.Join("; ", trace));
            });

        foreach (var publication in new[] { "null", "failed", "stale-success" })
            run($"movement rejects {publication} without cancelling its still-valid route", () =>
            {
                using var f = new Fight();
                var (movement, move) = CreateMovement();
                var route = f.Solve();
                var staleCandidate = f.Solve();
                Dispatch(movement, route, f.Snapshot, f.Player.Position.ToVec3(), f.Epoch);
                Check(move.IsRunning, "missing initial escape");
                var position = (Vector3)move.GetType().GetMethod("Advance")!.Invoke(move, [f.Player.Position.ToVec3(), 0.8f])!;
                f.Player.Position = WPos.FromVec3(position);
                var now = f.Epoch.AddSeconds(0.8);
                // The background task sampled the player before the currently executing route advanced.
                SafeZoneSolution? candidate = publication switch
                {
                    "null" => null,
                    "failed" => SafeZoneSolution.Failed(SolutionStatus.NoTemporalPath, f.Snapshot.Version, f.Epoch, f.Epoch),
                    _ => staleCandidate
                };
                Dispatch(movement, candidate, f.Snapshot, position, now);
                Check(move.IsRunning, "an unusable replacement stopped the safe current route");

                // Retention must still validate the entire remaining route against the latest geometry.
                f.SafeZone.Manager.AddDangerArea(new CircleTemplate(100), new StaticTransform(position, 0),
                    [new DangerWindow(now.AddSeconds(0.2), null)]);
                Dispatch(movement, candidate, f.Snapshot, position, now);
                Check(!move.IsRunning, "retained a route after a new hazard invalidated it");
            });
    }

    private static (object, IGreenMoveSystem) CreateMovement() =>
        ((object, IGreenMoveSystem))typeof(LateDodgeRegression).GetMethod("CreateMovement", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
    private static void Dispatch(object movement, SafeZoneSolution? solution, SafeZoneSnapshot snapshot, Vector3 position, DateTime now) =>
        movement.GetType().GetMethod("Dispatch", Fields)!.Invoke(movement,
            [solution, new PlayerState(position, 0, 6, false, false, 0, true, snapshot.Version), snapshot, now]);

    private sealed class Fight : IDisposable
    {
        public readonly DateTime Epoch = DateTime.UtcNow.AddMinutes(1);
        public readonly WorldState World = new();
        public readonly ScriptSafeZoneApi SafeZone = new() { DangerMarginForTest = 0.5f };
        public readonly CrucibleEngine Engine;
        public readonly CrucibleOutput Output;
        public readonly Actor Player;
        public SafeZoneSnapshot Snapshot => SafeZone.Manager.BuildSnapshot(World.CurrentTime);
        public Fight()
        {
            World.CurrentTime = Epoch;
            var boss = new Actor { InstanceID = 1, OID = 19000, NameID = 14657, Position = new(120, 0),
                HPMP = (100, 100), Type = ActorType.Enemy, IsTargetable = true, InCombat = true };
            Player = new() { InstanceID = 2, Position = new(119.9817f, 7.9805f), HPMP = (100, 100), Type = ActorType.Player, IsTargetable = true };
            World.Actors.Add(boss); World.Actors.Add(Player); World.Player = Player;
            Engine = new(World, 32, 45); Engine.SelectGroup();
            Output = new(SafeZone, new ScriptDrawApi(), SafeZone.Manager, _ => { }, () => 0.5f);
            ulong id = 100;
            foreach (var e in new[] { (49265u, 110.9789f, 8.9876f), (49265u, 128.9845f, 8.9876f),
                (49266u, 128.9845f, -9.0181f), (49265u, 110.9789f, -9.0181f) })
            {
                var actor = new Actor { InstanceID = id++, OID = 9020, Position = new(e.Item2, e.Item3), Type = ActorType.Enemy, HPMP = (100, 100) };
                World.Actors.Add(actor);
                Engine.Cast(actor, new() { Action = new(e.Item1), LocXZ = actor.Position, FinishAt = Epoch.AddSeconds(5.2) });
            }
            Render();
        }
        public bool InBlast() => Snapshot.DangerAreas.Any(d => d.Geometry.Contains(Player.Position.ToVec3()));
        public void Render() { Engine.Update(); Output.Apply(Engine, Engine.Frame(), 0, true, true, true); }
        public SafeZoneSolution Solve(SafeZoneSolution? previous = null) => new SafeZoneSolver().Solve(Snapshot,
            new(Player.Position.ToVec3(), 0, 6, false, false, 0, true, Snapshot.Version), new(6, 6, 0, false, 6, 6), new(), World.CurrentTime, previous);
        public void Dispose() { Output.Clear(); SafeZone.Dispose(); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
