using ECommons.DalamudServices;
using PromeRotation.DownloadSystem.Plugins;
using PromeRotation.Hosting;
using PromeRotation.LogSystem;

namespace MasterPlayer.Scripting;

/// <summary>
/// 脚本运行时上下文实现：事件订阅（按 owner 归属）、SafeZone / Move / Draw 三门面、设置、日志。
/// 一个脚本实例持有一个上下文；停止脚本时统一清理事件订阅与三门面资源。
/// 构造时把自身绑定到脚本会话 ownerId，使 PromeEventBus 订阅与 UnsubscribeAll 落在同一 owner 下。
/// 事件订阅经 IScriptEventRouter 落地：默认实时路由（PromeEventBus），回放会话注入回放路由。
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
    private readonly ScriptHintsApi _hints;
    private readonly IScriptEventRouter _router;
    private readonly HashSet<uint> _suppressedDodgeActions = [];
    private bool _disposed;

    internal MasterPlayerScriptContext(ScriptManager manager, ScriptMetadata metadata, ScriptSettingStore settings, IScriptEventRouter? router = null)
    {
        _manager = manager;
        _metadata = metadata;
        _settings = settings;
        _router = router ?? LiveScriptEventRouter.Instance;
        _ownerId = PromeOwnerRegistry.CreateScriptSessionOwnerId();
        PromeOwnerRegistry.Bind(this, _ownerId);
        _safeZone = new ScriptSafeZoneApi(metadata.Id, metadata.Name);
        _move = new ScriptMoveApi(Log);
        _draw = new ScriptDrawApi($"MasterPlayer_Script_{SanitizeId(metadata.Id)}_", _safeZone);
        _hints = new ScriptHintsApi(() => IsReplay);
    }

    public ScriptSafeZoneApi SafeZone => _safeZone;
    public ScriptMoveApi Move => _move;
    public ScriptDrawApi Draw => _draw;
    public ScriptHintsApi Hints => _hints;
    public ScriptSettingStore Settings => _settings;
    public Replay.ReplayWorldState? ReplayWorld => (_router as Replay.ReplayScriptEventRouter)?.World;
    public DateTime CurrentTime => ReplayWorld?.CurrentTime ?? DateTime.UtcNow;

    internal bool IsReplay => !ReferenceEquals(_router, LiveScriptEventRouter.Instance);

    public bool IsMechanismEnabled(string mechanism)
        => _manager.IsMechanismEnabled(_metadata.Id, mechanism);

    public void SuppressGenericDodge(params uint[] actionIds)
    {
        foreach (var id in actionIds)
            _suppressedDodgeActions.Add(id);
    }

    internal bool SuppressesGenericDodge(uint actionId)
        => _suppressedDodgeActions.Contains(actionId);

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

    // ── 事件订阅（经 IScriptEventRouter；实时= PromeEventBus 按 owner 归属，停止统一清理）──

    public IDisposable Subscribe(string eventName, Delegate handler)
        => _router.Subscribe(this, eventName, handler);

    public IDisposable OnBattleStarted(Action handler)
        => _router.Subscribe(this, "BattleStarted", handler);

    public IDisposable OnBattleEnded(Action handler)
        => _router.Subscribe(this, "BattleEnded", handler);

    public IDisposable OnPlayerDied(Action handler)
        => _router.Subscribe(this, "PlayerDied", handler);

    public IDisposable OnPlayerRevived(Action handler)
        => _router.Subscribe(this, "PlayerRevived", handler);

    public IDisposable OnPlayerMoveStateChanged(Action<bool> handler)
        => _router.Subscribe(this, "PlayerMoveStateChanged", handler);

    public IDisposable OnWeatherChanged(Action<uint> handler)
        => _router.Subscribe(this, "WeatherChanged", handler);

    public IDisposable OnTerritoryChanged(Action<ushort> handler)
        => _router.Subscribe(this, "TerritoryChanged", handler);

    public IDisposable OnCountdownStarted(Action handler)
        => _router.Subscribe(this, "CountdownStarted", handler);

    public IDisposable OnSystemLog(Action<uint> handler)
        => _router.Subscribe(this, "SystemLog", handler);

    public IDisposable OnBattleLog(Action<uint> handler)
        => _router.Subscribe(this, "BattleLog", handler);

    public IDisposable OnShowMessage(Action<uint> handler)
        => _router.Subscribe(this, "ShowMessage", handler);

    public IDisposable OnChatLog(Action<string, string, string> handler)
        => _router.Subscribe(this, "ChatLog", handler);

    public IDisposable OnLogEvent(Action<LogSystemEvent> handler)
        => _router.Subscribe(this, "LogEvent", handler, typeof(LogSystemEvent));

    public IDisposable OnLogEvent<TEvent>(Action<TEvent> handler)
        where TEvent : LogSystemEvent
        => _router.Subscribe(this, "LogEvent", handler, typeof(TEvent));

    public IDisposable OnStartCast(Action<LogSystemCastStartEvent> handler)
        => _router.Subscribe(this, "StartCast", handler, typeof(LogSystemCastStartEvent));

    public IDisposable OnCancelCast(Action<LogSystemCancelCastEvent> handler)
        => _router.Subscribe(this, "CancelCast", handler, typeof(LogSystemCancelCastEvent));

    public IDisposable OnInterruptCast(Action<LogSystemInterruptCastEvent> handler)
        => _router.Subscribe(this, "InterruptCast", handler, typeof(LogSystemInterruptCastEvent));

    public IDisposable OnActionEffect(Action<LogSystemActionEffectEvent> handler)
        => _router.Subscribe(this, "ActionEffect", handler, typeof(LogSystemActionEffectEvent));

    public IDisposable OnCreateObject(Action<LogSystemObjectCreateEvent> handler)
        => _router.Subscribe(this, "CreateObject", handler, typeof(LogSystemObjectCreateEvent));

    public IDisposable OnNpcYell(Action<LogSystemNpcYellEvent> handler)
        => _router.Subscribe(this, "NpcYell", handler, typeof(LogSystemNpcYellEvent));

    public IDisposable OnMapEffect(Action<LogSystemMapEffectEvent> handler)
        => _router.Subscribe(this, "MapEffect", handler, typeof(LogSystemMapEffectEvent));

    public IDisposable OnStatusAdd(Action<LogSystemStatusAddEvent> handler)
        => _router.Subscribe(this, "StatusAdd", handler, typeof(LogSystemStatusAddEvent));

    public IDisposable OnStatusRemove(Action<LogSystemStatusRemoveEvent> handler)
        => _router.Subscribe(this, "StatusRemove", handler, typeof(LogSystemStatusRemoveEvent));

    public IDisposable OnTargetIcon(Action<LogSystemTargetIconEvent> handler)
        => _router.Subscribe(this, "TargetIcon", handler, typeof(LogSystemTargetIconEvent));

    public IDisposable OnTether(Action<LogSystemTetherEvent> handler)
        => _router.Subscribe(this, "Tether", handler, typeof(LogSystemTetherEvent));

    public IDisposable OnTetherCancel(Action<LogSystemTetherCancelEvent> handler)
        => _router.Subscribe(this, "TetherCancel", handler, typeof(LogSystemTetherCancelEvent));

    public IDisposable OnEObjAnimation(Action<LogSystemEObjAnimationEvent> handler)
        => _router.Subscribe(this, "EObjAnimation", handler, typeof(LogSystemEObjAnimationEvent));

    public IDisposable OnEObjSetState(Action<LogSystemEObjSetStateEvent> handler)
        => _router.Subscribe(this, "EObjSetState", handler, typeof(LogSystemEObjSetStateEvent));

    public IDisposable OnPlayActionTimeline(Action<LogSystemPlayActionTimelineEvent> handler)
        => _router.Subscribe(this, "PlayActionTimeline", handler, typeof(LogSystemPlayActionTimelineEvent));

    public IDisposable OnTargetVfx(Action<LogSystemTargetVfxEvent> handler)
        => _router.Subscribe(this, "TargetVfx", handler, typeof(LogSystemTargetVfxEvent));

    public IDisposable OnSetObjPos(Action<LogSystemSetObjPosEvent> handler)
        => _router.Subscribe(this, "SetObjPos", handler, typeof(LogSystemSetObjPosEvent));

    public IDisposable OnMarker(Action<LogSystemMarkerEvent> handler)
        => _router.Subscribe(this, "Marker", handler, typeof(LogSystemMarkerEvent));

    public IDisposable OnDirectorUpdate(Action<LogSystemDirectorUpdateEvent> handler)
        => _router.Subscribe(this, "DirectorUpdate", handler, typeof(LogSystemDirectorUpdateEvent));

    public IDisposable OnMorelogCompat(Action<LogSystemMorelogCompatEvent> handler)
        => _router.Subscribe(this, "MorelogCompat", handler, typeof(LogSystemMorelogCompatEvent));

    public IDisposable OnPluginCatalogRefreshed(Action<IReadOnlyList<PluginManifest>> handler)
        => _router.Subscribe(this, "CatalogRefreshed", handler);

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
        TryCleanup("释放事件路由", () => _router.Dispose());
        TryCleanup("清理安全区", () => _safeZone.Cleanup());
        TryCleanup("停止移动", () => _move.Cleanup());
        TryCleanup("清理绘制", () => _draw.Cleanup());
        TryCleanup("撤销朝向", () => _hints.Cleanup());
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
