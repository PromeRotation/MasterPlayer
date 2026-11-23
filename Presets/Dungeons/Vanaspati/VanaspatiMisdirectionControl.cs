using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using MasterPlayer.Spatial;
using PromeRotation.GreenMoveSystem;

namespace MasterPlayer.Mechanics.Dungeons;

// Same native input entry points as PR GreenMovementInputController. Its special route
// is restricted to Blunderville; this owner supplies native script misdirection statuses.
internal sealed unsafe class VanaspatiMisdirectionControl : IDisposable
{
    private delegate void Walk(void* self, float* left, float* forward, float* turn, byte* strafe, byte* unknown, byte additive);
    private delegate byte InputActive(void* self, byte flags);
    private delegate bool InputEnabled(void* self);
    [Signature("E8 ?? ?? ?? ?? 84 C0 75 10 38 43 3C")]
    private readonly InputEnabled? inputEnabled1 = null;
    [Signature("E8 ?? ?? ?? ?? 84 C0 75 03 88 47 3F")]
    private readonly InputEnabled? inputEnabled2 = null;
    [Signature("E8 ?? ?? ?? ?? 80 7B 3E 00 48 8D 3D", DetourName = nameof(WalkDetour))]
    private Hook<Walk>? walkHook;
    [Signature("E8 ?? ?? ?? ?? 84 C0 74 09 84 DB 74 1A", DetourName = nameof(InputActiveDetour))]
    private Hook<InputActive>? activeHook;
    private nint arrowAddress;
    private uint? additionalTerritory;
    private long leaseExpires;
    private long inputExpires;
    private bool? forcedInput;
    private GreenMovePoint? forcedPoint;
    private bool initialized;
    private bool disposed;

    internal void Renew(bool authorized, uint? additionalTerritory = null)
    {
        if (!authorized || disposed) { Clear(); return; }
        this.additionalTerritory = additionalTerritory;
        if (!initialized)
        {
            initialized = true;
            try
            {
                Svc.Hook.InitializeFromAttributes(this);
                if (walkHook == null || activeHook == null || inputEnabled1 == null || inputEnabled2 == null
                    || !Svc.SigScanner.TryGetStaticAddressFromSig("F3 0F 11 0D ?? ?? ?? ?? 48 85 DB", out arrowAddress))
                    throw new InvalidOperationException("未能解析误导输入入口或箭头方向。");
                walkHook.Enable();
                activeHook.Enable();
            }
            catch (Exception ex)
            {
                DisposeHooks();
                Svc.Log.Error(ex, "[PromeRadar] 误导输入控制初始化失败。");
            }
        }
        if (walkHook?.IsEnabled == true && activeHook?.IsEnabled == true) leaseExpires = Environment.TickCount64 + 250;
    }

    internal void Clear()
    {
        leaseExpires = 0; inputExpires = 0; forcedInput = null; forcedPoint = null;
        additionalTerritory = null;
    }

    private bool Allowed()
        => !disposed && Environment.TickCount64 < leaseExpires && arrowAddress != 0
            && Settings.MasterPlayerSettings.Instance.EnableSmartMovement && !PlayerPerspective.IsReplay
            && (Svc.ClientState.TerritoryType is VanaspatiCatalog.TERRITORY or YuweyawataCatalog.TERRITORY
                || additionalTerritory == Svc.ClientState.TerritoryType)
            && !Svc.Condition[ConditionFlag.BetweenAreas] && !Svc.Condition[ConditionFlag.BetweenAreas51]
            && !Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent] && !Svc.Condition[ConditionFlag.WatchingCutscene]
            && !Svc.Condition[ConditionFlag.WatchingCutscene78]
            && MasterPlayerPlugin.Instance?.SafeZone is { Setting.MovementAllowed: true, MovementStatus.HasControl: true }
            && !HostMovement.HasControl && !HostMovement.AutomaticMovementEnabled
            && PromeRotation.Plugin.Instance.GreenMoveSystem.GetStatus() is { IsRunning: true, CurrentPoint: not null }
            && PlayerPerspective.LivePlayer is { IsDead: false } player
            && player.StatusList.Any(s => VanaspatiCatalog.Misdirected(s.StatusId));

    private void WalkDetour(void* self, float* left, float* forward, float* turn, byte* strafe, byte* unknown, byte additive)
    {
        if (additive == 0) { forcedInput = null; forcedPoint = null; inputExpires = 0; }
        walkHook!.Original(self, left, forward, turn, strafe, unknown, additive);
        try
        {
            if (!Allowed()) { forcedInput = null; return; }
            // Sample additive native input before MP's replacement, preserving manual priority
            // even when the game's forced walking branch no longer samples ordinary input.
            float manualLeft = 0, manualForward = 0, manualTurn = 0;
            byte manualStrafe = 0, manualUnknown = 0;
            walkHook.Original(self, &manualLeft, &manualForward, &manualTurn, &manualStrafe, &manualUnknown, 1);
            if (manualLeft != 0 || manualForward != 0)
            {
                *left = manualLeft; *forward = manualForward;
                forcedInput = null; forcedPoint = null; return;
            }
            var player = PlayerPerspective.LivePlayer!;
            var point = PromeRotation.Plugin.Instance.GreenMoveSystem.GetStatus().CurrentPoint;
            var arrow = *(float*)arrowAddress;
            var wanted = point is { } target && VanaspatiMisdirectionPolicy.ShouldMove(player.Position, target.Position, target.Tolerance, arrow);
            forcedInput = wanted;
            forcedPoint = point;
            inputExpires = Environment.TickCount64 + 100;
            *left = 0; *forward = 0;
            if (wanted && additive == 0 && inputEnabled1!.Invoke(self) && inputEnabled2!.Invoke(self))
            {
                var reference = player.Rotation;
                if (Svc.GameConfig.UiControl.TryGetUInt("MoveMode", out var mode) && mode == 1)
                {
                    var cameraManager = CameraManager.Instance();
                    var camera = cameraManager != null ? cameraManager->GetActiveCamera() : null;
                    if (camera != null) reference = camera->DirH + MathF.PI;
                }
                var delta = point!.Value.Position - player.Position;
                var relative = MathF.Atan2(delta.X, delta.Z) - reference;
                *left = MathF.Sin(relative); *forward = MathF.Cos(relative);
            }
        }
        catch (Exception ex)
        {
            Clear();
            Svc.Log.Error(ex, "[PromeRadar] 误导输入控制已撤销。");
        }
    }

    private byte InputActiveDetour(void* self, byte flags)
    {
        var original = activeHook!.Original(self, flags);
        return Allowed() && forcedPoint is { } point && PromeRotation.Plugin.Instance.GreenMoveSystem.GetStatus().CurrentPoint == point
            && forcedInput is { } wanted && Environment.TickCount64 < inputExpires ? (byte)(wanted ? 1 : 0) : original;
    }

    public void Dispose()
    {
        if (disposed) return;
        Clear(); disposed = true; DisposeHooks();
    }

    private void DisposeHooks()
    {
        activeHook?.Dispose(); activeHook = null;
        walkHook?.Dispose(); walkHook = null; arrowAddress = 0;
    }
}
