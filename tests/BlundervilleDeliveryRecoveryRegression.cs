using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class BlundervilleDeliveryRecoveryRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 13, 3, 0, 0, DateTimeKind.Utc);
    public static void Run(Action<string, Action> run)
    {
        run("Blunderville delivery recovery distinguishes delayed success from loss", () =>
        {
            foreach (var scoreFirst in new[] { false, true })
            {
                var state = Carry(37);
                if (scoreFirst)
                {
                    state.Director(BlundervilleRaceState.DirectorInstance, 3, 1, 0, 0, 0, Epoch.AddSeconds(1));
                    state.SynchronizeStatuses([new(3704, 37, DateTime.MaxValue)], Epoch.AddSeconds(1));
                }
                state.SynchronizeStatuses([], Epoch.AddSeconds(2));
                if (!scoreFirst) state.Director(BlundervilleRaceState.DirectorInstance, 3, 1, 0, 0, 0, Epoch.AddSeconds(2));
                Check(!state.FailedDelivery, "A confirmed delivery was classified as a failure.");
                state.SynchronizeStatuses([new(3704, 37, DateTime.MaxValue)], Epoch.AddSeconds(3));
                state.SynchronizeStatuses([], Epoch.AddSeconds(4));
                Check(state.FailedDelivery, "A later failed attempt was treated as a success.");
                state.Reset();
                Check(!state.FailedDelivery, "Failure survived a reset.");
            }
        });
        run("Blunderville delivery recovery preserves stun and cancels on pickup", () =>
        {
            var state = Carry(37); var planner = new BlundervilleRacePlanner();
            Vector3 p = new(-203.479f, 5.997f, 217.762f);
            state.SynchronizeStatuses([new(149, 0, Epoch.AddSeconds(3))], Epoch);
            Check(planner.Update(state, Epoch, p, true, false, false, null).Hold, "Recovery bypassed a real stun.");
            var now = Epoch.AddSeconds(3.1); state.SynchronizeStatuses([], now);
            var decision = planner.Update(state, now, p, true, false, false, null);
            Check(!decision.Hold && decision.RecoveryFloor != null && decision.Target is { Z: > 219 and < 223 }, "Expired stun did not start the side exit.");
            var returning = new BlundervilleFinalPlanner(BlundervilleFinalPlanner.ReturnNorth);
            Check(returning.Apply(decision, state, [], p, now) == decision, "Upper return overrode the descent.");
            state.SynchronizeStatuses([new(3704, 37, DateTime.MaxValue)], now.AddSeconds(1));
            decision = planner.Update(state, now.AddSeconds(1), BlundervilleRaceState.PickupCenter, true, false, false, null);
            Check(decision.RecoveryFloor == null && !decision.Hold && decision.Target is { Z: < 252 }, "Same-colour pickup retained the old failed route.");
        });
        run("Blunderville delivery recovery wind contacts remain bounded by the actual barriers", () =>
        {
            foreach (var (p, wall) in new[] { (new Vector3(-203.204f, 5.997f, 230.365f), 230.6f), (new Vector3(-203.082f, 5.997f, 224.659f), 224.85f) })
            {
                var floor = BlundervilleRoutes.NavigationBoundary(BlundervilleCourse.Delivery, p);
                Check(floor.Contains(p), "A recorded standing position is excluded.");
                Check(!floor.Contains(p with { Z = wall }), "The correction opened the barrier itself.");
                Check(BlundervilleDeliverySearch.FloorLink(floor, p, p - Vector3.UnitZ * .7f, true), "Wall contact cannot leave into clear floor.");
            }
        });
        foreach (var (colour, p) in new[]
        {
            ((ushort)37, new Vector3(-203.479f, 5.997f, 217.762f)),
            ((ushort)37, new Vector3(-196.22f, 6, 216.45f)),
            ((ushort)37, new Vector3(-208.12f, 3.01f, 226.31f)),
            ((ushort)35, new Vector3(-229.24f, 6, 269.15f)),
            ((ushort)36, new Vector3(-164.29f, 6, 271.23f)),
        })
            run($"Blunderville delivery recovery physical lower return colour {colour} x {p.X}", () => Simulate(colour, p));
    }

    internal static void Simulate(ushort colour, Vector3 start, bool completed = false)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var replay = new BlundervilleDeliveryScheduleRegression.Recording("2026.10.14 yellow-feedback");
        // w1ek polygons plus the observed south ramp seam at z=259.5..262.5,
        // already restored in the course boundary (19:30 manual ARR).
        using var terrain = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "delivery-surfaces.json")));
        var polygons = terrain.RootElement.EnumerateArray().Select(a => a.EnumerateArray().Select(v => new Vector3(v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle())).ToArray()).ToArray();
        var now = Epoch.AddSeconds(48.469); var state = Carry(colour);
        if (completed) state.Director(BlundervilleRaceState.DirectorInstance, 3, 1, 0, 0, 0, now);
        state.SynchronizeStatuses(completed ? [new(1548, 30, now.AddSeconds(30))] : [], now);
        var speed = completed ? 7.8f : 6f;
        var planner = new BlundervilleRacePlanner(); var move = new BlundervilleDeliveryScheduleRegression.Moving();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var p = start;
        BlundervilleRaceDecision decision = new(null, true, false, null, "");
        var grounded = true; var descended = p.Y < 4.2f; var nextScan = 0.0; var nextSolve = 0.0;
        var withoutFloor = 0.0;
        SafeZoneSolution? solution = null;
        const double dt = 1.0 / 30;
        for (var t = 0.0; t < 30; t += dt)
        {
            now = Epoch.AddSeconds(48.469 + t); move.Now = now; move.Speed = speed;
            if (t >= nextScan)
            {
                nextScan = t + .1; replay.Advance(48.469 + t); api.UtcNowForTest = now;
                decision = planner.Update(state, now, p, grounded, false, false, null);
                nav.Apply(state, decision, replay.Encounter, now, p, true, false, false);
                if (Environment.GetEnvironmentVariable("DELIVERY_DEBUG") == "1") Console.WriteLine($"REC {t:F2} {p} {decision.Hint} -> {decision.Target}");
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var player = new PlayerState(p, 0, speed, move.IsRunning, false, 0, grounded, snapshot.Version);
            var capability = new MovementCapability(speed, speed, 0, false, speed, speed);
            if (!runtime.Setting.EffectiveEnableAutoMovement) move.Stop();
            else if (decision.DirectTraversal) ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(player, capability, now);
            else
            {
                if (t >= nextSolve)
                {
                    nextSolve = t + .3;
                    var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", flags)!.Invoke(runtime.MovementStatus, null)!;
                    solution = new SafeZoneSolver().Solve(snapshot, player, capability, options, now);
                }
                runtime.MovementStatus.GetType().GetMethod("Dispatch", flags)!.Invoke(runtime.MovementStatus, [solution, player, snapshot, now]);
            }
            var next = move.Advance(p, (float)dt);
            // Independent local navmesh surface samples: never snap a player
            // downstairs onto an overlapping upper bridge. Falling takes time.
            var floor = Height(polygons, next, MathF.Max(3.65f, p.Y + .4f));
            // Navmesh omits its clearance strip along ledges. Cross it only
            // airborne, and require a lower sampled landing within one second.
            withoutFloor = floor == null ? withoutFloor + dt : 0;
            Check(withoutFloor < 1 && (floor != null || decision.DirectTraversal), $"No recorded landing below {next} at {t:F2}");
            next.Y = MathF.Max(floor ?? 3.25f, p.Y - .3f);
            grounded = floor != null && MathF.Abs(next.Y - floor.Value) < .05f;
            if (next.Y < 4.2f && grounded) descended = true;
            Check(next.Y - p.Y < .41f, "Returned through the side of a high bridge.");
            p = next;
            if (descended && p.Y >= 4.8f && Vector2.Distance(new(p.X, p.Z), new(-200, 252)) <= 2.6f)
            {
                Console.WriteLine($"RECOVERY colour {colour} from {start}: pickup after {t:F2}s, dispatch {move.Count}");
                Check(!completed || t < 11, "Successful shortcut was no faster than the recorded 14.4s return.");
                return;
            }
        }
        throw new InvalidOperationException($"Recovery stalled at {p}: {decision.Hint} -> {decision.Target}");
    }

    internal static float? Height(Vector3[][] polygons, Vector3 point, float ceiling)
    {
        float? best = null;
        foreach (var poly in polygons)
            for (var i = 1; i + 1 < poly.Length; ++i)
            {
                var a = poly[0]; var b = poly[i]; var c = poly[i + 1];
                var v0 = new Vector2(b.X - a.X, b.Z - a.Z); var v1 = new Vector2(c.X - a.X, c.Z - a.Z); var v2 = new Vector2(point.X - a.X, point.Z - a.Z);
                var det = v0.X * v1.Y - v1.X * v0.Y;
                if (MathF.Abs(det) < .00001f) continue;
                var u = (v2.X * v1.Y - v1.X * v2.Y) / det; var v = (v0.X * v2.Y - v2.X * v0.Y) / det;
                if (u < -.001 || v < -.001 || u + v > 1.001) continue;
                var y = a.Y + u * (b.Y - a.Y) + v * (c.Y - a.Y);
                if (y <= ceiling && (best == null || y > best)) best = y;
            }
        return best;
    }
    private static BlundervilleRaceState Carry(ushort colour)
    {
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20016, 0, 0, 0, Epoch);
        state.SynchronizeStatuses([new(3704, colour, DateTime.MaxValue)], Epoch);
        return state;
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
