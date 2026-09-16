using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using MasterPlayer.Settings;
using PromeRotation.Plugins;

namespace MasterPlayer;

[PromePlugin(
    id: "masterplayer",
    name: "MasterPlayer",
    author: "XSZ",
    description: "雷达悬浮窗 + 真实世界绘制 + 智能躲避 + 战斗中跟随 + 机制脚本框架",
    version: "0.1.0.0")]
public sealed class MasterPlayerPlugin : IPromePlugin
{
    private Radar.RadarRenderer? _radar;
    private Dodge.DodgeController? _dodge;
    private Mechanics.MechanicDispatcher? _mechanics;
    private Scripting.ScriptManager? _scripts;
    private Radar.RadarOverlayWindow? _radarOverlay;
    private Follow.FollowController? _follow;
    private Windows.MainWindow? _mainWindow;
    private SafeZoneActivation? _safeZoneActivation;
    internal Radar.AoeVisibilityRegistry? AoeVisibility { get; private set; }
    private bool _disposed;

    public static MasterPlayerPlugin? Instance { get; private set; }

    internal Mechanics.MechanicDispatcher? Mechanics => _mechanics;

    internal Scripting.ScriptManager? Scripts => _scripts;

    public void Initialize()
    {
        Instance = this;

        var settings = MasterPlayerSettings.Instance;
        AoeVisibility = new Radar.AoeVisibilityRegistry(settings.AoeVisibility);
        _safeZoneActivation = new SafeZoneActivation(PromeRotation.Plugin.Instance.SafeZoneSetting);
        _safeZoneActivation.Update(settings.EnableRadar, settings.EnableRadarOverlay, settings.EnableAutoDodge);

        _radar = new Radar.RadarRenderer();
        _dodge = new Dodge.DodgeController();
        _mechanics = new Mechanics.MechanicDispatcher(this);
        _scripts = new Scripting.ScriptManager();
        _scripts.RefreshCatalog();
        _radarOverlay = new Radar.RadarOverlayWindow();
        _follow = new Follow.FollowController();
        _mainWindow = new Windows.MainWindow();

        Svc.Framework.Update += OnFrameworkUpdate;
        Svc.PluginInterface.UiBuilder.Draw += _mainWindow.Draw;
        Svc.PluginInterface.UiBuilder.Draw += _radarOverlay.Draw;

        Svc.Log.Info("[MasterPlayer] Initialized");
    }

    public void DrawConfigUI()
    {
        if (ImGui.Button("打开 MasterPlayer 面板"))
            _mainWindow?.Toggle();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var settings = MasterPlayerSettings.Instance;
        _safeZoneActivation?.Update(settings.EnableRadar, settings.EnableRadarOverlay, settings.EnableAutoDodge);
        AoeVisibility?.Update(PromeRotation.Plugin.Instance.SafeZoneManager);
        _radar?.Update();
        _dodge?.Update();
        _mechanics?.Update();
        _scripts?.Update((float)framework.UpdateDelta.TotalSeconds);
        _follow?.Update();
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

        SafeDispose(ref _mainWindow);
        SafeDispose(ref _follow);
        SafeDispose(ref _radarOverlay);
        SafeDispose(ref _scripts);
        SafeDispose(ref _mechanics);
        SafeDispose(ref _dodge);
        SafeDispose(ref _radar);
        SafeDispose(ref _safeZoneActivation);
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
