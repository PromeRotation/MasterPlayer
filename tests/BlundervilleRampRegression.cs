using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

internal static class BlundervilleRampRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "Blunderville", "ramp-recovery-012045.json")));
        var surface = new Surface(fixture.RootElement.GetProperty("surface"));
        foreach (var sample in fixture.RootElement.GetProperty("positions").EnumerateArray())
        {
            var start = Vec(sample.GetProperty("position"));
            var time = sample.GetProperty("t").GetDouble();
            foreach (var speed in new[] { 6f, 12f })
                run($"Blunderville ramp ARR {time:F3}s at {speed}m/s: actual navigation reaches the high platform without jumping", () =>
                    Simulate(start, speed, surface));
        }

        run("Blunderville ramp recorded wall rejects the old diagonal approach", () =>
        {
            var from = new Vector3(10.1472f, 5.9968f, -270.985f);
            Check(!CanWalk(from, new(11.5f, 6.03f, -270), surface, out _),
                "The independent model surface no longer reproduces the recorded side-wall collision.");
        });

        run("Blunderville ramp airborne height cannot finish recovery or advance its turn", () =>
        {
            var state = Start();
            var planner = new BlundervilleRacePlanner();
            planner.Update(state, Epoch, new(13.35f, 6, -268.48f), true, false, true, null);
            var cursor = planner.Cursor;
            var airborne = planner.Update(state, Epoch.AddSeconds(.1), new(7, 10.03f, -268), false, true, true, null);
            Check(planner.Recovering && planner.Cursor == cursor && !airborne.Jump,
                "Airborne height advanced the recovery route or generated another jump.");
            planner.Update(state, Epoch.AddSeconds(.2), new(10, 9.9f, -268), true, false, true, null);
            Check(planner.Recovering, "Height alone cleared recovery while still beside the platform.");
            var landed = planner.Update(state, Epoch.AddSeconds(.3), new(7, 10.03f, -268), true, false, false, null);
            Check(!planner.Recovering && planner.RecoveryCorridor == null && landed.Target is { Z: -271 },
                "Confirmed landing failed to rejoin the high route.");
            planner.Update(state, Epoch.AddSeconds(.4), new(-11, 6, -271), true, false, true, null);
            Check(planner.Recovering && planner.RecoveryCorridor != null, "A second fall did not acquire a fresh recovery.");
            planner.Reset();
            Check(planner.RecoveryCorridor == null, "Reset retained the lower-floor boundary.");
        });

        run("Blunderville ramp does not pass a corner from four meters away or jump under native arrow control", () =>
        {
            var state = Start();
            var planner = new BlundervilleRacePlanner();
            planner.Update(state, Epoch, new(13.4f, 6, -274), true, false, false, null);
            var corner = planner.Update(state, Epoch.AddSeconds(.1), new(11, 6, -267.9f), true, false, true, null);
            Check(corner.Target is { X: 13.4f, Z: -268 } && !corner.Jump,
                "The pass-through rule cut the ramp corner.");
            state.SynchronizeStatuses([new(3694, 360, DateTime.MaxValue)], Epoch);
            var pos = new Vector3(13.4f, 6, -269);
            var arrow = planner.Update(state, Epoch.AddSeconds(.2), pos, true, false, true, 0,
                nativeMisdirection: true, executionTarget: pos + Vector3.UnitZ);
            Check(!arrow.Hold && !arrow.Jump, "Native arrow control bypassed the recovery no-jump gate.");
        });
    }

    private static void Simulate(Vector3 position, float speed, Surface surface)
    {
        var state = Start();
        var planner = new BlundervilleRacePlanner();
        var now = Epoch;
        using var runtime = new SafeZoneRuntime(new IdleMove(), () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi();
        api.ShareManagerForTest(runtime.Manager);
        var navigation = new BlundervilleNavigation(runtime, api, new());
        var encounter = new BlundervilleEncounter(message => throw new InvalidOperationException(message));
        var solver = new SafeZoneSolver();
        var capability = new MovementCapability(speed, speed, 0, false, speed, speed);
        Vector3? oldTarget = null;
        var dispatchAt = now;
        for (var step = 0; step < 200; ++step)
        {
            var decision = planner.Update(state, now, position, true, false, true, null);
            if (!planner.Recovering)
            {
                Check(position.Y >= 9.8f && MathF.Abs(position.X) <= 7.4f && position.Z >= -270,
                    $"Recovery ended before reaching the platform at {position}.");
                navigation.Apply(state, decision, encounter, now, position, true, false, false);
                Check(decision.Target is { Z: -271 }, "The completed ascent skipped the bridge approach.");
                navigation.Clear();
                Check(runtime.Manager.BuildSnapshot(now).ArenaBoundary == null && !runtime.Setting.RuntimeMovementRequested,
                    "Clearing the race retained its boundary or movement lease.");
                return;
            }
            Check(!decision.Hold && !decision.Jump && decision.Target != null, "Recovery stopped or requested a jump.");
            if (oldTarget != decision.Target) { oldTarget = decision.Target; dispatchAt = now.AddMilliseconds(80); }
            api.UtcNowForTest = now;
            navigation.Apply(state, decision, encounter, now, position, true, false, false, planner.RecoveryCorridor);
            Check(runtime.Setting.RuntimeArenaSafetyMargin == .5f, "Recovery weakened the ordinary boundary safety margin.");
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var solution = solver.Solve(snapshot, new(position, 0, speed, false, false, 0, true, snapshot.Version),
                capability, new SolveOptions { ArenaSafetyMargin = .5f }, now);
            Check(solution.IsSuccess && solution.ExecutablePath.Count != 0, $"No recovery route at {position}: {solution.DescribeOutcome()}.");
            // 100ms script/solve cadence, 80ms dispatch delay on a changed goal;
            // actual motion follows the solver polyline and clamps at its endpoint.
            var budget = speed * (float)Math.Clamp((now.AddSeconds(.1) - dispatchAt).TotalSeconds, 0, .1);
            foreach (var point in solution.ExecutablePath)
            {
                var delta = point.Position - position; delta.Y = 0;
                var length = delta.Length();
                if (length < .0001f) continue;
                var next = position + delta * MathF.Min(1, budget / length);
                Check(CanWalk(position, next, surface, out var floor),
                    $"Recovery cut a wall or left the measured floor: {position} -> {next}, goal {decision.Target}.");
                position = next with { Y = floor };
                budget -= length;
                if (budget <= 0) break;
            }
            now = now.AddSeconds(.1);
        }
        throw new InvalidOperationException($"Recovery made no progress at {position}, target {oldTarget}.");
    }

    private static bool CanWalk(Vector3 from, Vector3 to, Surface surface, out float height)
    {
        height = from.Y;
        var distance = Vector2.Distance(new(from.X, from.Z), new(to.X, to.Z));
        var count = Math.Max(1, (int)MathF.Ceiling(distance / .04f));
        for (var i = 1; i <= count; ++i)
        {
            var point = Vector3.Lerp(from, to, (float)i / count);
            var next = surface.Height(point);
            if (!float.IsFinite(next) || next - height > .23f || height - next > .35f) return false;
            height = next;
        }
        return true;
    }

    private sealed class Surface(JsonElement data)
    {
        private readonly float[][] _heights = data.GetProperty("heights").EnumerateArray()
            .Select(r => r.EnumerateArray().Select(h => h.ValueKind == JsonValueKind.Null ? float.NaN : h.GetSingle()).ToArray()).ToArray();
        private readonly float _minX = data.GetProperty("minX").GetSingle();
        private readonly float _minZ = data.GetProperty("minZ").GetSingle();
        private readonly float _spacing = data.GetProperty("spacing").GetSingle();
        public float Height(Vector3 p)
        {
            var x = (p.X - _minX) / _spacing;
            var z = (int)MathF.Round((p.Z - _minZ) / _spacing);
            var i = (int)MathF.Floor(x);
            if (z < 0 || z >= _heights.Length || i < 0 || i + 1 >= _heights[z].Length) return float.NaN;
            return float.Lerp(_heights[z][i], _heights[z][i + 1], x - i);
        }
    }

    private static BlundervilleRaceState Start()
    {
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 0x4E2E, 0, 0, 0, Epoch);
        return state;
    }
    private static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class IdleMove : IGreenMoveSystem
    {
        public bool IsRunning => false;
        public GreenMoveStatus GetStatus() => new(false, null, null, 0, 0, 0);
        public void Stop() { }
        public void MoveTo(GreenMovePoint p) => throw new NotSupportedException();
        public void MoveTo(IReadOnlyList<GreenMovePoint> p) => throw new NotSupportedException();
        public void Append(GreenMovePoint p) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> p) => throw new NotSupportedException();
        public void ClearQueue() => throw new NotSupportedException();
        public void SkipCurrent() => throw new NotSupportedException();
    }
}
