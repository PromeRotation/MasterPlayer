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
        public IPlayerCharacter Player { get; }
        public IGameObject? Target { get; set; }
        public Vector3 PlayerPosition { get; set; }
        public Vector3 TargetPosition { get; set; } = new(10f, 0f, 0f);
        public ObjectKind TargetKind { get; set; } = ObjectKind.BattleNpc;
        public float TargetRadius { get; set; } = 4f;
        public bool TargetDead { get; set; }
        public bool Targetable { get; set; } = true;
        public bool PlayerDead { get; set; }
        public bool HasPlayer { get; set; } = true;
        public bool InCombat { get; set; } = true;
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
            Replace(typeof(Svc), nameof(Svc.Objects), ScriptServiceProxy.Create<IObjectTable>((name, _) =>
                name == "get_LocalPlayer" ? (HasPlayer ? Player : null) : throw new NotSupportedException(name)));
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
            Controller = new FollowController(movement);
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
            foreach (var (field, value) in _savedSettings)
                field.SetValue(Settings, value);
            foreach (var (property, value) in _savedProperties.AsEnumerable().Reverse())
                property.SetValue(null, value);
        }
    }
}
