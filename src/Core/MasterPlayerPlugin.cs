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
    version: PluginBuildVersion.Value)]
public sealed class MasterPlayerPlugin : IPromePlugin
{
    private const string PanelCommand = "/prpr";
    private const string LegacyPanelCommand = "/prmp";
    private Radar.RadarRenderer? _radar;
    private Dodge.DodgeController? _dodge;
    private Mechanics.MechanicDispatcher? _mechanics;
    private MechanicHost.MechanicHostController? _mechanicHost;
    private Scripting.ScriptManager? _scripts;
    private Scripting.LegacyMapEffectCapture? legacyMapEffects;
    private Scripting.NativeActionEffectCapture? nativeActionEffects;
    private Replay.ReplayRecorder? _replayRecorder;
    private Replay.ReplayController? _replayController;
    private Radar.RadarOverlayWindow? _radarOverlay;
    private Follow.FollowController? _follow;
    private Windows.MainWindow? _mainWindow;
    private Windows.ReplayWindow? _replayWindow;
    private SafeZoneActivation? _safeZoneActivation;
    private SafeZoneRuntime? _safeZone;
    private Integration.SmartMovementIntegration? _integration;
    private Integration.RemoteControlClient? _remoteControl;
    private Combat.Sequences.CombatSkillSequence? _skillSequences;
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
    internal void SetLegacyMapReplayMode(bool active)
    {
        legacyMapEffects?.SetReplayMode(active);
        nativeActionEffects?.SetReplayMode(active);
    }
    internal void DrainNativeActionEffects(DateTime? through = null)
        => nativeActionEffects?.Drain(_scripts?.ReplayModeActive == true, through);

    public Integration.RemoteControlClient? RemoteControl => _remoteControl;
    internal Combat.Sequences.CombatSkillSequence? SkillSequences => _skillSequences;

    public void Initialize()
    {
        Instance = this;

        if (!Svc.ClientState.IsPvP)
            InitializeRuntime();

        Svc.ClientState.EnterPvP += OnEnterPvP;
        Svc.Framework.Update += OnFrameworkUpdate;
        Svc.PluginInterface.UiBuilder.Draw += Draw;
        Svc.Commands.AddHandler(PanelCommand, new CommandInfo(OnPanelCommand)
        {
            HelpMessage = "打开/关闭 PromeRadar；/prpr ai on/off 开关智能走位；/prpr rec 开停录制；/prpr replay 开关回放窗口",
        });
        Svc.Commands.AddHandler(LegacyPanelCommand, new CommandInfo(OnPanelCommand) { HelpMessage = "PromeRadar 旧指令；请使用 /prpr" });

        Svc.Log.Info($"[MasterPlayer] Initialized build={typeof(MasterPlayerPlugin).Module.ModuleVersionId:D}");
    }

    private void InitializeRuntime()
    {
        try
        {
            var settings = MasterPlayerSettings.Instance;
            AoeVisibility = new Radar.AoeVisibilityRegistry(settings.AoeVisibility, () => SafeZone.CurrentTime);
            _safeZone = new(PromeRotation.Plugin.Instance.GreenMoveSystem,
                () => HostMovement.AutomaticMovementEnabled, () => HostMovement.HasControl);
            _safeZoneTerritory = PromeEventBus.OnTerritoryChanged(this, _ =>
            {
                legacyMapEffects?.Clear();
                nativeActionEffects?.Clear();
                ClearLiveSafeZone();
            });
            _safeZoneDeath = PromeEventBus.OnPlayerDied(this, () => { if (!PlayerPerspective.IsDutyReplay) ClearLiveSafeZone(); });
            _safeZoneBattleEnd = PromeEventBus.OnBattleEnded(this, () => ClearLiveSafeZone(preserveTerrain: true));
            _safeZoneActivation = new SafeZoneActivation(SafeZone.Setting);
            _safeZoneActivation.Update(settings.EnableRadar, settings.EnableRadarOverlay, settings.EnableSmartMovement, settings.OptimizeDodgeTiming);
            _smartMovementEnabled = settings.EnableSmartMovement && !PlayerPerspective.IsReplay;

            _radar = new Radar.RadarRenderer();
            _dodge = new Dodge.DodgeController();
            _mechanics = new Mechanics.MechanicDispatcher(this);
            _mechanicHost = new MechanicHost.MechanicHostController();
            _scripts = new Scripting.ScriptManager();
            _scripts.RefreshCatalog();
            _replayRecorder = new Replay.ReplayRecorder();
            _replayController = new Replay.ReplayController(_scripts, _replayRecorder, _mechanicHost);
            legacyMapEffects = new Scripting.LegacyMapEffectCapture();
            nativeActionEffects = new Scripting.NativeActionEffectCapture();
            _replayWindow = new Windows.ReplayWindow();
            _radarOverlay = new Radar.RadarOverlayWindow();
            _follow = new Follow.FollowController();
            _remoteControl = new Integration.RemoteControlClient();
            _skillSequences = new Combat.Sequences.CombatSkillSequence();
            _mainWindow = new Windows.MainWindow();
            _integration = new Integration.SmartMovementIntegration(SafeZone);

            _integration.Update(this);
        }
        catch
        {
            DisposeRuntime();
            throw;
        }
    }

    public void DrawConfigUI()
    {
        if (Svc.ClientState.IsPvP)
        {
            ImGui.TextDisabled("PvP 区域内 PromeRadar 所有功能已暂停，离开后自动恢复。");
            return;
        }
        if (ImGui.Button("打开 PromeRadar 设置面板"))
            _mainWindow?.Toggle();
        ImGui.SameLine();
        ImGui.TextDisabled($"或输入 {PanelCommand}");
    }

    private void OnPanelCommand(string command, string args)
    {
        if (_disposed)
            return;
        if (Svc.ClientState.IsPvP)
        {
            Svc.Chat.Print("[PromeRadar] PvP 区域内所有功能已暂停，离开后自动恢复。");
            return;
        }
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

    private void Draw()
    {
        if (_disposed || Svc.ClientState.IsPvP || _safeZone == null)
            return;
        _mainWindow?.Draw();
        _radarOverlay?.Draw();
        _replayWindow?.Draw();
    }

    private void OnEnterPvP()
    {
        if (_disposed || _safeZone == null)
            return;

        // 事件订阅、原生 hook 和宿主绘制不会随 Update 停止，统一释放运行组件。
        // 保留配置及插件入口，离开 PvP 后由下一次 Framework 更新重建。
        DisposeRuntime();
        Svc.Log.Info("[PromeRadar] 进入 PvP 区域，所有功能已暂停。");
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (_disposed)
            return;
        if (Svc.ClientState.IsPvP)
        {
            OnEnterPvP();
            return;
        }
        if (_safeZone == null)
        {
            InitializeRuntime();
            Svc.Log.Info("[PromeRadar] 已离开 PvP 区域，按原设置恢复功能。");
        }

        _integration?.ApplyRequests(this);
        var settings = MasterPlayerSettings.Instance;
        ApplyMovementPolicy(settings);
        var deltaTime = (float)framework.UpdateDelta.TotalSeconds;
        var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        _safeZoneActivation?.Update(settings.EnableRadar, settings.EnableRadarOverlay, _smartMovementEnabled, settings.OptimizeDodgeTiming);
        var tActivation = System.Diagnostics.Stopwatch.GetTimestamp();
        _mechanicHost?.Update();
        var tHost = System.Diagnostics.Stopwatch.GetTimestamp();
        _dodge?.Update();
        var tDodge = System.Diagnostics.Stopwatch.GetTimestamp();
        _mechanics?.Update();
        var t1 = System.Diagnostics.Stopwatch.GetTimestamp();
        Scripting.SystemLogParameterEvents.Drain(_scripts?.ReplayModeActive == true);
        DrainNativeActionEffects();
        _replayRecorder?.Update();
        _replayController?.Update(deltaTime);
        legacyMapEffects?.Drain(_scripts?.ReplayModeActive == true);
        _skillSequences?.Update();
        var t2 = System.Diagnostics.Stopwatch.GetTimestamp();
        _scripts?.Update(deltaTime);
        var t3 = System.Diagnostics.Stopwatch.GetTimestamp();
        _follow?.Update();
        var tFollow = System.Diagnostics.Stopwatch.GetTimestamp();
        _safeZone?.Update(PromeRotation.Plugin.Instance.SpeedMultiplierService.EffectiveMultiplier,
            _scripts?.ReplayModeActive != true);
        var t4 = System.Diagnostics.Stopwatch.GetTimestamp();
        AoeVisibility?.Update(SafeZone.Manager);
        _radar?.Update();
        _integration?.Update(this);
        var t5 = System.Diagnostics.Stopwatch.GetTimestamp();
        var total = System.Diagnostics.Stopwatch.GetElapsedTime(t0, t5).TotalMilliseconds;
        if (total >= 12 && DateTime.UtcNow >= _nextPerfLog)
        {
            _nextPerfLog = DateTime.UtcNow.AddSeconds(1);
            static double Ms(long a, long b) => System.Diagnostics.Stopwatch.GetElapsedTime(a, b).TotalMilliseconds;
            Svc.Log.Info($"[MP-PERF] 帧 {total:F1}ms host/dodge={Ms(t0, t1):F1} (activation={Ms(t0, tActivation):F1} host={Ms(tActivation, tHost):F1} dodge={Ms(tHost, tDodge):F1} dispatcher={Ms(tDodge, t1):F1}) replay={Ms(t1, t2):F1} scripts={Ms(t2, t3):F1} follow={Ms(t3, tFollow):F1} safezone={Ms(tFollow, t4):F1} radar/aoe={Ms(t4, t5):F1} ver={_safeZone?.Manager.Version}");
        }
    }
    private DateTime _nextPerfLog;

    public void SetSmartMovementEnabled(bool enabled)
    {
        if (_disposed || Svc.ClientState.IsPvP) return;
        var settings = MasterPlayerSettings.Instance;
        if (settings.EnableSmartMovement == enabled && _smartMovementEnabled == enabled) return;
        settings.EnableSmartMovement = enabled;
        ApplyMovementPolicy(settings);
        settings.Save();
    }

    private void ApplyMovementPolicy(MasterPlayerSettings settings)
    {
        var movementEnabled = settings.EnableSmartMovement && !Svc.ClientState.IsPvP && !PlayerPerspective.IsReplay;
        if (_smartMovementEnabled == movementEnabled) return;
        _smartMovementEnabled = movementEnabled;
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
        _safeZoneActivation?.Update(settings.EnableRadar, settings.EnableRadarOverlay, movementEnabled, settings.OptimizeDodgeTiming);
    }

    private void ClearLiveSafeZone(bool preserveTerrain = false)
    {
        if (_scripts?.ReplayModeActive == true) return;
        if (preserveTerrain) _safeZone?.ClearCombatState();
        else _safeZone?.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        Svc.ClientState.EnterPvP -= OnEnterPvP;
        Svc.Framework.Update -= OnFrameworkUpdate;
        Svc.PluginInterface.UiBuilder.Draw -= Draw;

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

        DisposeRuntime();

        Instance = null;
        Svc.Log.Info("[MasterPlayer] Disposed");
    }

    private void DisposeRuntime()
    {
        _smartMovementEnabled = false;
        if (_safeZone != null)
            _safeZone.Setting.MovementAllowed = false;

        SafeDispose(ref _replayRecorder);
        SafeDispose(ref legacyMapEffects);
        SafeDispose(ref nativeActionEffects);
        SafeDispose(ref _skillSequences);
        SafeDispose(ref _remoteControl);
        SafeDispose(ref _integration);
        SafeDispose(ref _replayWindow);
        SafeDispose(ref _mainWindow);
        SafeDispose(ref _follow);
        SafeDispose(ref _radarOverlay);
        SafeDispose(ref _replayController);
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
        _nextPerfLog = default;
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
