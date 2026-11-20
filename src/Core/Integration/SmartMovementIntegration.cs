using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using MasterPlayer.Settings;
using MasterPlayer.Spatial;

namespace MasterPlayer.Integration;

internal sealed class SmartMovementIntegration : IDisposable
{
    private readonly ConcurrentQueue<bool> requests = new();
    private readonly ConcurrentQueue<bool> fsdRequests = new();
    private readonly List<Action> unregister = [];
    private readonly ICallGateProvider<bool, object> enabledChanged;
    private readonly ICallGateProvider<string, object> stateChanged;
    private IDtrBarEntry? dtr;
    private volatile string state = "{}";
    private volatile bool ready;
    private volatile bool enabledState;
    private bool? previousEnabled;
    private bool disposed;
    private bool fsdActive;
    private bool fsdOwnsSwitch;
    private bool fsdOwnsPt30Script;
    private DateTime nextPublish;
    private readonly SafeZoneRuntime runtime;
    private readonly ICallGateSubscriber<bool, bool> movementControl;
    private DateTime nextHandoffErrorAt;

    public SmartMovementIntegration(SafeZoneRuntime runtime)
    {
        this.runtime = runtime;
        movementControl = Svc.PluginInterface.GetIpcSubscriber<bool, bool>("NewFolder3.Movement.SetPromeRadarControl");
        try
        {
            Register("PromeRadar.GetApiVersion", () => 1);
            Register("PromeRadar.IsReady", () => ready);
            Register("PromeRadar.GetSmartMovementEnabled", () => enabledState);
            Register("PromeRadar.GetState", () => state);
            Register("PromeRadar.IsMechanicMovementActive", () => ready && runtime.MovementStatus.MechanicMovementActive);
            // 位置在接下来 seconds 秒内是否安全（不在将要判定的危险区、硬障碍内，且在场地边界内）；
            // 供 ACR 判断能否用突进贴回 Boss。仅主线程（PR 决策在 Framework.Update 中调用）。
            var positionSafe = Svc.PluginInterface.GetIpcProvider<float, float, float, bool>("PromeRadar.IsPositionSafe");
            positionSafe.RegisterFunc(IsPositionSafe);
            unregister.Add(positionSafe.UnregisterFunc);
            var setter = Svc.PluginInterface.GetIpcProvider<bool, object>("PromeRadar.SetSmartMovementEnabled");
            setter.RegisterAction(enabled => { if (ready) requests.Enqueue(enabled); });
            unregister.Add(setter.UnregisterAction);
            var fsdSetter = Svc.PluginInterface.GetIpcProvider<bool, object>("PromeRadar.SetFsdSessionActive");
            fsdSetter.RegisterAction(active => { if (ready) fsdRequests.Enqueue(active); });
            unregister.Add(fsdSetter.UnregisterAction);
            enabledChanged = Svc.PluginInterface.GetIpcProvider<bool, object>("PromeRadar.SmartMovementChanged");
            stateChanged = Svc.PluginInterface.GetIpcProvider<string, object>("PromeRadar.StateChanged");
            try
            {
                dtr = Svc.DtrBar.Get("PromeRadar");
                dtr.OnClick = OnClick;
            }
            catch (Exception ex)
            {
                Svc.Log.Warning(ex, "[PromeRadar] DTR 初始化失败");
            }
            runtime.SetMovementHandoff(RequestMovementControl);
        }
        catch
        {
            foreach (var remove in unregister)
            {
                try { remove(); }
                catch (Exception ex) { Svc.Log.Warning(ex, "[PromeRadar] 初始化失败后的 IPC 清理失败"); }
            }
            throw;
        }
    }

    private bool RequestMovementControl(bool active)
    {
        try
        {
            if (movementControl.HasFunction) return movementControl.InvokeFunc(active);
            if (!fsdActive || !active) return true;
            throw new InvalidOperationException("NewFolder3 缺少同步移动交接接口；需同时更新两个插件。");
        }
        catch (Exception ex)
        {
            if (DateTime.UtcNow >= nextHandoffErrorAt)
            {
                nextHandoffErrorAt = DateTime.UtcNow.AddSeconds(5);
                Svc.Log.Error(ex, "[PromeRadar] FSD 移动交接失败，未下发争抢路线");
            }
            return false;
        }
    }

    private static bool IsPositionSafe(float x, float z, float seconds)
    {
        if (MasterPlayerPlugin.Instance is not { } plugin) return false;
        var now = DateTime.UtcNow;
        var snapshot = plugin.SafeZone.Manager.BuildSnapshot(now);
        var point = new System.Numerics.Vector3(x, Svc.Objects.LocalPlayer?.Position.Y ?? 0, z);
        return Follow.PositionSafety.IsSafe(snapshot, point, now, now.AddSeconds(Math.Max(0, seconds)));
    }

    private void Register<T>(string name, Func<T> query)
    {
        var provider = Svc.PluginInterface.GetIpcProvider<T>(name);
        provider.RegisterFunc(query);
        unregister.Add(provider.UnregisterFunc);
    }

    private void OnClick(DtrInteractionEvent interaction)
    {
        if (ready && interaction.ClickType == MouseClickType.Left)
            requests.Enqueue(!MasterPlayerSettings.Instance.EnableSmartMovement);
    }

    public void ApplyRequests(MasterPlayerPlugin plugin)
    {
        while (fsdRequests.TryDequeue(out var active))
        {
            if (active == fsdActive) continue;
            fsdActive = active;
            if (active)
            {
                fsdOwnsSwitch = !MasterPlayerSettings.Instance.EnableSmartMovement;
                fsdOwnsPt30Script = plugin.Scripts?.IsScriptEnabled("promeradar.pt30") != true;
                plugin.Scripts?.SetScriptEnabled("promeradar.pt30", true);
                plugin.SetSmartMovementEnabled(true);
            }
            else
            {
                if (fsdOwnsSwitch) plugin.SetSmartMovementEnabled(false);
                if (fsdOwnsPt30Script) plugin.Scripts?.SetScriptEnabled("promeradar.pt30", false);
                fsdOwnsSwitch = false;
                fsdOwnsPt30Script = false;
            }
            Svc.Log.Information($"[PromeRadar] FSD session active={active}, ownsSwitch={fsdOwnsSwitch}");
        }
        while (requests.TryDequeue(out var enabled))
        {
            fsdOwnsSwitch = false;
            plugin.SetSmartMovementEnabled(enabled);
        }
    }

    public void Update(MasterPlayerPlugin plugin)
    {
        if (disposed) return;
        var enabled = MasterPlayerSettings.Instance.EnableSmartMovement;
        var changed = previousEnabled != enabled;
        if (changed)
        {
            previousEnabled = enabled;
            if (dtr != null)
            {
                dtr.Text = $"PromeRadar AI：{(enabled ? "开" : "关")}";
                dtr.Tooltip = $"智能走位已{(enabled ? "开启" : "关闭")}，左键切换。关闭时保留绘制与指路。";
                dtr.Shown = true;
            }
        }
        var replay = PlayerPerspective.IsReplay;
        var player = replay ? null : PlayerPerspective.LivePlayer;
        // Bound serialization and broadcasts: CapturedAtUtc otherwise makes every frame a change.
        var now = DateTime.UtcNow;
        if (!changed && now < nextPublish) return;
        nextPublish = now.AddMilliseconds(100);
        var pt30 = plugin.Scripts?.Running.Values.Select(r => r.Script)
            .OfType<Scripts.PilgrimsTraverse.PilgrimsTraverse30>().FirstOrDefault();
        var pt99 = plugin.Scripts?.Running.Values.Select(r => r.Script)
            .OfType<Scripts.PilgrimsTraverse.PilgrimsTraverse99>().FirstOrDefault();
        var solution = plugin.SafeZone.MovementStatus.LatestSolution;
        var nextState = JsonSerializer.Serialize(new
        {
            ApiVersion = 1,
            CapturedAtUtc = now,
            BuildMvid = typeof(MasterPlayerPlugin).Module.ModuleVersionId,
            MechanicRuntimeBuildMvid = typeof(MechanicRuntime.MechanicSession).Module.ModuleVersionId,
            Ready = true,
            SmartMovementEnabled = enabled,
            FsdSessionActive = fsdActive,
            Pt30ScriptAvailable = plugin.Scripts?.Scripts.Any(s => s.Metadata.Id == "promeradar.pt30") == true,
            Pt30ScriptReady = pt30?.NavigationReady == true,
            Pt30ScriptVersion = "1.0.0",
            Pt30State = pt30?.DiagnosticState,
            Pt99TargetControlActive = pt99?.TargetControlActive == true,
            Pt99TargetId = pt99?.TargetControlActive == true ? pt99.PreferredTargetId : 0,
            ScriptError = plugin.Scripts?.ErrorMessage,
            Pt30ChaseRemaining = pt30?.ChaseRemaining ?? 0,
            Pt30PreviewCount = pt30?.PreviewCount ?? 0,
            FrameMilliseconds = Svc.Framework.UpdateDelta.TotalMilliseconds,
            RecordingActive = plugin.Recorder?.IsRecording == true,
            RecordingPath = plugin.Recorder?.CurrentFilePath,
            CanMove = enabled && player is { IsDead: false } && !replay
                && plugin.SafeZone.Setting.EffectiveEnableSafeZone
                && plugin.SafeZone.Setting.EffectiveEnableAutoMovement
                && !Spatial.HostMovement.AutomaticMovementEnabled && !Spatial.HostMovement.HasControl,
            HasMovementControl = plugin.SafeZone.MovementStatus.HasControl,
            MechanicMovementActive = plugin.SafeZone.MovementStatus.MechanicMovementActive,
            MovementPhase = plugin.SafeZone.MovementStatus.PhaseText,
            SolveStatus = solution?.Status.ToString(),
            SolveTimeUtc = solution?.SolveTime,
            SolutionTemporal = solution?.UsesTemporalPlanning,
            SolutionPartial = solution?.IsPartialEscape,
            SolutionFallback = solution?.IsLeastDangerFallback,
            PathWaypointCount = solution?.ExecutablePath.Count ?? 0,
            PlannedWaitUntilUtc = solution?.ExecutablePath.Where(p => p.IsWaitPoint)
                .Select(p => (DateTime?)p.PlannedDepartureTime).FirstOrDefault(),
            EscapeDeadlineUtc = solution?.EscapeDeadline,
            EscapeWaitBlock = (plugin.SafeZone.MovementStatus as Spatial.SafeZoneReborn.SafeZoneRebornMovementSystem)?.EscapeWaitBlock,
            EscapeWaitBlockAtUtc = (plugin.SafeZone.MovementStatus as Spatial.SafeZoneReborn.SafeZoneRebornMovementSystem)?.EscapeWaitBlockAtUtc,
            plugin.SafeZone.MovementStatus.LastSolveQueueMilliseconds,
            plugin.SafeZone.MovementStatus.LastSolveWorkMilliseconds,
            plugin.SafeZone.MovementStatus.AsyncSolveCancelledCount,
            plugin.SafeZone.MovementStatus.AsyncSolveDiscardedCount,
            MechanicGoalActive = Mechanics.Crucible.CrucibleGoalSignal.Active || pt30?.GoalCount > 0,
            ReplayActive = replay,
            TerritoryId = Svc.ClientState.TerritoryType,
            MechanicsEnabled = MasterPlayerSettings.Instance.EnableMechanics,
            RunningScriptCount = plugin.Scripts?.Running.Count ?? 0,
            ActiveModule = plugin.MechanicHost?.HasActiveModule == true ? plugin.MechanicHost.ActiveModuleName : null,
            NavigationActive = pt30?.NavigationReady == true || plugin.MechanicHost?.NavigationActive == true,
            CaptureActive = plugin.MechanicHost?.CaptureActive == true,
            MechanicError = pt30?.NavigationReady == true ? null : plugin.MechanicHost?.LastError,
            DangerZoneCount = pt30?.EncounterActive == true ? pt30.DangerCount : plugin.MechanicHost?.NavDangerZoneCount ?? 0,
            ObstacleCount = pt30?.NavigationReady == true ? 1 : plugin.MechanicHost?.NavObstacleCount ?? 0,
            GoalZoneCount = pt30?.NavigationReady == true ? pt30.GoalCount : plugin.MechanicHost?.NavGoalZoneCount ?? 0,
            ChaseStatus = pt30?.EncounterActive == true ? pt30.DiagnosticState : plugin.MechanicHost?.ChaseStatus,
        });
        var stateDiffers = state != nextState;
        state = nextState;
        enabledState = enabled;
        ready = true;
        if (changed) Notify(() => enabledChanged.SendMessage(enabled));
        if (stateDiffers) Notify(() => stateChanged.SendMessage(nextState));
    }

    private static void Notify(Action send)
    {
        try { send(); }
        catch (Exception ex) { Svc.Log.Warning(ex, "[PromeRadar] IPC 状态订阅者执行失败"); }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        // Stop our executor before telling FSD that it may resume.
        runtime.SetMovementHandoff(null);
        ready = false;
        var finalState = JsonNode.Parse(state)?.AsObject() ?? new JsonObject();
        finalState["ApiVersion"] = 1;
        finalState["Ready"] = false;
        finalState["CanMove"] = false;
        finalState["HasMovementControl"] = false;
        finalState["MechanicMovementActive"] = false;
        finalState["MovementPhase"] = "已卸载";
        state = finalState.ToJsonString();
        Notify(() => stateChanged.SendMessage(state));
        foreach (var remove in unregister)
        {
            try { remove(); }
            catch (Exception ex) { Svc.Log.Warning(ex, "[PromeRadar] IPC 注销失败"); }
        }
        if (dtr != null)
        {
            dtr.OnClick = null;
            dtr.Remove();
            dtr = null;
        }
    }
}
