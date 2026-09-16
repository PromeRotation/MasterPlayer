using ECommons.DalamudServices;
using PromeRotation.DownloadSystem.Plugins;
using PromeRotation.Hosting;
using PromeRotation.LogSystem;

namespace MasterPlayer.Scripting;

/// <summary>
/// 脚本运行时上下文实现：事件订阅（按 owner 归属）、SafeZone / Move / Draw 三门面、设置、日志。
/// 一个脚本实例持有一个上下文；停止脚本时统一清理事件订阅与三门面资源。
/// 构造时把自身绑定到脚本会话 ownerId，使 PromeEventBus 订阅与 UnsubscribeAll 落在同一 owner 下。
/// </summary>
internal sealed class MasterPlayerScriptContext : IScriptHostContext, IDisposable
{
    private readonly ScriptManager _manager;
    private readonly ScriptMetadata _metadata;
    private readonly string _ownerId;
    private readonly ScriptSettingStore _settings;
    private readonly ScriptSafeZoneApi _safeZone;
    private readonly ScriptMoveApi _move;
    private readonly ScriptDrawApi _draw;
    private bool _disposed;

    internal MasterPlayerScriptContext(ScriptManager manager, ScriptMetadata metadata, ScriptSettingStore settings)
    {
        _manager = manager;
        _metadata = metadata;
        _settings = settings;
        _ownerId = PromeOwnerRegistry.CreateScriptSessionOwnerId();
        PromeOwnerRegistry.Bind(this, _ownerId);
        _safeZone = new ScriptSafeZoneApi(metadata.Id, metadata.Name);
        _move = new ScriptMoveApi(Log);
        _draw = new ScriptDrawApi($"MasterPlayer_Script_{SanitizeId(metadata.Id)}_", _safeZone);
    }

    public ScriptSafeZoneApi SafeZone => _safeZone;
    public ScriptMoveApi Move => _move;
    public ScriptDrawApi Draw => _draw;
    public ScriptSettingStore Settings => _settings;

    public bool IsMechanismEnabled(string mechanism)
        => _manager.IsMechanismEnabled(_metadata.Id, mechanism);

    public void Log(string message)
    {
        _manager.SetStatus(message);
        Svc.Log.Info($"[MasterPlayer:{_metadata.Name}] {message}");
    }

    public void LogError(string message)
    {
        _manager.SetError(message);
        Svc.Log.Error($"[MasterPlayer:{_metadata.Name}] {message}");
    }

    // ── 事件订阅（PromeEventBus 按 owner 归属，停止统一清理）──

    public IDisposable Subscribe(string eventName, Delegate handler)
        => PromeEventBus.Subscribe(this, eventName, handler);

    public IDisposable OnBattleStarted(Action handler)
        => PromeEventBus.OnBattleStarted(this, handler);

    public IDisposable OnBattleEnded(Action handler)
        => PromeEventBus.OnBattleEnded(this, handler);

    public IDisposable OnPlayerDied(Action handler)
        => PromeEventBus.OnPlayerDied(this, handler);

    public IDisposable OnPlayerRevived(Action handler)
        => PromeEventBus.OnPlayerRevived(this, handler);

    public IDisposable OnPlayerMoveStateChanged(Action<bool> handler)
        => PromeEventBus.OnPlayerMoveStateChanged(this, handler);

    public IDisposable OnWeatherChanged(Action<uint> handler)
        => PromeEventBus.OnWeatherChanged(this, handler);

    public IDisposable OnTerritoryChanged(Action<ushort> handler)
        => PromeEventBus.OnTerritoryChanged(this, handler);

    public IDisposable OnCountdownStarted(Action handler)
        => PromeEventBus.OnCountdownStarted(this, handler);

    public IDisposable OnSystemLog(Action<uint> handler)
        => PromeEventBus.OnSystemLog(this, handler);

    public IDisposable OnBattleLog(Action<uint> handler)
        => PromeEventBus.OnBattleLog(this, handler);

    public IDisposable OnShowMessage(Action<uint> handler)
        => PromeEventBus.OnShowMessage(this, handler);

    public IDisposable OnChatLog(Action<string, string, string> handler)
        => PromeEventBus.OnChatLog(this, handler);

    public IDisposable OnLogEvent(Action<LogSystemEvent> handler)
        => PromeEventBus.OnLogEvent(this, handler);

    public IDisposable OnLogEvent<TEvent>(Action<TEvent> handler)
        where TEvent : LogSystemEvent
        => PromeEventBus.OnLogEvent(this, handler);

    public IDisposable OnStartCast(Action<LogSystemCastStartEvent> handler)
        => PromeEventBus.OnStartCast(this, handler);

    public IDisposable OnCancelCast(Action<LogSystemCancelCastEvent> handler)
        => PromeEventBus.OnCancelCast(this, handler);

    public IDisposable OnInterruptCast(Action<LogSystemInterruptCastEvent> handler)
        => PromeEventBus.OnInterruptCast(this, handler);

    public IDisposable OnActionEffect(Action<LogSystemActionEffectEvent> handler)
        => PromeEventBus.OnActionEffect(this, handler);

    public IDisposable OnCreateObject(Action<LogSystemObjectCreateEvent> handler)
        => PromeEventBus.OnCreateObject(this, handler);

    public IDisposable OnNpcYell(Action<LogSystemNpcYellEvent> handler)
        => PromeEventBus.OnNpcYell(this, handler);

    public IDisposable OnMapEffect(Action<LogSystemMapEffectEvent> handler)
        => PromeEventBus.OnMapEffect(this, handler);

    public IDisposable OnStatusAdd(Action<LogSystemStatusAddEvent> handler)
        => PromeEventBus.OnStatusAdd(this, handler);

    public IDisposable OnStatusRemove(Action<LogSystemStatusRemoveEvent> handler)
        => PromeEventBus.OnStatusRemove(this, handler);

    public IDisposable OnTargetIcon(Action<LogSystemTargetIconEvent> handler)
        => PromeEventBus.OnTargetIcon(this, handler);

    public IDisposable OnTether(Action<LogSystemTetherEvent> handler)
        => PromeEventBus.OnTether(this, handler);

    public IDisposable OnTetherCancel(Action<LogSystemTetherCancelEvent> handler)
        => PromeEventBus.OnTetherCancel(this, handler);

    public IDisposable OnEObjAnimation(Action<LogSystemEObjAnimationEvent> handler)
        => PromeEventBus.OnEObjAnimation(this, handler);

    public IDisposable OnEObjSetState(Action<LogSystemEObjSetStateEvent> handler)
        => PromeEventBus.OnEObjSetState(this, handler);

    public IDisposable OnPlayActionTimeline(Action<LogSystemPlayActionTimelineEvent> handler)
        => PromeEventBus.OnPlayActionTimeline(this, handler);

    public IDisposable OnTargetVfx(Action<LogSystemTargetVfxEvent> handler)
        => PromeEventBus.OnTargetVfx(this, handler);

    public IDisposable OnSetObjPos(Action<LogSystemSetObjPosEvent> handler)
        => PromeEventBus.OnSetObjPos(this, handler);

    public IDisposable OnMarker(Action<LogSystemMarkerEvent> handler)
        => PromeEventBus.OnMarker(this, handler);

    public IDisposable OnDirectorUpdate(Action<LogSystemDirectorUpdateEvent> handler)
        => PromeEventBus.OnDirectorUpdate(this, handler);

    public IDisposable OnMorelogCompat(Action<LogSystemMorelogCompatEvent> handler)
        => PromeEventBus.OnMorelogCompat(this, handler);

    public IDisposable OnPluginCatalogRefreshed(Action<IReadOnlyList<PluginManifest>> handler)
        => PromeEventBus.OnPluginCatalogRefreshed(this, handler);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        TryCleanup("退订事件", () =>
        {
            PromeOwnerRegistry.MarkClosing(_ownerId);
            PromeEventBus.UnsubscribeAll(_ownerId);
            PromeOwnerRegistry.Unbind(this);
        });
        TryCleanup("清理安全区", () => _safeZone.Cleanup());
        TryCleanup("停止移动", () => _move.Cleanup());
        TryCleanup("清理绘制", () => _draw.Cleanup());
    }

    private void TryCleanup(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[MasterPlayer:{_metadata.Name}] 脚本上下文{what}失败：{ex.Message}");
        }
    }

    private static string SanitizeId(string id)
    {
        var chars = id.Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? "unknown" : new string(chars);
    }
}
