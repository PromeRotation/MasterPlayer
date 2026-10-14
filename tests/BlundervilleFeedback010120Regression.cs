using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

// ARR 2026-10-12 01:01:20 (MP 1.11.0): late MapEffect/endpoint packets broke the
// strict clock fits. The vanishing bridges lost their collapse prediction exactly
// when the neighbouring lane appeared, and the cylinder entrance never opened.
internal static class BlundervilleFeedback010120Regression
{
    private static readonly DateTime Epoch = new(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Finish = new(-200, 13.565f, -367);
    private static readonly float[] Centers = [-210.523f, -203.5f, -196.5f, -189.5f];
    private const uint Present = 0x00020001, Absent = 0x00080004, InitialPresent = 0x00010001;

    public static void Run(Action<string, Action> run)
    {
        run("Blunderville 01:01 a late bridge packet keeps the neighbouring collapse prediction", () =>
        {
            var events = Events("2026.10.12 01.01.20");
            // Relative times of the two recorded appearances that should have
            // offered a lane switch (absolute 221.017s and 236.074s).
            foreach (var (time, lanes) in new[] { (111.564, new byte[] { 15, 17 }), (126.621, new byte[] { 14, 16 }) })
            {
                var tracker = Feed(events, time + .01);
                foreach (var lane in lanes)
                    Check(tracker.BridgeNextDisappearAt(lane, Epoch.AddSeconds(time + .01)) is { } deadline
                        && (deadline - Epoch).TotalSeconds - time is > 3.8 and < 4.7,
                        $"Lane {lane} lost its collapse prediction after a delayed packet at {time:F2}s.");
            }
        });
        foreach (var recording in new[] { "2026.10.12 01.01.20", "2026.10.12 00.17.11", "2026.10.09 04.45.52" })
            run($"Blunderville recorded bridge collapses never precede their prediction {recording}", () =>
            {
                var events = Events(recording); var tracker = new BlundervilleBridgeTracker();
                var predicted = new Dictionary<byte, DateTime>(); var checkedCount = 0; var predictable = 0; var appearances = 0;
                foreach (var (time, index, state) in events)
                {
                    var now = Epoch.AddSeconds(time);
                    if (state == Absent && predicted.Remove(index, out var deadline))
                    {
                        // Late or conservative predictions are safe; a collapse
                        // before the predicted deadline would drop the player.
                        Check(deadline <= now.AddSeconds(.05), $"Lane {index} collapsed {(deadline - now).TotalSeconds:F2}s before its prediction at {time:F2}s.");
                        Check((now - deadline).TotalSeconds < 1, $"Lane {index} prediction was {(now - deadline).TotalSeconds:F2}s too early at {time:F2}s.");
                        ++checkedCount;
                    }
                    tracker.Map(index, state, now);
                    if (state == Present && time > 0)
                    {
                        ++appearances;
                        if (tracker.BridgeNextDisappearAt(index, now) is { } next) { predicted[index] = next; ++predictable; }
                    }
                }
                Console.WriteLine($"BRIDGE {recording}: predicted {predictable}/{appearances} appearances, verified {checkedCount} collapses");
                // The first two cycles of each lane are always learning time.
                Check(predictable * 20 >= appearances * 17, "Bridge timing remained unavailable for too many appearances.");
            });
        foreach (var recording in new[] { "2026.10.12 01.01.20", "2026.10.12 00.17.11" })
            run($"Blunderville recorded bridge crossing at every arrival phase {recording}", () =>
            {
                var events = Events(recording);
                var last = events[^1].Time;
                // 00:17 recorded the live switch turning within ~0.1s of the
                // neighbour's appearance; 0.08s background dispatch matches it.
                const double latency = .08;
                var starts = new List<double>();
                for (var start = 30.0; start + 20 < last; start += .7) starts.Add(start);
                // Independent phases: run them on every core, report in phase order.
                var outcomes = starts.AsParallel().AsOrdered().WithDegreeOfParallelism(Environment.ProcessorCount).Select(start =>
                {
                    try { return (Time: (double?)SimulateCrossing(events, start, latency), Unavoidable: false, Failure: (string?)null); }
                    catch (UnavoidableCollapse) { return (null, true, null); }
                    catch (InvalidOperationException e) { return (null, false, e.Message); }
                }).ToArray();
                var results = outcomes.Where(o => o.Time != null).Select(o => o.Time!.Value).ToList();
                var failures = outcomes.Where(o => o.Failure != null).Select(o => o.Failure!).ToList();
                var unavoidable = outcomes.Count(o => o.Unavoidable);
                foreach (var failure in failures.Take(8)) Console.WriteLine($"BRIDGE fail {failure}");
                Console.WriteLine($"BRIDGE crossing {recording}: {results.Count} passed, {unavoidable} without any visible neighbour before the collapse");
                Check(failures.Count == 0, $"{failures.Count} of {failures.Count + results.Count + unavoidable} recorded arrival phases failed.");
                Console.WriteLine($"BRIDGE crossing {recording}: {results.Count} phases, slowest {results.Max():F2}s, mean {results.Average():F2}s");
            });
    }

    private static double SimulateCrossing(IReadOnlyList<(double Time, byte Index, uint State)> events, double start, double latency)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var now = Epoch.AddSeconds(start);
        var move = new JumpMove();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new());
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20013, 0, 0, 0, Epoch);
        var tracker = new BlundervilleBridgeTracker(); var planner = new BlundervilleBridgePlanner(); var gate = new BlundervilleJumpExecution();
        var encounter = new BlundervilleEncounter(_ => { });
        var actual = new Dictionary<byte, uint>(); var next = 0;
        void Advance(double time)
        {
            for (; next < events.Count && events[next].Time <= time; ++next)
            {
                var e = events[next];
                tracker.Map(e.Index, e.State, Epoch.AddSeconds(e.Time)); actual[e.Index] = e.State;
            }
        }
        Advance(start);
        var p = new Vector3(-200, 13.565f, -320.5f);
        SafeZoneSolution? pending = null, accepted = null; var ready = 0.0; var nextSolve = 0.0;
        double? launch = null;
        for (var tick = 0; tick < 2500; ++tick)
        {
            var t = tick * .01; now = Epoch.AddSeconds(start + t); move.Now = now;
            Advance(start + t);
            var flying = launch is { } at && t - at < .7;
            if (tick % 10 == 0)
            {
                var plan = planner.Plan(tracker, now, p, Finish, !flying, flying)
                    ?? new BlundervilleBridgeDecision(Finish, false, false, null, "finish");
                if (gate.Prepare(plan.CrossingCorridor as BlundervilleJumpCorridor, p) is { } execution)
                    plan = plan with { Target = execution.Target, Hold = execution.Hold, Jump = false, CrossingCorridor = execution.Corridor };
                api.UtcNowForTest = now;
                nav.Apply(state, new(plan.Target, plan.Hold, plan.Jump, null, plan.Hint), encounter, now, p, true, false, false, plan.CrossingCorridor);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            if (pending != null && t >= ready) { accepted = pending; pending = null; }
            if (pending == null && t >= nextSolve)
            {
                var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", flags)!.Invoke(runtime.MovementStatus, null)!;
                pending = new SafeZoneSolver().Solve(snapshot, new(p, 0, 6, false, false, 0, true, snapshot.Version), new(6, 6, 0, false, 6, 6), options, now);
                ready = t + latency; nextSolve = ready + .1;
            }
            if (gate.Sample(now, p, !flying, flying, runtime.MovementStatus.HasControl, move.GetStatus()))
            {
                Check(nav.CommitJump(gate.Landing!.Value, p, gate.MeasuredSpeed, now), "Landing route could not be committed.");
                gate.ConfirmLaunch(now); launch = t; flying = true;
            }
            if (!gate.InFlight) nav.ReleaseJump();
            if (gate.NeedsRunupRetry) nav.StopForRunup();
            runtime.MovementStatus.GetType().GetMethod("Dispatch", flags)!.Invoke(runtime.MovementStatus,
                [accepted, new PlayerState(p, 0, 6, false, false, 0, true, snapshot.Version), snapshot, now]);
            p = move.Advance(p);
            flying = launch is { } landingAt && t + .01 - landingAt < .7;
            if (!flying && p.Z < -322.928f && p.Z > -353.072f)
            {
                var lane = Enumerable.Range(0, 4).MinBy(i => MathF.Abs(p.X - Centers[i]));
                var supported = MathF.Abs(p.X - Centers[lane]) < 2.53f && actual.GetValueOrDefault((byte)(14 + lane)) is Present or InitialPresent;
                // 01:01 at 40.81–43.94s the neighbour's appearance packet was
                // 3.12s late and arrived together with this lane's collapse; at
                // 82.59–83.16s a 2.59s gap left a 0.57s overlap instead of ~1.5s.
                // Without a neighbour visible a second before the collapse no
                // scan, dispatch and lateral run-up can complete.
                if (!supported && MathF.Abs(p.X - Centers[lane]) < 2.53f
                    && !new[] { lane - 1, lane + 1 }.Any(n => n is >= 0 and <= 3 && VisibleWithin(events, (byte)(14 + n), start + t - 2, start + t - 1)))
                    throw new UnavoidableCollapse();
                Check(supported, $"Fell from lane {14 + lane} at {p} t={start + t:F2}s (start {start:F2}s).");
            }
            if (!flying && p.Z <= -353.072f && p.Z > -354.9f)
                Check(false, $"Landed short of the finish platform at {p} t={start + t:F2}s (start {start:F2}s).");
            if (p.Z < -355) return t;
        }
        throw new InvalidOperationException($"Bridge crossing from {start:F2}s never reached the finish platform; stopped at {p}.");
    }

    private sealed class UnavoidableCollapse : Exception;

    private static bool VisibleWithin(IReadOnlyList<(double Time, byte Index, uint State)> events, byte index, double from, double until)
    {
        var state = events.LastOrDefault(e => e.Index == index && e.Time <= from).State;
        if (state is Present or InitialPresent) return true;
        return events.Any(e => e.Index == index && e.Time > from && e.Time <= until && e.State is Present or InitialPresent);
    }

    private static BlundervilleBridgeTracker Feed(IReadOnlyList<(double Time, byte Index, uint State)> events, double until)
    {
        var tracker = new BlundervilleBridgeTracker();
        foreach (var (time, index, state) in events.Where(e => e.Time <= until)) tracker.Map(index, state, Epoch.AddSeconds(time));
        return tracker;
    }

    private static IReadOnlyList<(double Time, byte Index, uint State)> Events(string recording)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "bridges-recorded.json")));
        return document.RootElement.GetProperty("recordings").GetProperty(recording).GetProperty("events").EnumerateArray()
            .Select(e => (e[0].GetDouble(), (byte)e[1].GetInt32(), e[2].GetUInt32())).ToArray();
    }

    private sealed class JumpMove : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _points = []; private int _index; private DateTime _ready;
        public DateTime Now;
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null, _points.LastOrDefault(), _index, _points.Count, Math.Max(0, _points.Count - _index - 1));
        public void MoveTo(GreenMovePoint p) => MoveTo([p]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> points) { var running = IsRunning; _points = points; _index = 0; if (!running) _ready = Now.AddSeconds(.08); }
        public void Stop() { _points = []; _index = 0; }
        public void Append(GreenMovePoint p) => Append([p]);
        public void Append(IReadOnlyList<GreenMovePoint> p) => _points = _points.Concat(p).ToArray();
        public void ClearQueue() => _points = _points.Take(_index + 1).ToArray();
        public void SkipCurrent() => ++_index;
        public Vector3 Advance(Vector3 p)
        {
            if (!IsRunning || Now < _ready) return p;
            var delta = _points[_index].Position - p; delta.Y = 0; var length = delta.Length();
            if (length <= _points[_index].Tolerance) { ++_index; return p; }
            return p + delta / length * Math.Min(length, .06f);
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
