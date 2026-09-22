using PromeRotation.DownloadSystem.Plugins;
using PromeRotation.LogSystem;

namespace MasterPlayer.Scripting;

/// <summary>
/// 机制脚本基类。生命周期：OnStart → OnUpdate(每帧) → OnStop。
/// 事件订阅统一用 OnXxx 系列（宿主按脚本 owner 归属，停止时自动清理）。
/// SafeZone 注册走 SafeZone 门面，移动走 Move 门面，绘制走 Draw 门面。
/// </summary>
public abstract class MasterPlayerScript
{
    private bool _started;

    /// <summary>宿主注入的运行上下文（事件订阅、三门面、设置读写）。</summary>
    public IScriptHostContext Host { get; internal set; } = null!;

    /// <summary>SafeZone 注册门面（包装 PR SafeZoneRegistry，停止时自动移除本脚本注册的区域）。</summary>
    public ScriptSafeZoneApi SafeZone => Host.SafeZone;

    /// <summary>移动门面（包装 PR GreenMoveSystem，内置让位/接管纪律）。</summary>
    public ScriptMoveApi Move => Host.Move;

    /// <summary>绘制门面（包装 PR DrawManager，id 自动加脚本前缀，停止时自动清理）。</summary>
    public ScriptDrawApi Draw => Host.Draw;

    /// <summary>本脚本的设置存取（[UserSetting] 属性自动映射，亦可键值读写）。</summary>
    public ScriptSettingStore Settings => Host.Settings;
    public DateTime CurrentTime => Host.CurrentTime;
    public Replay.ReplayWorldState? ReplayWorld => Host.ReplayWorld;

    /// <summary>
    /// 暴露给用户的机制开关列表。返回的每个名称都会在 UI 显示独立开关，
    /// 脚本内用 <see cref="IsMechanismEnabled"/> 检查。默认空（无机制开关）。
    /// </summary>
    public virtual IEnumerable<string> GetMechanisms() => [];

    /// <summary>机制是否被用户开启。</summary>
    public bool IsMechanismEnabled(string mechanism) => Host.IsMechanismEnabled(mechanism);

    /// <summary>声明本脚本接管的读条技能 ID：脚本运行期间，通用躲避跳过这些技能的自动危险区，避免与脚本重复注册。在 OnStart 里声明一次即可。</summary>
    protected void SuppressGenericDodge(params uint[] actionIds) => Host.SuppressGenericDodge(actionIds);

    // ── 生命周期（宿主驱动）────────────────────────────────

    /// <summary>脚本启动（事件订阅在这里做）。</summary>
    protected virtual void OnStart()
    {
    }

    /// <summary>每帧更新（host 驱动，dt 秒）。</summary>
    protected virtual void OnUpdate(float deltaTime)
    {
    }

    /// <summary>脚本停止（宿主随后清理事件订阅 / 安全区 / 移动 / 绘制，这里做脚本自身收尾）。</summary>
    protected virtual void OnStop()
    {
    }

    internal void StartInternal()
    {
        if (_started)
            return;
        _started = true;
        OnStart();
    }

    internal void UpdateInternal(float dt)
    {
        OnUpdate(dt);
    }

    internal void StopInternal()
    {
        if (!_started)
            return;
        _started = false;
        try
        {
            OnStop();
        }
        finally
        {
            (Host as IDisposable)?.Dispose();
        }
    }

    // ── 事件订阅（转发到 Host，按 owner 归属，停止自动清理）──

    /// <summary>按事件名订阅（PromeEventBus 事件名，兜底用）。</summary>
    protected IDisposable Subscribe(string eventName, Delegate handler)
        => Host.Subscribe(eventName, handler);

    // 战斗
    protected IDisposable OnBattleStarted(Action handler) => Host.OnBattleStarted(handler);
    protected IDisposable OnBattleEnded(Action handler) => Host.OnBattleEnded(handler);

    // 玩家状态
    protected IDisposable OnPlayerDied(Action handler) => Host.OnPlayerDied(handler);
    protected IDisposable OnPlayerRevived(Action handler) => Host.OnPlayerRevived(handler);
    protected IDisposable OnPlayerMoveStateChanged(Action<bool> handler) => Host.OnPlayerMoveStateChanged(handler);

    // 环境
    protected IDisposable OnWeatherChanged(Action<uint> handler) => Host.OnWeatherChanged(handler);
    protected IDisposable OnTerritoryChanged(Action<ushort> handler) => Host.OnTerritoryChanged(handler);
    protected IDisposable OnCountdownStarted(Action handler) => Host.OnCountdownStarted(handler);

    // 日志 / 聊天
    protected IDisposable OnSystemLog(Action<uint> handler) => Host.OnSystemLog(handler);
    protected IDisposable OnBattleLog(Action<uint> handler) => Host.OnBattleLog(handler);
    protected IDisposable OnShowMessage(Action<uint> handler) => Host.OnShowMessage(handler);
    protected IDisposable OnChatLog(Action<string, string, string> handler) => Host.OnChatLog(handler);
    protected IDisposable OnLogEvent(Action<LogSystemEvent> handler) => Host.OnLogEvent(handler);

    /// <summary>按强类型订阅 LogSystem 事件（覆盖无专用 OnXxx 的事件类型，如 Waymark / Knockback / ObjectRemove / InstanceContentText 等）。</summary>
    protected IDisposable OnLogEvent<TEvent>(Action<TEvent> handler) where TEvent : LogSystemEvent
        => Host.OnLogEvent(handler);

    // 战斗事件（LogSystem 事件族）
    protected IDisposable OnStartCast(Action<LogSystemCastStartEvent> handler) => Host.OnStartCast(handler);
    protected IDisposable OnCancelCast(Action<LogSystemCancelCastEvent> handler) => Host.OnCancelCast(handler);
    protected IDisposable OnInterruptCast(Action<LogSystemInterruptCastEvent> handler) => Host.OnInterruptCast(handler);
    protected IDisposable OnActionEffect(Action<LogSystemActionEffectEvent> handler) => Host.OnActionEffect(handler);
    protected IDisposable OnCreateObject(Action<LogSystemObjectCreateEvent> handler) => Host.OnCreateObject(handler);
    protected IDisposable OnNpcYell(Action<LogSystemNpcYellEvent> handler) => Host.OnNpcYell(handler);
    protected IDisposable OnMapEffect(Action<LogSystemMapEffectEvent> handler) => Host.OnMapEffect(handler);
    protected IDisposable OnStatusAdd(Action<LogSystemStatusAddEvent> handler) => Host.OnStatusAdd(handler);
    protected IDisposable OnStatusRemove(Action<LogSystemStatusRemoveEvent> handler) => Host.OnStatusRemove(handler);
    protected IDisposable OnTargetIcon(Action<LogSystemTargetIconEvent> handler) => Host.OnTargetIcon(handler);
    protected IDisposable OnTether(Action<LogSystemTetherEvent> handler) => Host.OnTether(handler);
    protected IDisposable OnTetherCancel(Action<LogSystemTetherCancelEvent> handler) => Host.OnTetherCancel(handler);
    protected IDisposable OnEObjAnimation(Action<LogSystemEObjAnimationEvent> handler) => Host.OnEObjAnimation(handler);
    protected IDisposable OnEObjSetState(Action<LogSystemEObjSetStateEvent> handler) => Host.OnEObjSetState(handler);
    protected IDisposable OnPlayActionTimeline(Action<LogSystemPlayActionTimelineEvent> handler) => Host.OnPlayActionTimeline(handler);
    protected IDisposable OnTargetVfx(Action<LogSystemTargetVfxEvent> handler) => Host.OnTargetVfx(handler);
    protected IDisposable OnSetObjPos(Action<LogSystemSetObjPosEvent> handler) => Host.OnSetObjPos(handler);
    protected IDisposable OnMarker(Action<LogSystemMarkerEvent> handler) => Host.OnMarker(handler);
    protected IDisposable OnDirectorUpdate(Action<LogSystemDirectorUpdateEvent> handler) => Host.OnDirectorUpdate(handler);
    protected IDisposable OnMorelogCompat(Action<LogSystemMorelogCompatEvent> handler) => Host.OnMorelogCompat(handler);

    // 插件
    protected IDisposable OnPluginCatalogRefreshed(Action<IReadOnlyList<PluginManifest>> handler)
        => Host.OnPluginCatalogRefreshed(handler);

    // ── 日志 ─────────────────────────────────────────────

    protected void Log(string message) => Host.Log(message);
    protected void LogError(string message) => Host.LogError(message);
}
