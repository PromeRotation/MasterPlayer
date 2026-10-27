using PromeRotation.DownloadSystem.Plugins;
using PromeRotation.LogSystem;

namespace MasterPlayer.Scripting;

/// <summary>
/// 宿主注入给脚本的运行时上下文：SafeZone / GreenMove / Draw 三门面、设置、日志、事件订阅。
/// 事件订阅返回的 IDisposable 全部归属当前脚本，停止时统一清理。
/// 事件方法与 PR 总线一一对应（35 个强类型 + Subscribe 兜底），脚本无需手写事件名。
/// </summary>
public interface IScriptHostContext
{
    DateTime CurrentTime => DateTime.UtcNow;
    Replay.ReplayWorldState? ReplayWorld => null;
    // ── 三门面 ────────────────────────────────────────
    ScriptSafeZoneApi SafeZone { get; }
    ScriptMoveApi Move { get; }
    ScriptDrawApi Draw { get; }
    ScriptHintsApi Hints { get; }

    // ── 设置 ──────────────────────────────────────────
    ScriptSettingStore Settings { get; }

    // ── 机制开关 ──────────────────────────────────────
    bool IsMechanismEnabled(string mechanism);

    // ── 通用躲避压制 ──────────────────────────────────
    /// <summary>声明本脚本接管的读条技能 ID：脚本运行期间，通用躲避（DodgeController）跳过这些技能的自动危险区，避免与脚本重复注册。</summary>
    void SuppressGenericDodge(params uint[] actionIds);

    // ── 日志 ──────────────────────────────────────────
    void Log(string message);
    void LogError(string message);

    // ── 事件订阅（按脚本 owner 归属，停止自动清理）───

    /// <summary>按事件名订阅（PromeEventBus 事件名，兜底用）。</summary>
    IDisposable Subscribe(string eventName, Delegate handler);

    // 战斗
    IDisposable OnBattleStarted(Action handler);
    IDisposable OnBattleEnded(Action handler);

    // 玩家状态
    IDisposable OnPlayerDied(Action handler);
    IDisposable OnPlayerRevived(Action handler);
    IDisposable OnPlayerMoveStateChanged(Action<bool> handler);

    // 环境
    IDisposable OnWeatherChanged(Action<uint> handler);
    IDisposable OnTerritoryChanged(Action<ushort> handler);
    IDisposable OnCountdownStarted(Action handler);

    // 日志 / 聊天
    IDisposable OnSystemLog(Action<uint> handler);
    IDisposable OnBattleLog(Action<uint> handler);
    IDisposable OnShowMessage(Action<uint> handler);
    IDisposable OnChatLog(Action<string, string, string> handler);
    IDisposable OnLogEvent(Action<LogSystemEvent> handler);

    /// <summary>按强类型订阅 LogSystem 事件（覆盖无专用 OnXxx 的事件类型，如 Waymark / Knockback / ObjectRemove / InstanceContentText 等）。</summary>
    IDisposable OnLogEvent<TEvent>(Action<TEvent> handler) where TEvent : LogSystemEvent;

    // 战斗事件（LogSystem 事件族）
    IDisposable OnStartCast(Action<LogSystemCastStartEvent> handler);
    IDisposable OnCancelCast(Action<LogSystemCancelCastEvent> handler);
    IDisposable OnInterruptCast(Action<LogSystemInterruptCastEvent> handler);
    IDisposable OnActionEffect(Action<LogSystemActionEffectEvent> handler);
    IDisposable OnCreateObject(Action<LogSystemObjectCreateEvent> handler);
    IDisposable OnNpcYell(Action<LogSystemNpcYellEvent> handler);
    IDisposable OnMapEffect(Action<LogSystemMapEffectEvent> handler);
    IDisposable OnStatusAdd(Action<LogSystemStatusAddEvent> handler);
    IDisposable OnStatusRemove(Action<LogSystemStatusRemoveEvent> handler);
    IDisposable OnTargetIcon(Action<LogSystemTargetIconEvent> handler);
    IDisposable OnTether(Action<LogSystemTetherEvent> handler);
    IDisposable OnTetherCancel(Action<LogSystemTetherCancelEvent> handler);
    IDisposable OnEObjAnimation(Action<LogSystemEObjAnimationEvent> handler);
    IDisposable OnEObjSetState(Action<LogSystemEObjSetStateEvent> handler);
    IDisposable OnPlayActionTimeline(Action<LogSystemPlayActionTimelineEvent> handler);
    IDisposable OnTargetVfx(Action<LogSystemTargetVfxEvent> handler);
    IDisposable OnSetObjPos(Action<LogSystemSetObjPosEvent> handler);
    IDisposable OnMarker(Action<LogSystemMarkerEvent> handler);
    IDisposable OnDirectorUpdate(Action<LogSystemDirectorUpdateEvent> handler);
    IDisposable OnMorelogCompat(Action<LogSystemMorelogCompatEvent> handler);

    // 插件
    IDisposable OnPluginCatalogRefreshed(Action<IReadOnlyList<PluginManifest>> handler);
}
