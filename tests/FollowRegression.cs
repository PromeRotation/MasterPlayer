using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using MasterPlayer.Follow;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using PromeRotation.GreenMoveSystem;
using PromeRotation.Spatial.Geometry;
using PromeRotation.Spatial.SafeZone;
using PromeRotation.Spatial.SafeZoneReborn.Solver;

internal static class FollowRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("Follow approaches a battle target and stops at hitbox distance", () =>
        {
            using var scope = new FollowScope();
            scope.Update();
            Check(scope.MoveCount == 1 && scope.LastPoint is { Tolerance: 6f, IgnoreY: true } point &&
                point.Position == scope.TargetPosition, "A living battle target must receive a route with both hitboxes included.");
            scope.PlayerPosition = new Vector3(4f, 0f, 0f);
            scope.Update();
            scope.Update();
            Check(scope.StopCount == 1 && scope.MoveCount == 1, "Reaching the configured edge distance must stop once without chasing the boss center.");
            scope.PlayerPosition = new Vector3(3.95f, 0f, 0f);
            scope.Update();
            Check(scope.MoveCount == 2, "Walking just outside the configured distance must resume following.");
        });

        run("Follow uses the configured center distance for players", () =>
        {
            using var scope = new FollowScope { TargetKind = ObjectKind.Pc, PlayerPosition = new Vector3(8.45f, 0f, 0f) };
            scope.Update();
            Check(scope.MoveCount == 1 && scope.LastPoint?.Tolerance == 1.5f,
                "A player 1.55y away must be approached when the configured distance is 1.5y.");
            scope.PlayerPosition = new Vector3(8.5f, 0f, 0f);
            scope.Update();
            Check(scope.StopCount == 1, "A player at the exact configured distance must stop following.");
        });

        run("Follow resumes a stopped route to a stationary target without spamming active routes", () =>
        {
            using var scope = new FollowScope();
            scope.Update();
            scope.IsRunning = false;
            scope.Update();
            Check(scope.MoveCount == 2, "An expired route must be reissued even when the target has not moved.");
            for (var i = 0; i < 10; i++)
                scope.Update();
            Check(scope.MoveCount == 2, "An active route, including a cast/input pause, must not be reset each frame.");
        });

        run("Follow refreshes a moving route when distance or target size changes", () =>
        {
            using var scope = new FollowScope();
            scope.Update();
            scope.Settings.FollowDistance = 0.5f;
            scope.Update();
            Check(scope.MoveCount == 2 && scope.LastPoint?.Tolerance == 5f, "Changing distance must update the active route tolerance.");
            scope.TargetRadius = 2f;
            scope.Update();
            Check(scope.MoveCount == 3 && scope.LastPoint?.Tolerance == 3f, "Changing target size must update the active route tolerance.");
        });

        foreach (var invalid in new[] { "missing", "dead", "untargetable", "event NPC", "self" })
        {
            run($"Follow releases its route for an invalid target: {invalid}", () =>
            {
                using var scope = new FollowScope();
                scope.Update();
                switch (invalid)
                {
                    case "missing": scope.Target = null; break;
                    case "dead": scope.TargetDead = true; break;
                    case "untargetable": scope.Targetable = false; break;
                    case "event NPC": scope.TargetKind = ObjectKind.EventNpc; break;
                    case "self": scope.Target = scope.Player; break;
                }
                scope.Update();
                scope.Update();
                Check(scope.MoveCount == 1 && scope.StopCount == 1, "An invalid target must stop only the existing follow route, once.");
            });
        }

        foreach (var gate in new[] { "disabled", "fallback disabled", "invalid slot", "combat disabled", "out of combat", "missing player", "dead player" })
        {
            run($"Follow respects activation gate: {gate}", () =>
            {
                using var scope = new FollowScope();
                switch (gate)
                {
                    case "disabled": scope.Settings.EnableFollow = false; break;
                    case "fallback disabled": scope.Settings.FollowTargetWhenNoSlot = false; break;
                    case "invalid slot": scope.Settings.FollowSlot = 8; break;
                    case "combat disabled": scope.Settings.FollowDuringCombat = false; break;
                    case "out of combat": scope.InCombat = false; break;
                    case "missing player": scope.HasPlayer = false; break;
                    case "dead player": scope.PlayerDead = true; break;
                }
                scope.Update();
                Check(scope.MoveCount == 0 && scope.StopCount == 0, "A blocked follow must neither issue nor stop a route it never owned.");
            });
        }

        run("Follow permits explicitly enabled out-of-combat movement", () =>
        {
            using var scope = new FollowScope { InCombat = false };
            scope.Settings.FollowOutOfCombat = true;
            scope.Update();
            Check(scope.MoveCount == 1, "Out-of-combat following must honor its own setting.");
        });

        run("Follow falls back to the current target when a party slot is unavailable", () =>
        {
            using var scope = new FollowScope();
            scope.Settings.FollowSlot = 1;
            scope.Update();
            Check(scope.MoveCount == 1, "An unavailable party member must allow the enabled current-target fallback.");
        });

        foreach (var owner in new[] { "legacy", "Reborn" })
        {
            run($"Follow yields to {owner} and resumes after control is released", () =>
            {
                using var scope = new FollowScope();
                scope.Update();
                scope.SetDodgeControl(owner, true);
                scope.Update();
                Check(scope.MoveCount == 1 && scope.StopCount == 0, "Following must not overwrite or stop a dodge route.");
                scope.SetDodgeControl(owner, false);
                scope.Update();
                Check(scope.MoveCount == 2, "Following must resume after dodge releases control.");
                scope.SetDodgeControl(owner, true);
                scope.Settings.EnableFollow = false;
                scope.Update();
                scope.Controller.Dispose();
                Check(scope.StopCount == 0, "Disabling or unloading follow must preserve a route taken over by dodge.");
            });
        }

        run("Auto-dodge following publishes one dynamic planner goal and never sends a raw return route", () =>
        {
            using var scope = new FollowScope();
            scope.Settings.EnableAutoDodge = true;
            scope.Update();
            var first = scope.Manager.BuildSnapshot(DateTime.UtcNow);
            Check(first.GoalAreas.Count == 1 && scope.MoveCount == 0, "auto-follow bypassed planner");
            scope.TargetPosition = new(12, 0, 0);
            scope.SetDodgeControl("Reborn", true); scope.Update();
            scope.SetDodgeControl("Reborn", false); scope.Update();
            var moved = scope.Manager.BuildSnapshot(DateTime.UtcNow);
            Check(moved.Version == first.Version && moved.GoalAreas[0].Geometry.Position == scope.TargetPosition,
                "moving target invalidated every in-flight solve instead of using dynamic binding");
            Check(scope.MoveCount == 0 && scope.StopCount == 0, "settled dodge handed a dangerous raw return to follow");
        });

        run("Auto-follow respects mechanic destinations and restores its goal after they clear", () =>
        {
            using var scope = new FollowScope();
            scope.Settings.EnableAutoDodge = true; scope.Update();
            var now = DateTime.UtcNow;
            var mechanic = scope.Manager.AddGoalArea(new CircleTemplate(0.2f), new StaticTransform(new(-8,0,0),0),
                [new GoalWindow(now, null, 10, PositionPreference.Center)]);
            scope.Update();
            Check(scope.Manager.BuildSnapshot(now).GoalAreas.Single().Handle == mechanic, "attack goal competed with bait destination");
            scope.Manager.RemoveArea(mechanic); scope.Update();
            Check(scope.Manager.BuildSnapshot(now).GoalAreas.Count == 1 && scope.MoveCount == 0, "follow goal not restored through planner");
            scope.Settings.EnableFollow = false; scope.Update();
            Check(scope.Manager.BuildSnapshot(now).GoalAreas.Count == 0, "disable leaked follow goal");
        });

        run("Auto-follow excludes pending damage and restores the target only after the hit window", () =>
        {
            using var scope = new FollowScope { PlayerPosition = new(-13,0,0), TargetPosition = Vector3.Zero };
            scope.Settings.EnableAutoDodge = true;
            var now = DateTime.UtcNow;
            scope.Now = now;
            var danger = new CircleGeometry(Vector3.Zero, 9);
            scope.Manager.AddDangerArea(new CircleTemplate(9), new StaticTransform(Vector3.Zero,0),
                [new DangerWindow(now.AddSeconds(1), now.AddSeconds(2))]);
            scope.Update();
            var snapshot = scope.Manager.BuildSnapshot(now);
            Check(!snapshot.GoalAreas.Single().GoalWindows.Any(w => w.IsActiveAt(now)), "fully covered follow goal still influenced the solver heuristic");
            Check(!snapshot.GoalAreas.Single().Geometry.Contains(scope.TargetPosition), "pending hit remained an attack preference");
            var before = Solve(scope, snapshot, now);
            Check(before.IsSuccess && before.ExecutablePath.All(p => !danger.Contains(p.Position)), "follow returned to pending damage");
            scope.Now = now.AddSeconds(1.9);
            scope.Update();
            var held = Solve(scope, scope.Manager.BuildSnapshot(scope.Now.Value), scope.Now.Value);
            Check(held.IsSuccess && held.ExecutablePath.All(p => !danger.Contains(p.Position)), "follow returned before the window ended");
            scope.Now = now.AddSeconds(2.4);
            scope.Update();
            var released = scope.Manager.BuildSnapshot(scope.Now.Value);
            var after = Solve(scope, released, scope.Now.Value);
            Check(after.IsSuccess && after.ExecutablePath.Count >= 2 && after.TargetPosition is { } target
                && released.GoalAreas.Single().Geometry.Contains(target), "follow did not resume after damage");
            Check(released.Version == snapshot.Version + 1 && released.GoalAreas.Single().Handle == snapshot.GoalAreas.Single().Handle,
                "goal activation must invalidate once without replacing the dynamic handle");
            Check(!snapshot.GoalAreas.Single().Geometry.Contains(scope.TargetPosition), "old worker snapshot was mutated");
            Check(scope.MoveCount == 0, "direct MoveTo bypassed time windows");
        });

        run("Auto-follow keeps safe attack positions while a moving future splash excludes the rest", () =>
        {
            using var scope = new FollowScope { PlayerPosition = new(-12,0,0), TargetPosition = Vector3.Zero };
            scope.Settings.EnableAutoDodge = true;
            var now = DateTime.UtcNow;
            scope.Now = now;
            var danger = new CircleGeometry(new(3,0,0),5);
            var handle = scope.Manager.AddDangerArea(new CircleTemplate(5), new StaticTransform(danger.Position,0),
                [new DangerWindow(now.AddSeconds(4),now.AddSeconds(5))]);
            scope.Update();
            var snapshot = scope.Manager.BuildSnapshot(now);
            var solution = Solve(scope,snapshot,now);
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2 && solution.TargetPosition is { } safe
                && snapshot.GoalAreas.Single().Geometry.Contains(safe) && !danger.Contains(safe), "future splash pulled attack preference into damage");
            Check(solution.ExecutablePath.All(p => !danger.Contains(p.Position)), "following crossed a future splash despite a safe approach");
            scope.Manager.UpdateArea(handle, binding: new StaticTransform(new(-3,0,0),0), incrementVersion: false);
            scope.TargetPosition = new(0,0,1);
            scope.Update();
            var moved = scope.Manager.BuildSnapshot(now);
            Check(moved.Version == snapshot.Version && moved.GoalAreas.Single().Geometry.Position == scope.TargetPosition,
                "dynamic target/mask updates changed the registry version");
            Check(!moved.GoalAreas.Single().Geometry.Contains(new(-3,0,0)) && moved.GoalAreas.Single().Geometry.Contains(new(4,0,1)),
                "moving splash mask did not follow its current position");
            Check(snapshot.GoalAreas.Single().Geometry.Contains(new(-3,0,0)), "new mask mutated the previous snapshot");
        });

        run("Auto-follow abandons an attack ring with less than a quarter safe and recovers after cancellation", () =>
        {
            using var scope = new FollowScope { PlayerPosition = new(-13,0,0), TargetPosition = Vector3.Zero };
            scope.Settings.EnableAutoDodge = true;
            var now = DateTime.UtcNow; scope.Now = now;
            var danger = scope.Manager.AddDangerArea(new CircleTemplate(5.5f), new StaticTransform(Vector3.Zero,0),
                [new DangerWindow(now.AddSeconds(3),now.AddSeconds(4))]);
            scope.Update();
            var snapshot = scope.Manager.BuildSnapshot(now);
            Check(!snapshot.GoalAreas.Any(g => g.Geometry.Contains(new(5.8f,0,0))), "tiny surviving attack strip still attracted following");
            var solution = Solve(scope, snapshot, now);
            Check(solution.IsSuccess && solution.ExecutablePath.All(p => Vector3.Distance(p.Position,scope.PlayerPosition) <= 0.5f),
                $"safe player chased the tiny attack strip: {solution.Status}, target={solution.TargetPosition}, path={string.Join(';',solution.ExecutablePath.Select(p => p.Position))}");
            scope.Manager.RemoveArea(danger); scope.Update();
            var restored = Solve(scope, scope.Manager.BuildSnapshot(now), now);
            Check(restored.IsSuccess && restored.ExecutablePath.Count >= 2, "cancelled hit did not restore safe following");
        });

        run("Auto-follow preserves clearance on partial attack circles", () =>
        {
            using var scope = new FollowScope { PlayerPosition = new(-12,0,0), TargetPosition = Vector3.Zero };
            scope.Settings.EnableAutoDodge = true;
            var now = DateTime.UtcNow; scope.Now = now;
            scope.Manager.AddDangerArea(new CircleTemplate(3), new StaticTransform(new(3,0,0),0),
                [new DangerWindow(now.AddSeconds(4),now.AddSeconds(5))]);
            scope.Update();
            var goal = scope.Manager.BuildSnapshot(now).GoalAreas.Single().Geometry;
            Check(!goal.Contains(new(-0.25f,0,0)), "attack goal consumed the host hard safety margin");
            Check(goal.Contains(new(-3,0,0)), "ample safe attack area was unnecessarily discarded");
        });

        foreach (var casting in new[] { false, true })
            run($"Auto-follow suspends uptime for {(casting ? "casting" : "urgent escape")}", () =>
            {
                using var scope = new FollowScope { PlayerCasting = casting };
                scope.Settings.EnableAutoDodge = true;
                var now = DateTime.UtcNow; scope.Now = now;
                var danger = scope.Manager.AddDangerArea(new CircleTemplate(2), new StaticTransform(Vector3.Zero,0),
                    [new DangerWindow(now.AddSeconds(casting ? 4 : 0.8),now.AddSeconds(5))]);
                scope.Update();
                var snapshot = scope.Manager.BuildSnapshot(now);
                Check(snapshot.GoalAreas.Count == 0, "uptime competed with casting or urgent escape");
                if (!casting)
                {
                    var solution = Solve(scope,snapshot,now);
                    Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, "suppressing uptime also suppressed the escape");
                }
                scope.Manager.RemoveArea(danger); scope.PlayerCasting = false; scope.Update();
                Check(scope.Manager.BuildSnapshot(now).GoalAreas.Count == 1, "following did not resume after the constraint cleared");
            });

        run("Auto-follow routes around a persistent hazard instead of crossing the direct line", () =>
        {
            using var scope = new FollowScope { TargetKind = ObjectKind.Pc };
            scope.Settings.EnableAutoDodge = true;
            var now = DateTime.UtcNow;
            var danger = new CircleGeometry(new(5,0,0),2);
            scope.Manager.AddDangerArea(new CircleTemplate(2), new StaticTransform(danger.Position,0), [DangerWindow.Persistent(now)]);
            scope.Update();
            var solveAt = DateTime.UtcNow;
            var solution = Solve(scope, scope.Manager.BuildSnapshot(solveAt), solveAt);
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, "safe detour not found");
            for (var i=1; i<solution.ExecutablePath.Count; ++i)
                Check(!danger.IntersectsSegment(solution.ExecutablePath[i-1].Position, solution.ExecutablePath[i].Position), "follow path crossed active hazard");
            Check(scope.MoveCount == 0, "unsafe direct route issued");
        });

        run("Auto-follow yields only while a mechanic goal window is active", () =>
        {
            using var scope = new FollowScope();
            scope.Settings.EnableAutoDodge = true;
            var now = DateTime.UtcNow;
            var template = new CircleTemplate(0.2f);
            var binding = new StaticTransform(new(-8,0,0),0);
            var future = scope.Manager.AddGoalArea(template, binding, [new GoalWindow(now.AddMinutes(1), null, 10, PositionPreference.Center)]);
            scope.Update();
            Check(scope.Manager.BuildSnapshot(DateTime.UtcNow).GoalAreas.Count == 2, "future mechanic blocked current following");
            scope.Manager.RemoveArea(future);
            var active = scope.Manager.AddGoalArea(template, binding, [new GoalWindow(now, null, 10, PositionPreference.Center)]);
            scope.Update();
            Check(scope.Manager.BuildSnapshot(DateTime.UtcNow).GoalAreas.Single().Handle == active, "active mechanic did not take precedence");
        });

        run("Auto-follow cleanup removes only its own goal and never stops a solver route", () =>
        {
            using var scope = new FollowScope();
            scope.Settings.EnableAutoDodge = true; scope.Update();
            var now = DateTime.UtcNow;
            var foreign = scope.Manager.AddGoalArea(new CircleTemplate(2), new StaticTransform(new(-5,0,0),0), [new GoalWindow(now,null,0,PositionPreference.Nearest)]);
            scope.SetDodgeControl("Reborn",true);
            scope.Controller.Dispose(); scope.Controller.Dispose();
            Check(scope.Manager.BuildSnapshot(now).GoalAreas.Single().Handle == foreign && scope.StopCount == 0, "cleanup took foreign goal or route");
        });

        run("Follow disposal stops its own route only once", () =>
        {
            using var scope = new FollowScope();
            scope.Update();
            scope.Controller.Dispose();
            scope.Controller.Dispose();
            scope.Update();
            Check(scope.MoveCount == 1 && scope.StopCount == 1, "Disposal must be idempotent and prevent subsequent updates.");
        });
    }

    private static SafeZoneSolution Solve(FollowScope scope, SafeZoneSnapshot snapshot, DateTime now)
        => new SafeZoneSolver().Solve(snapshot,
            new PlayerState(scope.PlayerPosition,0,6,false,false,0,true,snapshot.Version),
            new MovementCapability(6,6,0,false,6,6), new SolveOptions(), now);

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FollowScope : IDisposable
    {
        private readonly List<(PropertyInfo Property, object? Value)> _savedProperties = [];
        private readonly Dictionary<FieldInfo, object?> _savedSettings;
        private readonly List<string> _errors = [];
        private readonly object _legacy;
        private readonly object _reborn;
        public MasterPlayerSettings Settings { get; }
        public FollowController Controller { get; }
        public SafeZoneManager Manager { get; }
        private readonly SafeZoneSetting _plannerSetting = new() { EnableSafeZone = true, EnableAutoMovement = true };
        public IPlayerCharacter Player { get; }
        public IGameObject? Target { get; set; }
        public Vector3 PlayerPosition { get; set; }
        public Vector3 TargetPosition { get; set; } = new(10f, 0f, 0f);
        public ObjectKind TargetKind { get; set; } = ObjectKind.BattleNpc;
        public float TargetRadius { get; set; } = 4f;
        public bool TargetDead { get; set; }
        public bool Targetable { get; set; } = true;
        public bool PlayerDead { get; set; }
        public bool PlayerCasting { get; set; }
        public bool HasPlayer { get; set; } = true;
        public bool InCombat { get; set; } = true;
        public DateTime? Now { get; set; }
        public bool IsRunning { get; set; }
        public int MoveCount { get; private set; }
        public int StopCount { get; private set; }
        public GreenMovePoint? LastPoint { get; private set; }

        public FollowScope()
        {
            var configDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "MasterPlayer-FollowRegression-" + Guid.NewGuid().ToString("N")));
            Replace(typeof(Svc), nameof(Svc.PluginInterface), ScriptServiceProxy.Create<IDalamudPluginInterface>((name, _) =>
                name == "get_ConfigDirectory" ? configDirectory : throw new NotSupportedException(name)));
            Replace(typeof(Svc), nameof(Svc.Log), ScriptServiceProxy.Create<IPluginLog>((name, args) =>
            {
                _errors.Add(name + ": " + string.Join(" ", args.Select(a => a?.ToString())));
                return null;
            }));
            Settings = MasterPlayerSettings.Instance;
            _savedSettings = typeof(MasterPlayerSettings).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(field => field, field => field.GetValue(Settings));
            Settings.EnableFollow = true;
            Settings.EnableAutoDodge = false;
            Settings.FollowSlot = 0;
            Settings.FollowTargetWhenNoSlot = true;
            Settings.FollowDistance = 1.5f;
            Settings.FollowDuringCombat = true;
            Settings.FollowOutOfCombat = false;
            Settings.DebugMode = false;

            Player = ScriptServiceProxy.Create<IPlayerCharacter>((name, _) => name switch
            {
                "get_EntityId" => 1u,
                "get_ObjectKind" => ObjectKind.Pc,
                "get_IsTargetable" => true,
                "get_Position" => PlayerPosition,
                "get_HitboxRadius" => 0.5f,
                "get_IsDead" => PlayerDead,
                "get_IsCasting" => PlayerCasting,
                _ => throw new NotSupportedException(name),
            });
            Target = ScriptServiceProxy.Create<IBattleChara>((name, _) => name switch
            {
                "get_EntityId" => 2u,
                "get_ObjectKind" => TargetKind,
                "get_Position" => TargetPosition,
                "get_HitboxRadius" => TargetRadius,
                "get_IsDead" => TargetDead,
                "get_IsTargetable" => Targetable,
                "get_Name" => null,
                _ => throw new NotSupportedException(name),
            });
            Replace(typeof(Svc), nameof(Svc.Objects), ScriptServiceProxy.Create<IObjectTable>((name, args) => name switch
            {
                "get_LocalPlayer" => HasPlayer ? Player : null,
                "get_Length" => 2,
                "get_Item" => (int)args[0]! == 0 ? Player : Target,
                _ => throw new NotSupportedException(name)
            }));
            Replace(typeof(Svc), nameof(Svc.Targets), ScriptServiceProxy.Create<ITargetManager>((name, _) =>
                name == "get_Target" ? Target : throw new NotSupportedException(name)));
            Replace(typeof(Svc), nameof(Svc.Condition), ScriptServiceProxy.Create<ICondition>((name, args) =>
                name == "get_Item" ? (ConditionFlag)args[0]! == ConditionFlag.InCombat && InCombat : throw new NotSupportedException(name)));

            var assembly = typeof(PromeRotation.Plugin).Assembly;
            var plugin = RuntimeHelpers.GetUninitializedObject(typeof(PromeRotation.Plugin));
            _legacy = RuntimeHelpers.GetUninitializedObject(assembly.GetType("PromeRotation.Spatial.SafeZone.SafeZoneMovementSystem")!);
            _reborn = RuntimeHelpers.GetUninitializedObject(assembly.GetType("PromeRotation.Spatial.SafeZoneReborn.SafeZoneRebornMovementSystem")!);
            SetField(plugin, "_safeZoneMovementSystem", _legacy);
            SetField(plugin, "_safeZoneRebornMovementSystem", _reborn);
            Manager = new SafeZoneManager(_plannerSetting);
            SetField(plugin, "_safeZoneRebornSetting", _plannerSetting);
            SetField(plugin, "_safeZoneRebornManager", Manager);
            Replace(typeof(PromeRotation.Plugin), nameof(PromeRotation.Plugin.Instance), plugin);
            var movement = ScriptServiceProxy.Create<IGreenMoveSystem>((name, args) =>
            {
                switch (name)
                {
                    case "get_IsRunning": return IsRunning;
                    case "MoveTo": LastPoint = (GreenMovePoint)args[0]!; MoveCount++; IsRunning = true; return null;
                    case "Stop": StopCount++; IsRunning = false; return null;
                    default: throw new NotSupportedException(name);
                }
            });
            Controller = new FollowController(movement, () => Now ?? DateTime.UtcNow);
        }

        public void Update()
        {
            Controller.Update();
            Check(_errors.Count == 0, string.Join(Environment.NewLine, _errors));
        }

        public void SetDodgeControl(string owner, bool hasControl)
            => SetField(owner == "legacy" ? _legacy : _reborn, "_hasControl", hasControl);

        private static void SetField(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

        private void Replace(Type type, string name, object value)
        {
            var property = type.GetProperty(name)!;
            _savedProperties.Add((property, property.GetValue(null)));
            property.SetValue(null, value);
        }

        public void Dispose()
        {
            Controller.Dispose();
            Manager.Dispose();
            foreach (var (field, value) in _savedSettings)
                field.SetValue(Settings, value);
            foreach (var (property, value) in _savedProperties.AsEnumerable().Reverse())
                property.SetValue(null, value);
        }
    }
}
