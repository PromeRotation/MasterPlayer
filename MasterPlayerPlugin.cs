using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using MasterPlayer.Settings;
using MasterPlayer.Spatial;
using PromeRotation.Hosting;
using PromeRotation.Plugins;

namespace MasterPlayer;

[PromePlugin(
    id: "masterplayer",
    name: "PromeRadar",
    author: "XSZ",
    description: "雷达悬浮窗 + 真实世界绘制 + 智能走位 + 机制库 + 录制回放",
    version: "0.1.0.0")]
public sealed class MasterPlayerPlugin : IPromePlugin
{
    private const string PanelCommand = "/prpr";
    private const string LegacyPanelCommand = "/prmp";
    private Radar.RadarRenderer? _radar;
    private Dodge.DodgeController? _dodge;
    private Mechanics.MechanicDispatcher? _mechanics;
    private MechanicHost.MechanicHostController? _mechanicHost;
    private Scripting.ScriptManager? _scripts;
    private Replay.ReplayRecorder? _replayRecorder;
    private Replay.ReplayController? _replayController;
    private Radar.RadarOverlayWindow? _radarOverlay;
    private Follow.FollowController? _follow;
    private Windows.MainWindow? _mainWindow;
    private Windows.ReplayWindow? _replayWindow;
    private SafeZoneActivation? _safeZoneActivation;
    private SafeZoneRuntime? _safeZone;
    private Integration.SmartMovementIntegration? _integration;
    private bool _smartMovementEnabled;
    private IDisposable? _safeZoneTerritory, _safeZoneDeath, _safeZoneBattleEnd;
    public SafeZoneRuntime SafeZone => _safeZone ?? throw new InvalidOperationException("MasterPlayer 安全区尚未初始化。");
    internal Radar.AoeVisibilityRegistry? AoeVisibility { get; private set; }
    private bool _disposed;

    public static MasterPlayerPlugin? Instance { get; private set; }

    internal Mechanics.MechanicDispatcher? Mechanics => _mechanics;

    internal MechanicHost.MechanicHostController? MechanicHost => _mechanicHost;

    internal Scripting.ScriptManager? Scripts => _scripts;

    internal Replay.ReplayRecorder? Recorder => _replayRecorder;

    internal Replay.ReplayController? Replay => _replayController;

    internal Windows.ReplayWindow? ReplayWindow => _replayWindow;

    public void Initialize()
    {
        Instance = this;

        var settings = MasterPlayerSettings.Instance;
        AoeVisibility = new Radar.AoeVisibilityRegistry(settings.AoeVisibility, () => SafeZone.CurrentTime);
        _safeZone = new(PromeRotation.Plugin.Instance.GreenMoveSystem,
            () => HostMovement.AutomaticMovementEnabled, () => HostMovement.HasControl);
        _safeZoneTerritory = PromeEventBus.OnTerritoryChanged(this, _ => ClearLiveSafeZone());
        _safeZoneDeath = PromeEventBus.OnPlayerDied(this, ClearLiveSafeZone);
        _safeZoneBattleEnd = PromeEventBus.OnBattleEnded(this, ClearLiveSafeZone);
        _safeZoneActivation = new SafeZoneActivation(SafeZone.Setting);
        _safeZoneActivation.Update(settings.EnableRadar, settings.EnableRadarOverlay, settings.EnableSmartMovement, settings.OptimizeDodgeTiming);
        _smartMovementEnabled = settings.EnableSmartMovement;

        _radar = new Radar.RadarRenderer();
        _dodge = new Dodge.DodgeController();
        _mechanics = new Mechanics.MechanicDispatcher(this);
        _mechanicHost = new MechanicHost.MechanicHostController();
        _scripts = new Scripting.ScriptManager();
        _scripts.RefreshCatalog();
        _replayRecorder = new Replay.ReplayRecorder();
        _replayController = new Replay.ReplayController(_scripts, _replayRecorder, _mechanicHost);
        _replayWindow = new Windows.ReplayWindow();
        _radarOverlay = new Radar.RadarOverlayWindow();
        _follow = new Follow.FollowController();
        _mainWindow = new Windows.MainWindow();
        _integration = new Integration.SmartMovementIntegration();

        Svc.Framework.Update += OnFrameworkUpdate;
        Svc.PluginInterface.UiBuilder.Draw += _mainWindow.Draw;
        Svc.PluginInterface.UiBuilder.Draw += _radarOverlay.Draw;
        Svc.PluginInterface.UiBuilder.Draw += _replayWindow.Draw;
        Svc.Commands.AddHandler(PanelCommand, new CommandInfo(OnPanelCommand)
        {
            HelpMessage = "打开/关闭 PromeRadar；/prpr ai on/off 开关智能走位；/prpr rec 开停录制；/prpr replay 开关回放窗口",
        });
        Svc.Commands.AddHandler(LegacyPanelCommand, new CommandInfo(OnPanelCommand) { HelpMessage = "PromeRadar 旧指令；请使用 /prpr" });
        _integration.Update(this);

        Svc.Log.Info($"[MasterPlayer] Initialized build={typeof(MasterPlayerPlugin).Module.ModuleVersionId:D}");
    }

    public void DrawConfigUI()
    {
        if (ImGui.Button("打开 PromeRadar 设置面板"))
            _mainWindow?.Toggle();
        ImGui.SameLine();
        ImGui.TextDisabled($"或输入 {PanelCommand}");
    }

    private void OnPanelCommand(string command, string args)
    {
        var arg = args.Trim();
        var parts = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 0 && parts[0].Equals("ai", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length == 2 && (parts[1].Equals("on", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("off", StringComparison.OrdinalIgnoreCase)))
            {
                SetSmartMovementEnabled(parts[1].Equals("on", StringComparison.OrdinalIgnoreCase));
                Svc.Chat.Print($"[PromeRadar] 智能走位已{(MasterPlayerSettings.Instance.EnableSmartMovement ? "开启" : "关闭")}");
            }
            else Svc.Chat.Print("[PromeRadar] 用法：/prpr ai on 或 /prpr ai off");
            return;
        }
        if (arg.Equals("rec", StringComparison.OrdinalIgnoreCase) || arg.Equals("record", StringComparison.OrdinalIgnoreCase))
        {
            if (_replayRecorder == null)
                return;
            if (_replayRecorder.IsRecording)
                _replayRecorder.StopRecording();
            else
                _replayRecorder.StartRecording();
            return;
        }

        if (arg.Equals("replay", StringComparison.OrdinalIgnoreCase))
        {
            _replayWindow?.Toggle();
            return;
        }

        _mainWindow?.Toggle();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        _integration?.ApplyRequests(this);
        var settings = MasterPlayerSettings.Instance;
        ApplyMovementPolicy(settings);
        var deltaTime = (float)framework.UpdateDelta.TotalSeconds;
        _safeZoneActivation?.Update(settings.EnableRadar, settings.EnableRadarOverlay, settings.EnableSmartMovement, settings.OptimizeDodgeTiming);
        _mechanicHost?.Update();
        _dodge?.Update();
        _mechanics?.Update();
        _replayRecorder?.Update();
        _replayController?.Update(deltaTime);
        _scripts?.Update(deltaTime);
        _follow?.Update();
        _safeZone?.Update(PromeRotation.Plugin.Instance.SpeedMultiplierService.EffectiveMultiplier,
            _scripts?.ReplayModeActive != true);
        AoeVisibility?.Update(SafeZone.Manager);
        _radar?.Update();
        _integration?.Update(this);
    }

    public void SetSmartMovementEnabled(bool enabled)
    {
        if (_disposed) return;
        var settings = MasterPlayerSettings.Instance;
        if (settings.EnableSmartMovement == enabled && _smartMovementEnabled == enabled) return;
        settings.EnableSmartMovement = enabled;
        ApplyMovementPolicy(settings);
        settings.Save();
    }

    private void ApplyMovementPolicy(MasterPlayerSettings settings)
    {
        if (_smartMovementEnabled == settings.EnableSmartMovement) return;
        _smartMovementEnabled = settings.EnableSmartMovement;
        if (!_smartMovementEnabled)
        {
            if (_safeZone != null) _safeZone.Setting.MovementAllowed = false;
            try { _scripts?.SuspendMovement(); }
            catch (Exception ex) { Svc.Log.Error(ex, "[PromeRadar] 撤销脚本移动失败"); }
            try { _mechanicHost?.SuspendMovement(); }
            catch (Exception ex) { Svc.Log.Error(ex, "[PromeRadar] 撤销模块朝向失败"); }
            try { _safeZone?.SuspendMovement(); }
            catch (Exception ex) { Svc.Log.Error(ex, "[PromeRadar] 停止智能走位失败"); }
        }
        _safeZoneActivation?.Update(settings.EnableRadar, settings.EnableRadarOverlay, settings.EnableSmartMovement, settings.OptimizeDodgeTiming);
    }

    private void ClearLiveSafeZone()
    {
        if (_scripts?.ReplayModeActive != true) _safeZone?.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        Svc.Framework.Update -= OnFrameworkUpdate;
        if (_mainWindow != null)
            Svc.PluginInterface.UiBuilder.Draw -= _mainWindow.Draw;
        if (_radarOverlay != null)
            Svc.PluginInterface.UiBuilder.Draw -= _radarOverlay.Draw;
        if (_replayWindow != null)
            Svc.PluginInterface.UiBuilder.Draw -= _replayWindow.Draw;

        foreach (var panelCommand in new[] { PanelCommand, LegacyPanelCommand })
        {
            try
            {
                Svc.Commands.RemoveHandler(panelCommand);
            }
            catch (Exception ex)
            {
                Svc.Log.Warning(ex, $"[PromeRadar] 移除聊天指令 {panelCommand} 失败");
            }
        }

        SafeDispose(ref _integration);
        SafeDispose(ref _replayWindow);
        SafeDispose(ref _mainWindow);
        SafeDispose(ref _follow);
        SafeDispose(ref _radarOverlay);
        SafeDispose(ref _replayController);
        SafeDispose(ref _replayRecorder);
        SafeDispose(ref _scripts);
        SafeDispose(ref _mechanicHost);
        SafeDispose(ref _mechanics);
        SafeDispose(ref _dodge);
        SafeDispose(ref _radar);
        SafeDispose(ref _safeZoneActivation);
        SafeDispose(ref _safeZoneTerritory);
        SafeDispose(ref _safeZoneDeath);
        SafeDispose(ref _safeZoneBattleEnd);
        SafeDispose(ref _safeZone);
        AoeVisibility = null;

        Instance = null;
        Svc.Log.Info("[MasterPlayer] Disposed");
    }

    private static void SafeDispose<T>(ref T? disposable) where T : class, IDisposable
    {
        try
        {
            disposable?.Dispose();
        }
        catch (Exception ex)
        {
            try
            {
                Svc.Log.Error(ex, $"[MasterPlayer] Failed to dispose {typeof(T).Name}");
            }
            catch
            {
            }
        }
        finally
        {
            disposable = null;
        }
    }
}
