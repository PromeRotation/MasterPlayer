using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using MasterPlayer;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.GreenMoveSystem;

internal static class PlayerPerspectiveRegression
{
    public static unsafe void Run(Action<string, Action> run)
    {
        run("ARR perspective preserves the Blunderville Director clock and clears personal carry history", () =>
        {
            var race = new MasterPlayer.Mechanics.Blunderville.BlundervilleRaceState();
            var now = DateTime.UtcNow;
            race.Director(MasterPlayer.Mechanics.Blunderville.BlundervilleRaceState.DirectorInstance, 0x80000001, 0x4E30, 0, 0, 0, now);
            var course = race.Course;
            var started = race.StartedAt;
            Check(race.IsRacing && race.StartedByDirector, "Missing initial Director clock.");
            race.SynchronizeStatuses([new(3704, 35, now.AddSeconds(30))], now);
            race.ResetPerspective();
            Check(race.Course == course && race.IsRacing && race.StartedByDirector && race.StartedAt == started,
                "Switching perspective reset the shared arena clock.");
            Check(race.CarryCrystalParam == 0 && race.LastCrystalParam == 0 && !race.FailedDelivery,
                "Switching perspective retained another actor's crystal history.");
            race.Director(MasterPlayer.Mechanics.Blunderville.BlundervilleRaceState.DirectorInstance, 7, 0, 0, 0, 0, now);
            race.ResetPerspective();
            Check(race.IsRacing && !race.Qualified, "Another actor's qualification ended the viewed actor's race.");
            race.Director(MasterPlayer.Mechanics.Blunderville.BlundervilleRaceState.DirectorInstance, 6, 0, 0, 0, 0, now);
            race.ResetPerspective();
            Check(!race.IsRacing, "Switching perspective restarted an ended round.");
        });
        run("ARR perspective follows A/B/A, rejects missing objects and restores the local player", () =>
        {
            using var scope = new Scope();
            scope.Target->TargetObjectId = 2;
            Check(PlayerPerspective.GetId() == 1, "A normal hard target changed the local perspective.");
            scope.Playback = true;
            Check(PlayerPerspective.GetId() == 2 && PlayerPerspective.LivePlayer!.Position.X == 12, "ARR did not select B.");
            scope.Target->TargetObjectId = 1;
            Check(PlayerPerspective.GetId() == 1, "ARR did not return to A.");
            foreach (var id in new uint[] { 0, 0xE0000000, 99 })
            {
                scope.Target->TargetObjectId = id;
                Check(PlayerPerspective.GetId() == 0 && PlayerPerspective.LivePlayer == null, "Invalid ARR object fell back to the local player.");
            }
            scope.Playback = false;
            Check(PlayerPerspective.GetId() == 1 && !PlayerPerspective.IsReplay, "Leaving ARR did not restore the local player.");
            Check(Marshal.OffsetOf<TargetSystem>(nameof(TargetSystem.TargetObjectId)).ToInt32() == 0x140,
                "Review the installed CN struct against Kodakku's perspective field.");
        });
        run("ARR perspective keeps offline world isolation even when its player is missing", () =>
        {
            using var scope = new Scope { Playback = true };
            scope.Target->TargetObjectId = 2;
            var world = new ReplayWorldState();
            world.Apply(new() { Full = true, LocalPlayerId = 42, Actors = [new() { Id = 42, Position = new(7, 0, 9) }] });
            scope.RejectLiveReads = true;
            Check(PlayerPerspective.GetId(world) == 42 && PlayerPerspective.Capture(world, DateTime.UtcNow)?.Position.X == 7,
                "Offline replay read ARR or the live object table.");
            world.Apply(new() { LocalPlayerId = 42, Removed = [42] });
            Check(PlayerPerspective.GetId(world) == 0 && PlayerPerspective.Capture(world, DateTime.UtcNow) == null,
                "Missing recorded player fell back to a live player.");
        });
        run("ARR perspective still solves routes but never dispatches movement", () =>
        {
            using var scope = new Scope { Playback = true };
            scope.Target->TargetObjectId = 1;
            var calls = 0;
            var movement = ScriptServiceProxy.Create<IGreenMoveSystem>((name, _) => name switch
            {
                "get_IsRunning" => false,
                "GetStatus" => default(GreenMoveStatus),
                _ => throw new InvalidOperationException($"ARR issued native movement: {name} ({++calls})")
            });
            using var plugin = new MasterPlayerPlugin(movement);
            var runtime = plugin.SafeZone;
            runtime.Setting.EnableSafeZone = runtime.Setting.EnableAutoMovement = true;
            runtime.Registry.SetArenaBoundary(new MasterPlayer.Spatial.Geometry.CircleGeometry(new(10, 0, 0), 30));
            runtime.Registry.AddPersistentSafeArea(new CircleTemplate(1), new StaticTransform(new(15, 0, 0), 0));
            for (var i = 0; i < 100 && runtime.MovementStatus.LatestSolution == null; ++i)
            { runtime.Update(1, true); Thread.Sleep(10); }
            Check(runtime.MovementStatus.LatestSolution?.IsSuccess == true && calls == 0, "ARR did not produce a preview-only solution.");
            scope.Target->TargetObjectId = 0;
            runtime.Update(1, true);
            Check(runtime.MovementStatus.LatestSolution == null, "Missing perspective retained the previous route.");
            scope.Target->TargetObjectId = 2;
            runtime.Update(1, true);
            Check(!runtime.MovementStatus.HasControl && calls == 0, "Switching perspective acquired control.");
        });
    }

    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }

    private sealed unsafe class Scope : IDisposable
    {
        private readonly IObjectTable _objects = Svc.Objects;
        private readonly ICondition _condition = Svc.Condition;
        private readonly nint _targetAddress = TargetSystem.Addresses.Instance.Value;
        public TargetSystem* Target { get; } = (TargetSystem*)NativeMemory.AllocZeroed((nuint)sizeof(TargetSystem));
        public bool Playback, RejectLiveReads;
        public Scope()
        {
            var a = Player(1, 0); var b = Player(2, 12);
            TargetSystem.Addresses.Instance.Value = (nint)Target;
            Set(nameof(Svc.Condition), ScriptServiceProxy.Create<ICondition>((name, args) =>
                name == "get_Item" && (ConditionFlag)args[0]! == ConditionFlag.DutyRecorderPlayback && Playback));
            Set(nameof(Svc.Objects), ScriptServiceProxy.Create<IObjectTable>((name, args) =>
            {
                if (RejectLiveReads) throw new InvalidOperationException("Offline touched live objects.");
                return name switch
                {
                    "get_LocalPlayer" => a,
                    "SearchByEntityId" => (uint)args[0]! switch { 1 => a, 2 => b, _ => null },
                    _ => throw new NotSupportedException(name)
                };
            }));
        }
        private static IPlayerCharacter Player(uint id, float x) => ScriptServiceProxy.Create<IPlayerCharacter>((name, _) => name switch
        {
            "get_EntityId" => id,
            "get_Position" => new Vector3(x, 0, 0),
            "get_Rotation" or "get_CurrentCastTime" or "get_TotalCastTime" => 0f,
            "get_IsDead" or "get_IsCasting" => false,
            _ => throw new NotSupportedException(name)
        });
        private static void Set(string name, object? value) => typeof(Svc).GetProperty(name)!.SetValue(null, value);
        public void Dispose()
        {
            Set(nameof(Svc.Objects), _objects); Set(nameof(Svc.Condition), _condition);
            TargetSystem.Addresses.Instance.Value = _targetAddress;
            NativeMemory.Free(Target);
        }
    }
}
