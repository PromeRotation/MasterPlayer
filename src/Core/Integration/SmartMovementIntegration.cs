using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using MasterPlayer.Settings;

namespace MasterPlayer.Integration;

internal sealed class SmartMovementIntegration : IDisposable
{
    private readonly ConcurrentQueue<bool> requests = new();
    private readonly List<Action> unregister = [];
    private readonly ICallGateProvider<bool, object> enabledChanged;
    private readonly ICallGateProvider<string, object> stateChanged;
    private IDtrBarEntry? dtr;
    private volatile string state = "{}";
    private volatile bool ready;
    private volatile bool enabledState;
    private bool? previousEnabled;
    private bool disposed;

    public SmartMovementIntegration()
    {
        try
        {
            Register("PromeRadar.GetApiVersion", () => 1);
            Register("PromeRadar.IsReady", () => ready);
            Register("PromeRadar.GetSmartMovementEnabled", () => enabledState);
            Register("PromeRadar.GetState", () => state);
            var setter = Svc.PluginInterface.GetIpcProvider<bool, object>("PromeRadar.SetSmartMovementEnabled");
            setter.RegisterAction(enabled => { if (ready) requests.Enqueue(enabled); });
            unregister.Add(setter.UnregisterAction);
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
        while (requests.TryDequeue(out var enabled))
            plugin.SetSmartMovementEnabled(enabled);
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
        var replay = plugin.Replay?.SessionActive == true;
        var player = Svc.Objects.LocalPlayer;
        var nextState = JsonSerializer.Serialize(new
        {
            ApiVersion = 1,
            Ready = true,
            SmartMovementEnabled = enabled,
            CanMove = enabled && player is { IsDead: false } && !replay
                && plugin.SafeZone.Setting.EffectiveEnableSafeZone
                && plugin.SafeZone.Setting.EffectiveEnableAutoMovement
                && !Spatial.HostMovement.AutomaticMovementEnabled && !Spatial.HostMovement.HasControl,
            HasMovementControl = plugin.SafeZone.MovementStatus.HasControl,
            MovementPhase = plugin.SafeZone.MovementStatus.PhaseText,
            ReplayActive = replay,
            TerritoryId = Svc.ClientState.TerritoryType,
            MechanicsEnabled = MasterPlayerSettings.Instance.EnableMechanics,
            RunningScriptCount = plugin.Scripts?.Running.Count ?? 0,
            ActiveModule = plugin.MechanicHost?.HasActiveModule == true ? plugin.MechanicHost.ActiveModuleName : null,
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
        ready = false;
        var finalState = JsonNode.Parse(state)?.AsObject() ?? new JsonObject();
        finalState["ApiVersion"] = 1;
        finalState["Ready"] = false;
        finalState["CanMove"] = false;
        finalState["HasMovementControl"] = false;
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
            dtr.Dispose();
            dtr = null;
        }
    }
}
