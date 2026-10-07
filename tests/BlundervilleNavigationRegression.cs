using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

internal static class BlundervilleNavigationRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 WaveOrigin = new(-6, 25.58691f, 190.4f);

    public static void Run(Action<string, Action> run)
    {
        RunHostInputPolicy(run);
        run("Blunderville navigation locks the cast geometry and its real damage window", () =>
        {
            var encounter = NewEncounter();
            var origin = new Vector3(-200, 6, 212);
            var cast = new BlundervilleCast(34800, origin + Vector3.UnitX * 20, 0, Epoch, Epoch.AddSeconds(3.5), origin);
            encounter.StartCast(1, cast, Epoch);
            encounter.Synchronize([new(1, 16352, origin + Vector3.UnitX * 10, Cast: cast with
            {
                SourcePosition = origin + Vector3.UnitX * 10, Rotation = MathF.PI / 2,
            })], Epoch.AddSeconds(0.1));
            var hazard = encounter.Hazards(Epoch.AddSeconds(0.1)).Single();
            Check(hazard.Geometry.Position == origin && hazard.Geometry.Contains(origin + Vector3.UnitZ * 30)
                && !hazard.Geometry.Contains(origin + Vector3.UnitX * 20), "A native actor update moved or rotated the locked cast danger.");
            Check(hazard.From == cast.FinishAt && hazard.Until == cast.FinishAt.AddSeconds(0.2)
                && hazard.Telegraph == cast.StartedAt, "Drawing time replaced the cast's absolute damage window.");
        });

        run("Blunderville navigation cancellation removes both cast and its predicted wave", () =>
        {
            var encounter = NewEncounter();
            var cast = WaveCast();
            encounter.StartCast(1, cast, Epoch);
            Check(encounter.Hazards(Epoch).Count() == 5, "The starting wave did not expose its current and four future rows.");
            encounter.Cancel(1, 34800, Epoch.AddSeconds(0.2));
            Check(encounter.Hazards(Epoch.AddSeconds(0.2)).Count() == 5, "An unrelated cancellation erased a wave.");
            encounter.Cancel(1, 34812, Epoch.AddSeconds(0.3));
            Check(!encounter.Hazards(Epoch.AddSeconds(0.3)).Any(), "A cancelled cast retained navigation danger.");
            encounter.Synchronize([new(1, 16338, WaveOrigin, Cast: cast)], Epoch.AddSeconds(0.4));
            Check(!encounter.Hazards(Epoch.AddSeconds(0.4)).Any(), "A stale native snapshot revived a cancelled wave.");
        });

        run("Blunderville navigation source removal clears pending damage without clearing another source", () =>
        {
            var encounter = NewEncounter();
            encounter.StartCast(1, WaveCast(), Epoch);
            encounter.StartCast(2, new(34717, Vector3.Zero, null, Epoch, Epoch.AddSeconds(2), Vector3.Zero), Epoch);
            encounter.Remove(1);
            var remaining = encounter.Hazards(Epoch).ToArray();
            Check(remaining.Length == 1 && remaining[0].Id == "cast/2", "Source removal leaked predicted rows or erased another source.");
            encounter.Remove(2);
            Check(!encounter.Hazards(Epoch).Any(), "A removed actor still owns damage.");
        });

        run("Blunderville navigation replay rewind discards all future damage", () =>
        {
            var encounter = NewEncounter();
            encounter.StartCast(1, WaveCast(), Epoch);
            encounter.Synchronize([new(2, 16351, new Vector3(10, 6, 220))], Epoch.AddSeconds(0.1));
            Check(encounter.Hazards(Epoch.AddSeconds(0.2)).Any(), "The rewind fixture had no danger.");
            Check(!encounter.Hazards(Epoch.AddSeconds(-1)).Any(), "Seeking backward retained the prior future wave or ball.");
        });

        run("Blunderville navigation wave resynchronization preserves each short timed damage window", () =>
        {
            var encounter = NewEncounter();
            encounter.StartCast(1, WaveCast(), Epoch);
            var observedStart = Epoch.AddSeconds(0.88);
            encounter.Effect(1, 34812, 1, WaveOrigin, 0, observedStart);
            var next = encounter.Hazards(observedStart).Single(h => h.Id == "wave/1/1");
            Check(next.From == Epoch.AddSeconds(1.33) && next.Until == Epoch.AddSeconds(1.53),
                "The recorded starting effect did not calibrate the next row to a 0.45 s delay and 0.2 s damage window.");
            Check(encounter.Previews(observedStart).Single(p => p.Id == next.Id).Outline,
                "The early visual preview was not an outline, so this test cannot distinguish drawing from damage.");
            Check(next.From > observedStart && next.Telegraph == Epoch, "An early outline became immediately active navigation danger.");

            var rowOne = WaveOrigin + new Vector3(0, -2.21464f, 6);
            var observedRow = Epoch.AddSeconds(1.38);
            encounter.Effect(1, 34804, 2, rowOne, 0, observedRow);
            var calibrated = encounter.Hazards(observedRow).ToArray();
            Check(calibrated.All(h => h.Id != "wave/1/1"), "The consumed row remained in the navigation prediction.");
            var rowTwo = calibrated.Single(h => h.Id == "wave/1/2");
            Check(NearTime(rowTwo.From, Epoch.AddSeconds(1.83)) && NearTime(rowTwo.Until, Epoch.AddSeconds(2.03)),
                "A later real effect did not recalibrate the remaining wave rows.");
            Check((rowTwo.Until - rowTwo.From).TotalSeconds == 0.2,
                "The 0.55 s effect preview persistence became the navigation hit duration.");
        });

        run("Blunderville navigation keeps learned charge routes as route outlines", () =>
        {
            var encounter = NewEncounter();
            var a = new Vector3(-202, 6, 240);
            var b = new Vector3(-190, 6, 240);
            encounter.Effect(1, 34774, 1, a, 0, Epoch);
            encounter.Effect(1, 34774, 2, b, 0, Epoch.AddSeconds(0.3));
            var route = encounter.Previews(Epoch.AddSeconds(0.3)).Single(p => p.Id == "route/1");
            Check(route.Outline && route.Geometry.Contains((a + b) / 2), "The observed charge corridor was not retained as a route outline.");
            Check(!encounter.Hazards(Epoch.AddSeconds(0.3)).Any(), "The entire charge travel corridor became permanently dangerous.");
        });

        run("Blunderville navigation moving balls follow the latest object position without an old effect pulse", () =>
        {
            var encounter = NewEncounter();
            var old = new Vector3(0, 6, -280);
            var current = old + Vector3.UnitZ * 8;
            encounter.Synchronize([new(1, 16351, old)], Epoch);
            encounter.Effect(1, 34776, 1, old, 0, Epoch.AddSeconds(0.01));
            encounter.Synchronize([new(1, 16351, current)], Epoch.AddSeconds(0.1));
            var hazard = encounter.Hazards(Epoch.AddSeconds(0.1)).Single();
            Check(hazard.Id == "ball/1" && hazard.Geometry.Position == current && !hazard.Geometry.Contains(old),
                "The old ball effect position remained in navigation danger.");
            Check(hazard.Geometry is CircleGeometry { Radius: 2 }, "The moving ball changed its recorded client radius.");
            encounter.Remove(1);
            Check(!encounter.Hazards(Epoch.AddSeconds(0.2)).Any(), "A removed rolling ball left a danger pulse.");
        });

        run("Blunderville automatic movement lease enables a default-off runtime without changing public flags", () =>
        {
            using var scope = new RuntimeScope();
            Check(scope.Runtime.Manager.BuildSnapshot(Epoch).GoalAreas.Count == 0, "The default-off runtime unexpectedly exposed navigation.");
            scope.Runtime.Update(1, true);
            Check(scope.Move.MoveCount == 0, "A default-off runtime started moving without a race request.");
            var owner = new object();
            scope.Runtime.RequestAutomaticMovement(owner, true);
            Check(scope.Runtime.Manager.BuildSnapshot(Epoch).GoalAreas.Count == 1, "A lease enabled dispatch but left the manager snapshot disabled.");
            scope.DispatchAcceptedRoute();
            Check(scope.Move.IsRunning && scope.Runtime.MovementStatus.HasControl, "The race lease did not dispatch the real solver route.");
            Check(!scope.Runtime.Setting.EnableSafeZone && !scope.Runtime.Setting.EnableAutoMovement,
                "A race lease changed the user's public solver or automatic movement choice.");
        });

        run("Blunderville automatic movement lease belongs to both owners until the last owner releases", () =>
        {
            using var scope = new RuntimeScope();
            var first = new object(); var second = new object();
            scope.Runtime.RequestAutomaticMovement(first, true);
            scope.Runtime.RequestAutomaticMovement(second, true);
            scope.DispatchAcceptedRoute();
            scope.Runtime.RequestAutomaticMovement(first, false);
            scope.Runtime.Update(1, true);
            Check(scope.Move.IsRunning && scope.Move.StopCount == 0, "Releasing one owner cancelled the other's current route.");
            scope.Runtime.RequestAutomaticMovement(second, false);
            scope.Runtime.Update(1, true);
            Check(!scope.Move.IsRunning && scope.Move.StopCount == 1 && !scope.Runtime.MovementStatus.HasControl,
                "The final lease release did not stop its owned route.");
            Check(scope.Runtime.Manager.ContainsArea(scope.Goal), "Lease release destroyed an independently registered goal.");
        });

        run("Blunderville runtime race speed changes the real accepted solver arrival time", () =>
        {
            SafeZoneSolution Solve(float baseSpeed, float multiplier)
            {
                using var scope = new RuntimeScope();
                scope.Runtime.RequestAutomaticMovement(new object(), true, baseSpeed: baseSpeed);
                return scope.SolveRuntimeRoute(multiplier);
            }
            var slow = Solve(3.6f, 1);
            var ordinary = Solve(6, 1);
            var accelerated = Solve(6, 1.3f);
            Check(Vector3.Distance(slow.ExecutablePath[^1].Position, ordinary.ExecutablePath[^1].Position) < 0.1f
                && Vector3.Distance(accelerated.ExecutablePath[^1].Position, ordinary.ExecutablePath[^1].Position) < 0.1f,
                "The speed comparison solved different route destinations.");
            Check(slow.ExecutablePath[^1].PlannedArrivalTime > ordinary.ExecutablePath[^1].PlannedArrivalTime.AddSeconds(0.35),
                "The live runtime still planned the slow 3.6 yalm/s state at ordinary running speed.");
            Check(accelerated.ExecutablePath[^1].PlannedArrivalTime < ordinary.ExecutablePath[^1].PlannedArrivalTime.AddSeconds(-0.1),
                "The runtime dropped the effective speed multiplier before the actual solver captured it.");
        });

        run("Blunderville race speed belongs to its owners and disappears on release or Clear", () =>
        {
            using var scope = new RuntimeScope();
            var slow = new object(); var fast = new object();
            scope.Runtime.RequestAutomaticMovement(fast, true, baseSpeed: 7.8f);
            scope.Runtime.RequestAutomaticMovement(slow, true, baseSpeed: 3.6f);
            Check(scope.Runtime.Setting.RuntimeBaseSpeed == 3.6f, "A faster owner overrode the slower race movement constraint.");
            scope.Runtime.RequestAutomaticMovement(slow, false);
            Check(scope.Runtime.Setting.RuntimeBaseSpeed == 7.8f && scope.Runtime.Setting.RuntimeMovementRequested,
                "Releasing the slower owner lost the remaining owner's speed or movement lease.");
            scope.Runtime.RequestAutomaticMovement(fast, true);
            Check(scope.Runtime.Setting.RuntimeBaseSpeed == null && scope.Runtime.Setting.RuntimeMovementRequested,
                "An owner could not end its speed override while retaining ordinary automatic movement.");
            scope.Runtime.RequestAutomaticMovement(slow, true, baseSpeed: 6);
            scope.Runtime.RequestAutomaticMovement(slow, false);
            Check(scope.Runtime.Setting.RuntimeBaseSpeed == null, "A released owner left its speed behind.");
            scope.Runtime.RequestAutomaticMovement(fast, true, baseSpeed: 3.6f);
            scope.Runtime.Clear();
            Check(scope.Runtime.Setting.RuntimeBaseSpeed == null && !scope.Runtime.Setting.RuntimeMovementRequested
                && !scope.Runtime.Setting.EnableSafeZone && !scope.Runtime.Setting.EnableAutoMovement,
                "Session cleanup retained race speed or changed public automatic movement settings.");
        });

        run("Blunderville scoped arena margin enables a narrow board and restores ordinary clearance", () =>
        {
            using var scope = new RuntimeScope();
            scope.Runtime.Setting.EnableSafeZone = scope.Runtime.Setting.EnableAutoMovement = true;
            scope.Runtime.Manager.SetArenaBoundary(new RectangleGeometry(new Vector3(4, 0, 0), MathF.PI / 2, 1.14f, 12));
            var race = new object(); var ordinary = new object();
            scope.Runtime.RequestAutomaticMovement(race, true, arenaSafetyMargin: 0.1f);
            var narrow = scope.SolveRuntimeRoute(1);
            Check(narrow.ExecutablePath[^1].Position.X >= 7.8f && scope.Runtime.Setting.RuntimeArenaSafetyMargin == 0.1f,
                "The scoped margin was not passed to the real runtime solve across a 1.14 metre board.");
            scope.Runtime.RequestAutomaticMovement(ordinary, true);
            Check(scope.Runtime.Setting.RuntimeArenaSafetyMargin == 0.5f && !scope.Move.IsRunning,
                "An ordinary owner did not restore its full clearance and invalidate the narrow-board route.");
            // A 1.14 m board can retain a single grid row at 0.5 m clearance;
            // the ownership invariant is immediate invalidation and a fresh ordinary-margin solve.
            scope.SolveRuntimeRoute(1, requireMovement: false);
            scope.Runtime.RequestAutomaticMovement(ordinary, false);
            Check(scope.Runtime.Setting.RuntimeArenaSafetyMargin == 0.1f, "Releasing the ordinary owner lost the race's scoped clearance.");
            scope.SolveRuntimeRoute(1);
            scope.Runtime.RequestAutomaticMovement(race, false);
            Check(scope.Runtime.Setting.RuntimeArenaSafetyMargin == null, "The final owner left its arena shrink override behind.");
            scope.Runtime.RequestAutomaticMovement(race, true, arenaSafetyMargin: 0.1f);
            scope.Runtime.Clear();
            Check(scope.Runtime.Setting.RuntimeArenaSafetyMargin == null
                && scope.Runtime.Setting.EnableSafeZone && scope.Runtime.Setting.EnableAutoMovement,
                "Clear retained a scoped margin or changed public activation settings.");
        });

        run("Blunderville reduced arena margin preserves the full danger and obstacle margins", () =>
        {
            var dangerPosition = new Vector3(-2, 0, 0);
            var obstaclePosition = new Vector3(2, 0, 0);
            var snapshot = new SafeZoneSnapshot(1, Epoch, new CircleGeometry(Vector3.Zero, 10),
                [new(new(1, AreaKind.Obstacle), new CircleGeometry(obstaclePosition, 0.1f))],
                [new(new(2, AreaKind.Danger), new CircleGeometry(dangerPosition, 0.1f), [DangerWindow.Persistent(Epoch)])], []);
            var map = RasterizedMap.Build(snapshot, Vector3.Zero,
                new() { GridResolution = 0.1f, HardSafetyMargin = 0.5f, ArenaSafetyMargin = 0.1f }, Epoch, 6);
            foreach (var (position, label) in new[] { (dangerPosition, "danger"), (obstaclePosition, "obstacle") })
            {
                var (column, row) = map.WorldToCell(position + Vector3.UnitZ * 0.35f);
                Check(map.PixelMaxGAt(column, row) <= 0,
                    $"The smaller arena margin weakened the {label} clearance outside its 0.1 metre core.");
            }
        });

        foreach (var existingAutoMovement in new[] { false, true })
            run($"Blunderville race hold immediately stops movement and preserves public flags ({existingAutoMovement})", () =>
            {
                using var scope = new RuntimeScope();
                scope.Runtime.Setting.EnableSafeZone = scope.Runtime.Setting.EnableAutoMovement = existingAutoMovement;
                var first = new object(); var second = new object();
                scope.Runtime.RequestAutomaticMovement(first, true);
                scope.Runtime.RequestAutomaticMovement(second, true);
                scope.DispatchAcceptedRoute();
                scope.Runtime.RequestAutomaticMovement(first, true, true);
                Check(!scope.Move.IsRunning && scope.Move.StopCount == 1, "The hold waited for a later frame instead of stopping immediately.");
                scope.Runtime.Update(1, true);
                Check(!scope.Move.IsRunning && scope.Move.StopCount == 1, "The second active lease restarted movement through a hold.");
                Check(scope.Runtime.Setting.EnableSafeZone == existingAutoMovement && scope.Runtime.Setting.EnableAutoMovement == existingAutoMovement,
                    "A race hold changed public automatic movement settings.");
                scope.Runtime.RequestAutomaticMovement(first, false);
                scope.DispatchAcceptedRoute();
                Check(scope.Move.IsRunning, "Revoking the holding owner failed to resume the other owner's route.");
            });

        foreach (var existingAutoMovement in new[] { false, true })
            run($"Blunderville runtime Clear removes race leases and holds while restoring ordinary settings ({existingAutoMovement})", () =>
            {
                using var scope = new RuntimeScope();
                scope.Runtime.Setting.EnableSafeZone = scope.Runtime.Setting.EnableAutoMovement = existingAutoMovement;
                scope.Runtime.RequestAutomaticMovement(new object(), true, true);
                scope.Runtime.Clear();
                Check(!scope.Runtime.Setting.RuntimeMovementRequested && !scope.Runtime.Setting.RuntimeMovementHeld,
                    "Clearing the session retained a race lease or a hold.");
                Check(scope.Runtime.Setting.EnableSafeZone == existingAutoMovement && scope.Runtime.Setting.EnableAutoMovement == existingAutoMovement,
                    "Clearing the session changed the user's original public settings.");
                Check(scope.Runtime.Setting.EffectiveEnableSafeZone == existingAutoMovement
                    && scope.Runtime.Setting.EffectiveEnableAutoMovement == existingAutoMovement,
                    "Clearing a hold did not restore ordinary runtime activation.");
                Check(!scope.Move.IsRunning && !scope.Runtime.MovementStatus.HasControl,
                    "Session cleanup retained race movement control.");
            });
    }

    private static void RunHostInputPolicy(Action<string, Action> run)
    {
        var assembly = typeof(PromeRotation.Plugin).Assembly;
        var policy = assembly.GetType("PromeRotation.GreenMoveSystem.BlundervilleInputPolicy");
        if (policy == null) return;
        var matches = policy.GetMethod("DirectionMatches", BindingFlags.NonPublic | BindingFlags.Static)!;
        var threshold = MathF.PI / 12;
        var cases = new (string Name, float Desired, float Arrow, bool Allowed)[]
        {
            ("aligned", 0, 0, true),
            ("positive 15 degree boundary", threshold, 0, true),
            ("negative 15 degree boundary", -threshold, 0, true),
            ("outside positive boundary", threshold + 0.001f, 0, false),
            ("outside negative boundary", -threshold - 0.001f, 0, false),
            ("wrap across plus and minus PI", MathF.PI - 0.05f, -MathF.PI + 0.05f, true),
            ("wrong direction across PI", MathF.PI - 0.15f, -MathF.PI + 0.15f, false),
            ("same direction after repeated rotations", 50 * MathF.Tau + 0.03f, 0.03f, true),
            ("NaN requested heading", float.NaN, 0, false),
            ("NaN native arrow", 0, float.NaN, false),
            ("infinite requested heading", float.PositiveInfinity, 0, false),
            ("infinite native arrow", 0, float.NegativeInfinity, false),
        };
        foreach (var test in cases)
            run($"Blunderville PR arrow input policy: {test.Name}", () =>
                Check((bool)matches.Invoke(null, [test.Desired, test.Arrow])! == test.Allowed,
                    "PR started forced movement outside its validated arrow alignment window."));

        var controllerType = assembly.GetType("PromeRotation.GreenMoveSystem.GreenMovementInputController")!;
        if (controllerType.GetProperty("CanControlMisdirection") is { } capability)
            run("Blunderville PR missing the active MC hook never advertises arrow control", () =>
            {
                var input = RuntimeHelpers.GetUninitializedObject(controllerType);
                Check(capability.GetValue(input) is false, "Uninitialized movement advertised native arrow control.");
                controllerType.GetField("hookReady", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(input, true);
                controllerType.GetField("forcedMovementDirection", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(input, (nint)1);
                controllerType.GetProperty("Enabled")!.SetValue(input, true);
                Check(capability.GetValue(input) is false, "An arrow pointer without the MC hook bypassed MP's fallback gate.");
                ((IDisposable)input).Dispose();
                Check(capability.GetValue(input) is false, "Disposed movement retained its native arrow capability.");
            });

        run("Blunderville PR disabling its route immediately clears the forced input latch", () =>
        {
            var type = assembly.GetType("PromeRotation.GreenMoveSystem.GreenMovementInputController")!;
            var input = RuntimeHelpers.GetUninitializedObject(type);
            var enabled = type.GetProperty("Enabled")!;
            var latch = type.GetField("forcedControlState", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var expiry = type.GetField("forcedControlExpires", BindingFlags.NonPublic | BindingFlags.Instance)!;
            enabled.SetValue(input, true);
            latch.SetValue(input, true); expiry.SetValue(input, Environment.TickCount64 + 10000);
            enabled.SetValue(input, false);
            Check(latch.GetValue(input) == null && (long)expiry.GetValue(input)! == 0,
                "Stopping a route retained the forced input decision for its next owner.");
            enabled.SetValue(input, true);
            Check(latch.GetValue(input) == null, "Re-enabling a route resurrected the previous forced input decision.");
            latch.SetValue(input, false); expiry.SetValue(input, Environment.TickCount64 + 10000);
            ((IDisposable)input).Dispose();
            Check(latch.GetValue(input) == null && (long)expiry.GetValue(input)! == 0,
                "Disposing movement retained an input hold that could survive plugin reload.");
        });
    }

    private static BlundervilleEncounter NewEncounter() => new(message => throw new InvalidOperationException(message));
    private static BlundervilleCast WaveCast() => new(34812, WaveOrigin - Vector3.UnitZ * 3, 0, Epoch, Epoch.AddSeconds(1), WaveOrigin);
    private static bool NearTime(DateTime actual, DateTime expected) => Math.Abs(actual.Ticks - expected.Ticks) < 10;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed class RuntimeScope : IDisposable
    {
        private readonly List<(PropertyInfo Property, object? Value)> _services = [];
        private readonly List<string> _errors = [];
        public FakeMove Move { get; } = new();
        public SafeZoneRuntime Runtime { get; }
        public SafeZoneAreaHandle Goal { get; }

        public RuntimeScope()
        {
            var player = ScriptServiceProxy.Create<IPlayerCharacter>((name, _) => name switch
            {
                "get_Position" => Vector3.Zero, "get_Rotation" => 0f,
                "get_IsDead" => false, "get_IsCasting" => false,
                _ => throw new NotSupportedException(name),
            });
            Replace(nameof(Svc.Objects), ScriptServiceProxy.Create<IObjectTable>((name, _) => name switch
            {
                "get_LocalPlayer" => player, "get_Length" => 1,
                _ => throw new NotSupportedException(name),
            }));
            Replace(nameof(Svc.Log), ScriptServiceProxy.Create<IPluginLog>((name, args) =>
            {
                _errors.Add(name + ": " + string.Join(" ", args.Select(a => a?.ToString())));
                return null;
            }));
            Runtime = new(Move, () => false, () => false) { ReplayClock = () => Epoch };
            Runtime.Manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 12));
            Goal = Runtime.Manager.AddGoalArea(new CircleTemplate(0.2f), new StaticTransform(Vector3.UnitX * 8, 0),
                [GoalWindow.Persistent(Epoch, 100, PositionPreference.Center)]);
        }

        public void DispatchAcceptedRoute()
        {
            var snapshot = Runtime.Manager.BuildSnapshot(Epoch);
            var solution = new SafeZoneSolver().Solve(snapshot,
                new PlayerState(Vector3.Zero, 0, 6, false, false, 0, true, snapshot.Version),
                new MovementCapability(6, 6, 0, false, 6, 6), new SolveOptions(), Epoch);
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, "The runtime fixture has no real route to its goal.");
            Runtime.MovementStatus.GetType().GetField("_latestSolution", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Runtime.MovementStatus, solution);
            Runtime.Update(1, true);
            Check(_errors.Count == 0, string.Join(Environment.NewLine, _errors));
        }

        public SafeZoneSolution SolveRuntimeRoute(float multiplier, bool requireMovement = true)
        {
            var acceptedBefore = Runtime.MovementStatus.AsyncSolveAcceptedCount;
            Runtime.Update(multiplier, true);
            var task = Runtime.MovementStatus.GetType().GetField("_pendingSolveTask", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Runtime.MovementStatus) as Task;
            Check(task != null && task.Wait(TimeSpan.FromSeconds(10)), "The runtime's real asynchronous solver did not finish.");
            Runtime.Update(multiplier, true);
            var solution = Runtime.MovementStatus.LatestSolution;
            Check(solution != null && Runtime.MovementStatus.AsyncSolveAcceptedCount == acceptedBefore + 1,
                "The runtime failed to accept the completed real asynchronous solver result.");
            Check(!requireMovement || solution is { IsSuccess: true } && solution.ExecutablePath.Count >= 2 && Move.IsRunning,
                "The runtime failed to accept and dispatch its real speed-dependent route.");
            Check(_errors.Count == 0, string.Join(Environment.NewLine, _errors));
            return solution!;
        }

        private void Replace(string name, object value)
        {
            var property = typeof(Svc).GetProperty(name)!;
            _services.Add((property, property.GetValue(null)));
            property.SetValue(null, value);
        }

        public void Dispose()
        {
            Runtime.Dispose();
            foreach (var (property, value) in _services.AsEnumerable().Reverse()) property.SetValue(null, value);
        }
    }

    private sealed class FakeMove : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _route = [];
        public int MoveCount { get; private set; }
        public int StopCount { get; private set; }
        public bool IsRunning => _route.Count != 0;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _route[0] : null,
            IsRunning ? _route[^1] : null, 0, _route.Count, Math.Max(0, _route.Count - 1));
        public void MoveTo(GreenMovePoint point) => MoveTo([point]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> points) { _route = points.ToArray(); MoveCount++; }
        public void Stop() { _route = []; StopCount++; }
        public void Append(GreenMovePoint point) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> points) => throw new NotSupportedException();
        public void ClearQueue() => throw new NotSupportedException();
        public void SkipCurrent() => throw new NotSupportedException();
    }
}
