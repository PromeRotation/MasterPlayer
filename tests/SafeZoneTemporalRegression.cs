using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using MasterPlayer;
using MasterPlayer.Scripting;
using MasterPlayer.Scripts;
using MasterPlayer.Settings;
using PromeRotation.GreenMoveSystem;
using PromeRotation.Service;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using LegacySolver = PromeRotation.Spatial.SafeZone.Solver;
using HostZone = PromeRotation.Spatial.SafeZone;

internal static class SafeZoneTemporalRegression
{
    private static readonly DateTime Epoch = new(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc);
    private static readonly SolveOptions Options = new() { GenerateDebugData = true };
    private static readonly MovementCapability Capability = new(6f, 6f, 0f, false, 6f, 6f);

    public static void RunPrecision(Action<string, Action> run)
    {
        var center = new Vector3(920f, 15f, -420f);
        var maximumStopError = 0f;
        var slowestSolveMs = 0d;
        foreach (var angle in new[] { 0f, 0.31f, MathF.PI / 4f, MathF.PI / 2f })
        {
            run($"B31 four-point movement stays within 0.2y at route angle {angle}", () =>
            {
                var north = new Vector3(MathF.Sin(angle), 0f, -MathF.Cos(angle));
                var side = new Vector3(north.Z, 0f, -north.X) * 7f;
                var outer = center + north * MathF.Sqrt(18.3f * 18.3f - 7f * 7f);
                var inner = outer - north * 7f;
                Vector3[] route = [inner + side, outer + side, outer - side, inner - side];
                var position = center;
                for (var step = 0; step < route.Length; step++)
                {
                    var snapshot = PreciseGoal(route[step], center, step + 1,
                        route.Take(Math.Max(0, step - 1)).Select(p =>
                            new DangerAreaSnapshot(default, new CircleTemplate(6.5f).Resolve(p, 0f, null),
                                [new DangerWindow(Epoch, null)])).ToArray());
                    var started = System.Diagnostics.Stopwatch.StartNew();
                    var solution = Solve(snapshot, position, Epoch);
                    slowestSolveMs = Math.Max(slowestSolveMs, started.Elapsed.TotalMilliseconds);
                    Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2,
                        $"Bait point {step + 1} must remain reachable despite its small target and previous pools.");
                    Check(solution.DebugData!.GridResolution <= 0.1001f, "The small target must refine the planning grid.");
                    var move = new FakeMove();
                    Dispatch(CreateMovement(move), solution, position, snapshot, Epoch);
                    var final = move.GetStatus().FinalPoint;
                    Check(final.HasValue && Vector3.Distance(final.Value.Position, route[step]) + final.Value.Tolerance <= 0.2f,
                        "Grid-center error plus the dispatched stop tolerance must fit inside the 0.2y circle.");
                    for (var tick = 0; tick < 600 && move.IsRunning; tick++)
                    {
                        position = move.Advance(position, 0.01f);
                        CheckSafe(snapshot, position, Epoch);
                    }
                    Check(!move.IsRunning && Vector3.Distance(position, route[step]) <= 0.2f,
                        $"Bait point {step + 1} stopped too early: error={Vector3.Distance(position, route[step]):F4}y.");
                    maximumStopError = MathF.Max(maximumStopError, Vector3.Distance(position, route[step]));
                }
                var finalPools = new SafeZoneSnapshot(5, Epoch, new CircleTemplate(19.05f).Resolve(center, 0f, null), [],
                    route.Select(p => new DangerAreaSnapshot(default, new CircleTemplate(6.5f).Resolve(p, 0f, null),
                        [new DangerWindow(Epoch, null)])).ToArray(), []);
                CheckPoolEscape(finalPools, position);
            });
        }

        foreach (var offset in new[] { new Vector3(0.21f, 0f, 0f), new Vector3(-0.21f, 0f, 0f),
            new Vector3(0f, 0f, 0.21f), new Vector3(0f, 0f, -0.21f) })
        {
            run($"0.21y target error still dispatches movement from {offset}", () =>
            {
                var target = center + new Vector3(-7f, 0f, -9.9071f);
                var snapshot = PreciseGoal(target, center);
                var position = target + offset;
                var solution = Solve(snapshot, position, Epoch);
                var move = new FakeMove();
                Dispatch(CreateMovement(move), solution, position, snapshot, Epoch);
                Check(move.IsRunning, "A player outside the 0.2y circle must not be considered settled.");
                for (var tick = 0; tick < 30 && move.IsRunning; tick++)
                    position = move.Advance(position, 0.01f);
                Check(!move.IsRunning && Vector3.Distance(position, target) <= 0.2f,
                    "The movement executor must correct the remaining error.");
            });
        }

        run("0.19y positioning settles, but the settled lock cannot hide a later 0.21y error", () =>
        {
            var snapshot = PreciseGoal(center, center);
            var settled = Solve(snapshot, center + new Vector3(0.19f, 0f, 0f), Epoch);
            Check(settled.IsSuccess && settled.ExecutablePath.Count == 0,
                "A safe player already within 0.2y should hold position.");
            var displaced = new SafeZoneSolver().Solve(snapshot, Player(center + new Vector3(0.21f, 0f, 0f)),
                Capability, Options, Epoch.AddSeconds(0.1), settled);
            Check(displaced.ExecutablePath.Count >= 2, "A previous settled goal must not suppress correction outside the circle.");
        });

        run("small targets do not override active poison pools", () =>
        {
            var target = center + new Vector3(7f, 0f, 0f);
            var snapshot = PreciseGoal(target, center, pools:
                [new DangerAreaSnapshot(default, new CircleTemplate(6.5f).Resolve(target, 0f, null),
                    [new DangerWindow(Epoch, null)])]);
            var solution = Solve(snapshot, center, Epoch);
            Check(solution.TargetPosition is not { } destination || Vector3.Distance(destination, target) > 6.5f,
                "An unreachable precise target must never authorize standing in a poison pool.");
            foreach (var point in solution.ExecutablePath)
                CheckSafe(snapshot, point.Position, Epoch);
        });

        run("expired precise goals restore the ordinary grid resolution", () =>
        {
            var snapshot = PreciseGoal(center, center);
            var map = RasterizedMap.Build(snapshot, center, Options, Epoch.AddSeconds(31), 6f);
            Check(map.Resolution == Options.GridResolution, "An expired bait must not keep the finer grid active.");
        });
        Console.WriteLine($"INFO B31 precise route: worst stop error={maximumStopError:F4}y, slowest solve={slowestSolveMs:F1}ms");
    }

    private static SafeZoneSnapshot PreciseGoal(Vector3 target, Vector3 center, int version = 1, DangerAreaSnapshot[]? pools = null)
        => new(version, Epoch, new CircleTemplate(19.05f).Resolve(center, 0f, null), [], pools ?? [],
            [new GoalAreaSnapshot(default, new CircleTemplate(0.2f).Resolve(target, 0f, null),
                [new GoalWindow(Epoch, Epoch.AddSeconds(30), 0, PositionPreference.Center)])]);

    public static void CheckPoolEscape(SafeZoneSnapshot snapshot, Vector3 position)
    {
        var now = DateTime.UtcNow;
        var solution = Solve(snapshot, position, now);
        Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2,
            "A pool appearing under the player must produce an escape route even without a goal.");
        var move = new FakeMove();
        Dispatch(CreateMovement(move), solution, position, snapshot, now);
        Check(move.IsRunning, "An escape solution must actually dispatch movement.");
        var otherPools = snapshot.DangerAreas.Where(d => !d.Geometry.Contains(position)).ToArray();
        var escaped = false;
        for (var tick = 0; tick < 600 && move.IsRunning; tick++)
        {
            position = move.Advance(position, 0.01f);
            var outside = snapshot.DangerAreas.All(d => !d.Geometry.Contains(position));
            Check(otherPools.All(d => !d.Geometry.Contains(position)), "Escaping the new pool must not enter an older pool.");
            Check(!escaped || outside, "After leaving the pool the route must never re-enter it.");
            Check(snapshot.ArenaBoundary!.Contains(position), "Escaping must stay within the arena.");
            escaped |= outside;
        }
        Check(escaped && !move.IsRunning, "Movement must stop outside the persistent pool, accounting for its stop tolerance.");
    }

    public static void Run(Action<string, Action> run)
    {
        run("solver teleporter edge crosses a wall to reach the goal", () =>
        {
            var boundary = new RectangleTemplate(40f, 40f).Resolve(Vector3.Zero, 0f, null);
            var wall = new ObstacleSnapshot(default, new RectangleTemplate(2f, 40f).Resolve(Vector3.Zero, 0f, null));
            var goal = new GoalAreaSnapshot(default, new CircleTemplate(2f).Resolve(new Vector3(15f, 0f, 0f), 0f, null),
                [new GoalWindow(Epoch, null, 1, PositionPreference.Center)]);
            var snapshot = new SafeZoneSnapshot(1, Epoch, boundary, [wall], [], [goal]);
            var start = new Vector3(-15f, 0f, 0f);

            var blocked = new SafeZoneSolver().Solve(snapshot, Player(start), Capability, Options, Epoch);
            var blockedEnd = blocked.ExecutablePath.Count >= 2 ? blocked.ExecutablePath[^1].Position : start;
            Check(Vector3.Distance(blockedEnd, new Vector3(15f, 0f, 0f)) > 6f,
                $"without a teleporter the wall must keep the goal unreachable (end={blockedEnd})");

            var withEdge = Options with
            {
                Teleporters = [new SolverTeleporter(new Vector3(-2.2f, 0f, 0f), new Vector3(2.2f, 0f, 0f), 1.5f, false, 0.5f)],
            };
            var solved = new SafeZoneSolver().Solve(snapshot, Player(start), Capability, withEdge, Epoch);
            Check(solved.IsSuccess && solved.ExecutablePath.Count >= 2, "teleporter edge must produce a route");
            var end = solved.ExecutablePath[^1].Position;
            Check(Vector3.Distance(end, new Vector3(15f, 0f, 0f)) <= 2.5f,
                $"teleporter route must reach the goal across the wall (end={end})");
            Check(solved.ExecutablePath.Skip(1).Any(w => w.Position.X > 1f),
                "route never crossed the wall despite the teleporter edge");
        });

        foreach (var hard in new[] { true, false })
        {
            foreach (var offset in new[] { 0f, 4.49f })
                run($"a {(hard ? "hard obstacle" : "persistent danger")} appearing over the player permits escape from {offset}y", () =>
                {
                    var area = new CircleTemplate(4.5f).Resolve(Vector3.Zero, 0f, null);
                    var snapshot = new SafeZoneSnapshot(1, Epoch, new CircleTemplate(20f).Resolve(Vector3.Zero, 0f, null),
                        hard ? [new ObstacleSnapshot(default, area)] : [],
                        hard ? [] : [new DangerAreaSnapshot(default, area, [new DangerWindow(Epoch, null)])], []);
                    CheckAreaEscape(snapshot, Vector3.UnitX * offset);
                });

            run($"escape from a {(hard ? "hard obstacle" : "persistent danger")} stops outside even with zero safety margin", () =>
            {
                var area = new CircleTemplate(4.5f).Resolve(Vector3.Zero, 0f, null);
                var snapshot = new SafeZoneSnapshot(1, Epoch, new CircleTemplate(20f).Resolve(Vector3.Zero, 0f, null),
                    hard ? [new ObstacleSnapshot(default, area)] : [],
                    hard ? [] : [new DangerAreaSnapshot(default, area, [new DangerWindow(Epoch, null)])], []);
                CheckAreaEscape(snapshot, Vector3.Zero, options: Options with { HardSafetyMargin = 0f, GenerateDebugData = false });
            });

            run($"overlapping {(hard ? "hard obstacles" : "persistent dangers")} allow only exit, including during replanning", () =>
            {
                var areas = new[] { -1.5f, 1.5f }.Select(x => new CircleTemplate(4f).Resolve(Vector3.UnitX * x, 0f, null)).ToArray();
                var otherObstacle = new ObstacleSnapshot(default, new CircleTemplate(2.5f).Resolve(-Vector3.UnitZ * 5.5f, 0f, null));
                var otherDanger = new DangerAreaSnapshot(default, new CircleTemplate(2.5f).Resolve(Vector3.UnitZ * 5.5f, 0f, null),
                    [new DangerWindow(Epoch, null)]);
                var snapshot = new SafeZoneSnapshot(1, Epoch, new CircleTemplate(20f).Resolve(Vector3.Zero, 0f, null),
                    hard ? [.. areas.Select(a => new ObstacleSnapshot(default, a)), otherObstacle] : [otherObstacle],
                    hard ? [otherDanger] : [.. areas.Select(a => new DangerAreaSnapshot(default, a, [new DangerWindow(Epoch, null)])), otherDanger], []);
                CheckAreaEscape(snapshot, Vector3.Zero, replan: true);
            });

            run($"an inescapable {(hard ? "hard obstacle" : "persistent danger")} cannot authorize leaving the arena", () =>
            {
                var area = new CircleTemplate(6f).Resolve(Vector3.Zero, 0f, null);
                var snapshot = new SafeZoneSnapshot(1, Epoch, new CircleTemplate(5f).Resolve(Vector3.Zero, 0f, null),
                    hard ? [new ObstacleSnapshot(default, area)] : [],
                    hard ? [] : [new DangerAreaSnapshot(default, area, [new DangerWindow(Epoch, null)])], []);
                var solution = Solve(snapshot, Vector3.Zero, Epoch);
                Check(solution.Status == SolutionStatus.NoReachableSafeCell && solution.ExecutablePath.Count == 0,
                    "A fully blocked arena must report no escape instead of a safe no-movement result or an out-of-bounds route.");
            });
        }

        run("a hard obstacle interrupts cast waiting even with a future AOE and an overlapping goal", () =>
        {
            var arena = new CircleTemplate(20f).Resolve(Vector3.Zero, 0f, null);
            var future = new DangerAreaSnapshot(default, new CircleTemplate(10f).Resolve(Vector3.Zero, 0f, null),
                [new DangerWindow(Epoch.AddSeconds(10), Epoch.AddSeconds(10.6), Epoch)]);
            var player = Player(Vector3.Zero) with { IsCasting = true, CastTimeRemaining = 2f };
            var move = new FakeMove();
            var movement = CreateMovement(move);
            var before = new SafeZoneSnapshot(1, Epoch, arena, [], [future], []);
            Dispatch(movement, new SafeZoneSolver().Solve(before, player, Capability, Options, Epoch), player, before, Epoch);
            Check(!move.IsRunning, "A future-only AOE should permit finishing the cast.");
            var snapshot = new SafeZoneSnapshot(2, Epoch, arena,
                [new ObstacleSnapshot(default, new CircleTemplate(4.5f).Resolve(Vector3.Zero, 0f, null))], [future],
                [new GoalAreaSnapshot(default, new CircleTemplate(1f).Resolve(Vector3.Zero, 0f, null), [GoalWindow.Persistent(Epoch)])]);
            var solution = new SafeZoneSolver().Solve(snapshot, player, Capability, Options, Epoch);
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, "An overlapping goal must not settle the player inside the obstacle.");
            Dispatch(movement, solution, player, snapshot, Epoch);
            Check(move.IsRunning && ((ISafeZoneRebornMovementStatus)movement).HasControl,
                "A newly covering hard obstacle must interrupt waiting and dispatch escape immediately.");
            CheckAreaEscape(snapshot, player.Position);
        });

        run("movement routes around persistent poison including the default 0.5y margin", () =>
        {
            var target = new Vector3(10f, 0f, 0f);
            var snapshot = PreciseGoal(target, Vector3.Zero, pools:
                [new DangerAreaSnapshot(default, new CircleTemplate(6.5f).Resolve(Vector3.Zero, 0f, null),
                    [new DangerWindow(Epoch, null)])]);
            var position = -target;
            var solution = Solve(snapshot, position, Epoch);
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, "A goal across the pool must remain reachable by going around it.");
            var move = new FakeMove();
            Dispatch(CreateMovement(move), solution, position, snapshot, Epoch);
            for (var tick = 0; tick < 1000 && move.IsRunning; tick++)
            {
                position = move.Advance(position, 0.01f);
                CheckSafe(snapshot, position, Epoch);
            }
            Check(!move.IsRunning && Vector3.Distance(position, target) <= 0.2f,
                "Avoiding a pool must still arrive within 0.2y of the requested target.");
        });

        foreach (var activeFirst in new[] { true, false })
            run($"active poison interrupts cast waiting despite a future AOE (active first: {activeFirst})", () =>
            {
                var future = new DangerAreaSnapshot(default, new CircleTemplate(10f).Resolve(Vector3.Zero, 0f, null),
                    [new DangerWindow(Epoch.AddSeconds(10), Epoch.AddSeconds(10.6), Epoch)]);
                var pool = new DangerAreaSnapshot(default, new CircleTemplate(6.5f).Resolve(Vector3.Zero, 0f, null),
                    [new DangerWindow(Epoch, null)]);
                var arena = new CircleTemplate(20f).Resolve(Vector3.Zero, 0f, null);
                var player = Player(Vector3.Zero) with { IsCasting = true, CastTimeRemaining = 2f };
                var move = new FakeMove();
                var movement = CreateMovement(move);
                var before = new SafeZoneSnapshot(1, Epoch, arena, [], [future], []);
                var pending = new SafeZoneSolver().Solve(before, player, Capability, Options, Epoch);
                Dispatch(movement, pending, player, before, Epoch);
                Check(!move.IsRunning, "A future-only threat with ample time should still permit finishing the cast.");
                var snapshot = new SafeZoneSnapshot(2, Epoch, arena, [], activeFirst ? [pool, future] : [future, pool], []);
                var solution = new SafeZoneSolver().Solve(snapshot, player, Capability, Options, Epoch);
                Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, "The active pool must have an escape path.");
                Dispatch(movement, solution, player, snapshot, Epoch);
                Check(move.IsRunning && ((ISafeZoneRebornMovementStatus)movement).HasControl,
                    "An already-active pool must immediately end cast waiting regardless of danger registration order.");
            });

        run("Reborn temporal planning leaves the legacy solver behavior unchanged", () =>
        {
            var position = new Vector3(8f, 0f, 0f);
            var hostSnapshot = new HostZone.SafeZoneSnapshot(1, Epoch,
                new HostZone.RectangleTemplate(24f, 10f).Resolve(Vector3.Zero, 0f, null), [],
                [
                    new HostZone.DangerAreaSnapshot(default,
                        new HostZone.RectangleTemplate(13f, 12f).Resolve(new(-6f, 0f, 0f), 0f, null),
                        [new HostZone.DangerWindow(Epoch.AddSeconds(2), Epoch.AddSeconds(2.6), Epoch)]),
                    new HostZone.DangerAreaSnapshot(default,
                        new HostZone.RectangleTemplate(13f, 12f).Resolve(new(6f, 0f, 0f), 0f, null),
                        [new HostZone.DangerWindow(Epoch.AddSeconds(4), Epoch.AddSeconds(4.6), Epoch)])
                ], []);
            var legacy = new LegacySolver.SafeZoneSolver().Solve(hostSnapshot,
                new LegacySolver.PlayerState(position, 0f, 6f, false, false, 0f, true, 1),
                new LegacySolver.MovementCapability(6f, 6f, 0f, false, 6f, 6f),
                new LegacySolver.SolveOptions(), Epoch);
            Check(legacy.ExecutablePath.Count < 2, "Legacy behavior for a fully covered arena must stay unchanged.");
            Check(Solve(Staggered(), position, Epoch).ExecutablePath.Count >= 2,
                "The same snapshot must activate temporal staging only in Reborn.");
        });

        run("plugin settings, areas and boundaries are independent of the host", () =>
        {
            var legacySetting = new HostZone.SafeZoneSetting { EnableSafeZone = true, EnableAutoMovement = true };
            using var legacyManager = new HostZone.SafeZoneManager(legacySetting);
            var hostRegistry = new HostZone.SafeZoneRegistry(legacyManager);
            using var plugin = new MasterPlayerPlugin();
            var rebornSetting = plugin.SafeZone.Setting;
            Check(typeof(SafeZoneSolver).Assembly != typeof(PromeRotation.Plugin).Assembly &&
                typeof(SafeZoneManager).Assembly != typeof(HostZone.SafeZoneManager).Assembly,
                "The plugin must own both its solver and its registry data types.");
            var legacyBoundary = new HostZone.CircleTemplate(20f).Resolve(Vector3.Zero, 0f, null);
            hostRegistry.SetArenaBoundary(legacyBoundary);
            var legacyArea = hostRegistry.AddPersistentDanger(new HostZone.CircleTemplate(2f), new HostZone.StaticTransform(Vector3.Zero, 0f));
            var legacyVersion = legacyManager.Version;
            using (var activation = new SafeZoneActivation(MasterPlayerPlugin.Instance!.SafeZone.Setting))
            {
                activation.Update(true, true, true);
                Check(rebornSetting.EnableSafeZone && rebornSetting.EnableAutoMovement, "Activation must enable Reborn.");
                MasterPlayerPlugin.Instance!.SafeZone.Registry.SetArenaBoundary(new CircleTemplate(5f).Resolve(Vector3.Zero, 0f, null));
                var rebornArea = MasterPlayerPlugin.Instance!.SafeZone.Registry.AddPersistentDanger(new CircleTemplate(4f), new StaticTransform(Vector3.UnitX * 8f, 0f));
                Check(MasterPlayerPlugin.Instance!.SafeZone.Manager.BuildSnapshot(DateTime.UtcNow).DangerAreas.Count == 1,
                    "Reborn must contain only its own area, excluding legacy registrations.");
                MasterPlayerPlugin.Instance!.SafeZone.Registry.Remove(rebornArea);
                MasterPlayerPlugin.Instance!.SafeZone.Registry.ClearAll();
                Check(legacyManager.ContainsArea(legacyArea) && legacyManager.Version == legacyVersion,
                    "Reborn removal and clearing must not mutate legacy areas or their version.");
                Check(ReferenceEquals(legacyManager.BuildSnapshot(DateTime.UtcNow).ArenaBoundary, legacyBoundary),
                    "Reborn boundary changes must not replace or clear the legacy boundary.");
            }
            Check(!rebornSetting.EnableSafeZone && !rebornSetting.EnableAutoMovement &&
                legacySetting.EnableSafeZone && legacySetting.EnableAutoMovement,
                "Releasing MasterPlayer activation must leave legacy switches unchanged.");
            plugin.SafeZone.Dispose();
            Check(legacyManager.ContainsArea(legacyArea), "Disposing Reborn must not remove legacy areas.");
        });

        run("legacy automatic movement takes priority before it has a route", () =>
        {
            var snapshot = Staggered();
            var position = new Vector3(8f, 0f, 0f);
            var solution = Solve(snapshot, position, Epoch);
            var legacySetting = new SafeZoneSetting { EnableSafeZone = true, EnableAutoMovement = true };
            var move = new FakeMove();
            var movement = CreateMovement(move, legacySetting);
            Dispatch(movement, solution, position, snapshot, Epoch);
            var status = (ISafeZoneRebornMovementStatus)movement;
            Check(!status.HasControl && !move.IsRunning && move.StopCount == 0 && status.PhaseText == "让位宿主自动移动",
                "Reborn must neither issue nor stop movement when legacy automation is enabled.");
            legacySetting.EnableAutoMovement = false;
            Dispatch(movement, solution, position, snapshot, Epoch);
            Check(status.HasControl && move.IsRunning, "Reborn must resume after legacy automation is disabled.");
            legacySetting.EnableAutoMovement = true;
            Dispatch(movement, solution, position, snapshot, Epoch);
            Check(!status.HasControl && !move.IsRunning && move.StopCount == 1,
                "If legacy has not issued a route, yielding must stop the existing Reborn route once.");
        });

        run("Crucible boundary registration and stopping use only Reborn", () =>
        {
            using var plugin = new MasterPlayerPlugin();
            using var legacy = new HostZone.SafeZoneManager(new HostZone.SafeZoneSetting { EnableSafeZone = true });
            var reborn = plugin.SafeZone.Manager;
            plugin.SafeZone.Setting.EnableSafeZone = true;
            var legacyBoundary = new HostZone.CircleTemplate(20f).Resolve(Vector3.Zero, 0f, null);
            new HostZone.SafeZoneRegistry(legacy).SetArenaBoundary(legacyBoundary);
            var version = legacy.Version;
            var script = new CrucibleHigh1Script();
            var trackerType = typeof(CrucibleHigh1Script).GetNestedType("CastAoeTracker", BindingFlags.NonPublic)!;
            SetField(script, "_tracker", Activator.CreateInstance(trackerType, script)!);
            try
            {
                var boundary = new CircleTemplate(5f).Resolve(Vector3.Zero, 0f, null);
                var setBoundary = typeof(CrucibleHigh1Script).GetMethod("SetBoundaryShape", BindingFlags.Instance | BindingFlags.NonPublic)!;
                setBoundary.Invoke(script, [boundary]);
                Check(ReferenceEquals(reborn.BuildSnapshot(DateTime.UtcNow).ArenaBoundary, boundary),
                    "The actual script boundary must be registered with the same solver as its danger areas.");
                setBoundary.Invoke(script, [null]);
                Check(reborn.BuildSnapshot(DateTime.UtcNow).ArenaBoundary is null, "Group cleanup must clear Reborn's boundary.");
                setBoundary.Invoke(script, [boundary]);
                typeof(CrucibleHigh1Script).GetMethod("OnStop", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(script, null);
                Check(reborn.BuildSnapshot(DateTime.UtcNow).ArenaBoundary is null && script.Errors.Count == 0,
                    "Stopping the script must clear its Reborn boundary without errors.");
                Check(legacy.Version == version && ReferenceEquals(legacy.BuildSnapshot(DateTime.UtcNow).ArenaBoundary, legacyBoundary),
                    "Neither registration nor either cleanup path may touch a legacy script's boundary.");
            }
            finally
            {
                script.SafeZone.Dispose();
            }
        });

        foreach (var owner in typeof(PromeRotation.Plugin).GetProperty("SafeZoneRebornMovementStatus") is null
            ? new[] { "none", "legacy", "plugin", "replay" } : new[] { "none", "legacy", "Reborn", "plugin", "replay" })
        {
            run($"script route cleanup respects actual safe-zone ownership: {owner}", () => CheckScriptCleanup(owner));
        }

        run("Reborn release preserves a route already taken by legacy", () =>
        {
            var snapshot = Staggered();
            var position = new Vector3(8f, 0f, 0f);
            var solution = Solve(snapshot, position, Epoch);
            var legacy = new LegacyStatus();
            var move = new FakeMove();
            var movement = CreateMovement(move, legacyStatus: legacy);
            Dispatch(movement, solution, position, snapshot, Epoch);
            var legacyTarget = new GreenMovePoint(new Vector3(11f, 0f, 0f));
            move.MoveTo(legacyTarget);
            legacy.HasControl = true;
            Dispatch(movement, solution, position, snapshot, Epoch);
            Check(!((ISafeZoneRebornMovementStatus)movement).HasControl && move.GetStatus().FinalPoint == legacyTarget && move.StopCount == 0,
                "Yielding must not overwrite or stop a legacy-owned route, even before its switches are observed.");
            // ReleaseControl is also used on feature shutdown and disposal before the next dispatch.
            SetField(movement, "_hasControl", true);
            movement.GetType().GetMethod("ReleaseControl", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(movement, null);
            Check(move.IsRunning && move.StopCount == 0 && !((ISafeZoneRebornMovementStatus)movement).HasControl,
                "Shutdown must drop stale ownership without stopping the legacy route.");
        });

        run("staggered AOEs require staging before the first area clears", () =>
        {
            var snapshot = Staggered();
            var start = new Vector3(8f, 0f, 0f);
            var solution = Solve(snapshot, start, Epoch);
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2,
                "Standing deep in the later AOE must not be treated as the best position: move toward the area that clears first.");
            Check(solution.TargetPosition!.Value.X < start.X - 1f,
                "The staging destination must reduce the distance to the area that clears first.");
        });

        foreach (var startX in new[] { -6f, 8f })
        {
            run($"staggered AOEs have a safe complete timed route from x={startX}", () =>
            {
                var snapshot = Staggered();
                var solution = Solve(snapshot, new Vector3(startX, 0f, 0f), Epoch);
                Check(solution.IsSuccess, $"Expected a feasible timed route, got {solution.Status}.");
                var path = solution.DebugData!.RawPath;
                Check(path.Any(p => p.IsWaitPoint), "The full plan must wait for the earlier hit to finish.");
                Check(path[^1].Position.X < -0.5f, "The full plan must return to the earlier area before the later hit.");
                Check(solution.ExecutablePath[^1].IsWaitPoint, "Only the first movement phase may be sent to GreenMove.");
                ValidatePath(snapshot, path, 5.2f);
            });
        }

        run("overlapping windows support repeated crossings of the same cells", () =>
        {
            var original = Staggered();
            var dangers = original.DangerAreas.Select(d => d with
            {
                DangerWindows = [d.DangerWindows[0], d.DangerWindows[0] with
                {
                    DangerFrom = d.DangerWindows[0].DangerFrom.AddSeconds(4),
                    DangerUntil = d.DangerWindows[0].DangerUntil!.Value.AddSeconds(4),
                }],
            }).ToArray();
            var snapshot = new SafeZoneSnapshot(1, Epoch, original.ArenaBoundary, [], dangers, []);
            var solution = Solve(snapshot, new Vector3(8f, 0f, 0f), Epoch);
            Check(solution.IsSuccess, "Repeated waves with sufficient gaps must be solvable.");
            Check(solution.DebugData!.RawPath.Count(p => p.IsWaitPoint) >= 3,
                "The planner must retain separate safe intervals when revisiting a cell.");
            ValidatePath(snapshot, solution.DebugData.RawPath, 9.2f);
        });

        foreach (var laterHit in new[] { 2f, 3f })
        {
            run($"AOEs with no safe transfer window are rejected: later hit={laterHit}s", () =>
            {
                var original = Staggered();
                var later = original.DangerAreas[1] with
                {
                    DangerWindows = [new DangerWindow(Epoch.AddSeconds(laterHit), Epoch.AddSeconds(laterHit + 0.6f), Epoch)],
                };
                var snapshot = new SafeZoneSnapshot(1, Epoch, original.ArenaBoundary, [], [original.DangerAreas[0], later], []);
                var solution = Solve(snapshot, new Vector3(8f, 0f, 0f), Epoch);
                Check(solution.Status == SolutionStatus.NoTemporalPath && solution.ExecutablePath.Count == 0,
                    "Simultaneous or overlapping hit windows must not produce a supposedly safe staging route.");
            });
        }

        run("hard obstacles cannot be crossed while waiting for staggered AOEs", () =>
        {
            var original = Staggered();
            var wall = new ObstacleSnapshot(default, new RectangleTemplate(2f, 12f).Resolve(Vector3.Zero, 0f, null));
            var snapshot = new SafeZoneSnapshot(1, Epoch, original.ArenaBoundary, [wall], original.DangerAreas, []);
            Check(Solve(snapshot, new Vector3(8f, 0f, 0f), Epoch).Status == SolutionStatus.NoTemporalPath,
                "Time passing must never reopen a permanent obstacle.");
        });

        run("waiting takes control of an existing follow route only once", () =>
        {
            var snapshot = Staggered();
            var staging = Solve(snapshot, new Vector3(8f, 0f, 0f), Epoch).TargetPosition!.Value;
            var now = Epoch.AddSeconds(1);
            var solution = Solve(snapshot, staging, now);
            Check(solution.ExecutablePath.Count == 1 && solution.ExecutablePath[0].IsWaitPoint,
                "The staged player should wait for the earlier area to clear.");
            var move = new FakeMove();
            move.MoveTo(new GreenMovePoint(new Vector3(-8f, 0f, 0f)));
            var movement = CreateMovement(move);
            Dispatch(movement, solution, staging, snapshot, now);
            Check(!move.IsRunning && ((ISafeZoneRebornMovementStatus)movement).HasControl,
                "An existing follow route must not continue while the solver holds position.");
            Check(move.Advance(staging, 0.1f) == staging, "The player must stay at the waiting position.");
            var stops = move.StopCount;
            Dispatch(movement, solution, staging, snapshot, now.AddSeconds(0.05));
            Check(move.StopCount == stops, "Holding must not resend Stop every frame.");
        });

        run("an asynchronously delayed staging route is not dispatched after its departure budget", () =>
        {
            var snapshot = Staggered();
            var position = new Vector3(8f, 0f, 0f);
            var solution = Solve(snapshot, position, Epoch);
            var move = new FakeMove();
            Dispatch(CreateMovement(move), solution, position, snapshot, Epoch.AddSeconds(2.85));
            Check(!move.IsRunning, "A stale plan must be rejected when the player cannot reach the waiting point in time.");
        });

        run("movement dispatch stages, holds and enters the cleared AOE", () =>
        {
            var snapshot = Staggered();
            var position = new Vector3(8f, 0f, 0f);
            var move = new FakeMove();
            var movement = CreateMovement(move);
            var sawHold = false;
            var reachedClearedArea = false;
            for (var tick = 0; tick <= 104; tick++)
            {
                var now = Epoch.AddSeconds(tick * 0.05);
                var solution = Solve(snapshot, position, now);
                Check(solution.IsSuccess, $"Rolling solve unexpectedly failed at t={tick * 0.05:F2}: {solution.Status}");
                Dispatch(movement, solution, position, snapshot, now);
                var status = (ISafeZoneRebornMovementStatus)movement;
                if (status.PhaseText == "等待危险结束")
                {
                    sawHold = true;
                    Check(!move.IsRunning && status.HasControl, "Waiting must stop owned movement and keep follow from taking control.");
                }
                position = move.Advance(position, 0.05f);
                CheckSafe(snapshot, position, now.AddSeconds(0.05));
                reachedClearedArea |= tick * 0.05f < 3.5f && position.X < -0.5f;
            }
            Check(sawHold && reachedClearedArea, "Dispatch must both wait and enter the cleared area before the second hit.");
            Check(move.StopCount > 0, "The waiting phase must actually stop the movement system.");
        });
    }

    private static void CheckAreaEscape(SafeZoneSnapshot snapshot, Vector3 position, bool replan = false, SolveOptions? options = null)
    {
        var solveOptions = options ?? Options;
        var areas = snapshot.HardObstacles.Select(o => o.Geometry)
            .Concat(snapshot.DangerAreas.Where(d => d.DangerWindows.Any(w => w.IsActiveAt(Epoch))).Select(d => d.Geometry)).ToArray();
        var left = areas.Select(a => !a.Contains(position)).ToArray();
        void CheckPosition(Vector3 point)
        {
            Check(snapshot.ArenaBoundary!.Contains(point), "Escaping must remain inside the arena.");
            for (var i = 0; i < areas.Length; i++)
            {
                var inside = areas[i].Contains(point);
                Check(!left[i] || !inside, "Escape must neither enter another area nor re-enter an area already left.");
                left[i] |= !inside;
            }
        }

        var solution = new SafeZoneSolver().Solve(snapshot, Player(position), Capability, solveOptions, Epoch);
        Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, "A covering area must produce an escape route without requiring a goal.");
        for (var i = 1; i < solution.ExecutablePath.Count; i++)
        {
            var from = solution.ExecutablePath[i - 1].Position;
            var to = solution.ExecutablePath[i].Position;
            var steps = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(from, to) / 0.02f));
            for (var step = 1; step <= steps; step++)
                CheckPosition(Vector3.Lerp(from, to, (float)step / steps));
        }
        Check(left.All(value => value), "The planned destination must be outside every current danger and obstacle.");

        left = areas.Select(a => !a.Contains(position)).ToArray();
        var move = new FakeMove();
        var movement = CreateMovement(move);
        SetField(movement, "_options", solveOptions);
        Dispatch(movement, solution, position, snapshot, Epoch);
        Check(move.IsRunning, "The escape route must actually start movement.");
        for (var tick = 0; tick < 1000 && move.IsRunning; tick++)
        {
            if (replan && tick > 0 && tick % 20 == 0)
            {
                var now = Epoch.AddSeconds(tick * 0.01);
                Dispatch(movement, new SafeZoneSolver().Solve(snapshot, Player(position), Capability, solveOptions, now), position, snapshot, now);
            }
            position = move.Advance(position, 0.01f);
            CheckPosition(position);
        }
        Check(left.All(value => value) && !move.IsRunning,
            "The movement executor must stop outside all areas, including its arrival tolerance and repeated solves.");
    }

    private static SafeZoneSnapshot Staggered() => new(1, Epoch,
        new RectangleTemplate(24f, 10f).Resolve(Vector3.Zero, 0f, null), [],
        [
            new DangerAreaSnapshot(default,
                new RectangleTemplate(13f, 12f).Resolve(new Vector3(-6f, 0f, 0f), 0f, null),
                [new DangerWindow(Epoch.AddSeconds(2), Epoch.AddSeconds(2.6), Epoch)]),
            new DangerAreaSnapshot(default,
                new RectangleTemplate(13f, 12f).Resolve(new Vector3(6f, 0f, 0f), 0f, null),
                [new DangerWindow(Epoch.AddSeconds(4), Epoch.AddSeconds(4.6), Epoch)])
        ], []);

    private static SafeZoneSolution Solve(SafeZoneSnapshot snapshot, Vector3 position, DateTime now)
        => new SafeZoneSolver().Solve(snapshot, Player(position), Capability, Options, now);

    private static PlayerState Player(Vector3 position) => new(position, 0f, 6f, false, false, 0f, true, 1);

    private static object CreateMovement(FakeMove move, SafeZoneSetting? legacySetting = null, LegacyStatus? legacyStatus = null)
    {
        var type = typeof(SafeZoneSolver).Assembly.GetType("MasterPlayer.Spatial.SafeZoneReborn.SafeZoneRebornMovementSystem")!;
        var movement = RuntimeHelpers.GetUninitializedObject(type);
        SetField(movement, "_setting", new SafeZoneSetting());
        SetField(movement, "_options", Options);
        SetField(movement, "_greenMove", move);
        SetField(movement, "_externalMovementEnabled", (Func<bool>)(() => legacySetting is { EnableSafeZone: true, EnableAutoMovement: true }));
        SetField(movement, "_externalHasControl", (Func<bool>)(() => legacyStatus?.HasControl == true));
        return movement;
    }

    private static void SetField(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

    private static void CheckScriptCleanup(string owner)
    {
        var instance = typeof(PromeRotation.Plugin).GetProperty(nameof(PromeRotation.Plugin.Instance))!;
        var objects = typeof(Svc).GetProperty(nameof(Svc.Objects))!;
        var previousPlugin = instance.GetValue(null);
        var previousObjects = objects.GetValue(null);
        var assembly = typeof(PromeRotation.Plugin).Assembly;
        var plugin = (PromeRotation.Plugin)RuntimeHelpers.GetUninitializedObject(typeof(PromeRotation.Plugin));
        var legacy = RuntimeHelpers.GetUninitializedObject(assembly.GetType("PromeRotation.Spatial.SafeZone.SafeZoneMovementSystem")!);
        var rebornType = assembly.GetType("PromeRotation.Spatial.SafeZoneReborn.SafeZoneRebornMovementSystem");
        var reborn = rebornType is null ? null : RuntimeHelpers.GetUninitializedObject(rebornType);
        using var localPlugin = new MasterPlayerPlugin(new FakeMove());
        SetField(localPlugin.SafeZone.MovementStatus, "_hasControl", owner == "plugin");
        SetField(legacy, "_hasControl", owner == "legacy");
        if (reborn is not null)
            SetField(reborn, "_hasControl", owner == "Reborn");
        SetField(plugin, "_safeZoneMovementSystem", legacy);
        if (reborn is not null)
            SetField(plugin, "_safeZoneRebornMovementSystem", reborn);
        SetField(plugin, "_safeZoneSetting", new HostZone.SafeZoneSetting { EnableSafeZone = true, EnableAutoMovement = true });
        var rebornSetting = new HostZone.SafeZoneSetting { EnableSafeZone = true, EnableAutoMovement = true };
        if (reborn is not null)
            SetField(plugin, "_safeZoneRebornSetting", rebornSetting);

        // Seed an accepted route without installing game hooks or executing game input.
        var movement = RuntimeHelpers.GetUninitializedObject(assembly.GetType("PromeRotation.GreenMoveSystem.GreenMoveManager")!);
        SetField(movement, "gate", new object());
        SetField(movement, "input", RuntimeHelpers.GetUninitializedObject(assembly.GetType("PromeRotation.GreenMoveSystem.GreenMovementInputController")!));
        SetField(movement, "speedMultiplierService", ScriptServiceProxy.Create<ISpeedMultiplierService>((_, _) => null));
        SetField(plugin, "_greenMoveManager", movement);
        try
        {
            instance.SetValue(null, plugin);
            objects.SetValue(null, ScriptServiceProxy.Create<IObjectTable>((_, _) => null));
            var route = new List<GreenMovePoint> { new(Vector3.UnitX), new(Vector3.UnitX * 2), new(Vector3.UnitX * 3) };
            if (owner == "replay")
            {
                plugin.SafeZoneSetting.EnableAutoMovement = rebornSetting.EnableAutoMovement = false;
                localPlugin.Scripts.ReplayModeActive = true;
                SetField(movement, "route", route.ToList());
                SetField(movement, "currentIndex", 0);
                SetField(movement, "isRunning", true);
                var api = new ScriptMoveApi();
                api.MoveTo(new GreenMovePoint(-Vector3.UnitX));
                api.Append(new GreenMovePoint(-Vector3.UnitX));
                api.CatmullRomMoveTo(new GreenMovePoint(-Vector3.UnitX));
                Check(plugin.GreenMoveSystem.GetStatus().FinalPoint == route[^1] && plugin.GreenMoveSystem.GetStatus().TotalCount == route.Count,
                    "Replay must block script movement even when all automatic movement is disabled.");
                return;
            }
            foreach (var operation in new[] { "Stop", "Cleanup", "ClearQueue", "SkipCurrent", "MoveTo" })
            {
                SetField(movement, "route", route.ToList());
                SetField(movement, "currentIndex", 0);
                SetField(movement, "isRunning", true);
                var api = new ScriptMoveApi();
                SetField(api, "_hasControl", true);
                switch (operation)
                {
                    case "Stop": api.Stop(); break;
                    case "Cleanup": api.Cleanup(); break;
                    case "ClearQueue": api.ClearQueue(); break;
                    case "SkipCurrent": api.SkipCurrent(); break;
                    case "MoveTo": api.MoveTo(new GreenMovePoint(-Vector3.UnitX)); break;
                }
                if (owner == "none")
                {
                    api.Cleanup();
                    Check(!plugin.GreenMoveSystem.IsRunning && plugin.GreenMoveSystem.GetStatus().TotalCount == 0,
                        $"{operation}: enabling automation without taking control must still allow cleanup of the script's own route.");
                }
                else
                {
                    api.Cleanup();
                    var status = plugin.GreenMoveSystem.GetStatus();
                    Check(status.IsRunning && status.CurrentIndex == 0 && status.TotalCount == route.Count && status.FinalPoint == route[^1],
                        $"{operation}: script operations must preserve the route now owned by {owner}.");
                }
            }
        }
        finally
        {
            instance.SetValue(null, previousPlugin);
            objects.SetValue(null, previousObjects);
        }
    }

    private sealed class LegacyStatus
    {
        public bool HasControl { get; set; }
    }

    private static void Dispatch(object movement, SafeZoneSolution solution, Vector3 position, SafeZoneSnapshot snapshot, DateTime now)
        => Dispatch(movement, solution, Player(position), snapshot, now);

    private static void Dispatch(object movement, SafeZoneSolution solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now)
        => movement.GetType().GetMethod("Dispatch", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(movement, [solution, player, snapshot, now]);

    private static void ValidatePath(SafeZoneSnapshot snapshot, IReadOnlyList<TimedWaypoint> path, float duration)
    {
        for (var tick = 0; tick <= duration * 100; tick++)
        {
            var now = Epoch.AddSeconds(tick * 0.01);
            var position = path[^1].Position;
            for (var i = 0; i + 1 < path.Count; i++)
            {
                if (now <= path[i].PlannedDepartureTime)
                {
                    position = path[i].Position;
                    break;
                }
                if (now <= path[i + 1].PlannedArrivalTime)
                {
                    var fraction = (float)((now - path[i].PlannedDepartureTime).TotalSeconds /
                        (path[i + 1].PlannedArrivalTime - path[i].PlannedDepartureTime).TotalSeconds);
                    position = Vector3.Lerp(path[i].Position, path[i + 1].Position, fraction);
                    break;
                }
            }
            CheckSafe(snapshot, position, now);
        }
    }

    private static void CheckSafe(SafeZoneSnapshot snapshot, Vector3 position, DateTime now)
    {
        Check(snapshot.ArenaBoundary!.Contains(position), "The route must stay inside the arena.");
        Check(snapshot.HardObstacles.All(o => !o.Geometry.Contains(position)), "The route must avoid hard obstacles.");
        foreach (var danger in snapshot.DangerAreas)
        {
            foreach (var window in danger.DangerWindows)
            {
                var buffered = new DangerWindow(window.DangerFrom.AddSeconds(-Options.DangerStartSafetyTime),
                    window.DangerUntil?.AddSeconds(Options.DangerEndSafetyTime));
                Check(!buffered.IsActiveAt(now) || !danger.Geometry.Contains(position),
                    $"Route intersects a buffered AOE at t={(now - Epoch).TotalSeconds:F3}, position={position}.");
            }
        }
    }

    private sealed class FakeMove : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _points = [];
        private int _index;
        public int StopCount { get; private set; }
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null,
            _points.Count > 0 ? _points[^1] : null, _index, _points.Count, _points.Count - _index);
        public void MoveTo(GreenMovePoint point) => MoveTo([point]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> points) { _points = points; _index = 0; }
        public void Stop() { StopCount++; _points = []; _index = 0; }
        public void Append(GreenMovePoint point) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> points) => throw new NotSupportedException();
        public void ClearQueue() => throw new NotSupportedException();
        public void SkipCurrent() => _index++;

        public Vector3 Advance(Vector3 position, float seconds)
        {
            var distance = 6f * seconds;
            while (IsRunning && distance > 0f)
            {
                var delta = _points[_index].Position - position;
                var length = delta.Length();
                if (length <= _points[_index].Tolerance)
                {
                    _index++;
                    continue;
                }
                var step = MathF.Min(length, distance);
                position += delta / length * step;
                distance -= step;
                if (step >= length)
                    _index++;
            }
            return position;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
