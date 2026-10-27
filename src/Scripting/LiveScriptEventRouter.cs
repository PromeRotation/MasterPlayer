using System.Reflection;
using PromeRotation.Hosting;
using PromeRotation.LogSystem;

namespace MasterPlayer.Scripting;

/// <summary>
/// 实时事件路由：把脚本订阅转发到 PromeEventBus（owner 为脚本上下文，清理仍走 UnsubscribeAll）。
/// 单例无状态；Dispose 是空操作，绝不释放共享实例。
/// </summary>
internal sealed class LiveScriptEventRouter : IScriptEventRouter
{
    internal static readonly LiveScriptEventRouter Instance = new();

    private static readonly MethodInfo GenericOnLogEvent = typeof(PromeEventBus)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(PromeEventBus.OnLogEvent) && m.IsGenericMethodDefinition);

    private static readonly Dictionary<Type, Func<object, Delegate, IDisposable>> TypedSubscriptions = new()
    {
        [typeof(LogSystemEvent)] = static (o, h) => PromeEventBus.OnLogEvent(o, (Action<LogSystemEvent>)h),
        [typeof(LogSystemCastStartEvent)] = static (o, h) => PromeEventBus.OnStartCast(o, (Action<LogSystemCastStartEvent>)h),
        [typeof(LogSystemCancelCastEvent)] = static (o, h) => PromeEventBus.OnCancelCast(o, (Action<LogSystemCancelCastEvent>)h),
        [typeof(LogSystemInterruptCastEvent)] = static (o, h) => PromeEventBus.OnInterruptCast(o, (Action<LogSystemInterruptCastEvent>)h),
        [typeof(LogSystemActionEffectEvent)] = static (o, h) => PromeEventBus.OnActionEffect(o, (Action<LogSystemActionEffectEvent>)h),
        [typeof(LogSystemObjectCreateEvent)] = static (o, h) => PromeEventBus.OnCreateObject(o, (Action<LogSystemObjectCreateEvent>)h),
        [typeof(LogSystemNpcYellEvent)] = static (o, h) => PromeEventBus.OnNpcYell(o, (Action<LogSystemNpcYellEvent>)h),
        [typeof(LogSystemMapEffectEvent)] = static (o, h) => PromeEventBus.OnMapEffect(o, (Action<LogSystemMapEffectEvent>)h),
        [typeof(LogSystemStatusAddEvent)] = static (o, h) => PromeEventBus.OnStatusAdd(o, (Action<LogSystemStatusAddEvent>)h),
        [typeof(LogSystemStatusRemoveEvent)] = static (o, h) => PromeEventBus.OnStatusRemove(o, (Action<LogSystemStatusRemoveEvent>)h),
        [typeof(LogSystemTargetIconEvent)] = static (o, h) => PromeEventBus.OnTargetIcon(o, (Action<LogSystemTargetIconEvent>)h),
        [typeof(LogSystemTetherEvent)] = static (o, h) => PromeEventBus.OnTether(o, (Action<LogSystemTetherEvent>)h),
        [typeof(LogSystemTetherCancelEvent)] = static (o, h) => PromeEventBus.OnTetherCancel(o, (Action<LogSystemTetherCancelEvent>)h),
        [typeof(LogSystemEObjAnimationEvent)] = static (o, h) => PromeEventBus.OnEObjAnimation(o, (Action<LogSystemEObjAnimationEvent>)h),
        [typeof(LogSystemEObjSetStateEvent)] = static (o, h) => PromeEventBus.OnEObjSetState(o, (Action<LogSystemEObjSetStateEvent>)h),
        [typeof(LogSystemPlayActionTimelineEvent)] = static (o, h) => PromeEventBus.OnPlayActionTimeline(o, (Action<LogSystemPlayActionTimelineEvent>)h),
        [typeof(LogSystemTargetVfxEvent)] = static (o, h) => PromeEventBus.OnTargetVfx(o, (Action<LogSystemTargetVfxEvent>)h),
        [typeof(LogSystemSetObjPosEvent)] = static (o, h) => PromeEventBus.OnSetObjPos(o, (Action<LogSystemSetObjPosEvent>)h),
        [typeof(LogSystemMarkerEvent)] = static (o, h) => PromeEventBus.OnMarker(o, (Action<LogSystemMarkerEvent>)h),
        [typeof(LogSystemDirectorUpdateEvent)] = static (o, h) => PromeEventBus.OnDirectorUpdate(o, (Action<LogSystemDirectorUpdateEvent>)h),
        [typeof(LogSystemMorelogCompatEvent)] = static (o, h) => PromeEventBus.OnMorelogCompat(o, (Action<LogSystemMorelogCompatEvent>)h),
    };

    private LiveScriptEventRouter()
    {
    }

    public IDisposable Subscribe(object owner, string eventName, Delegate handler, Type? logEventType = null)
    {
        if (logEventType == null)
            return PromeEventBus.Subscribe(owner, eventName, handler);

        if (TypedSubscriptions.TryGetValue(logEventType, out var subscribe))
            return subscribe(owner, handler);

        // 无专门入口的 LogSystem 子类型（如 Waymark/TimelineExecution 的泛型订阅）：走泛型 OnLogEvent<TEvent>。
        return (IDisposable)GenericOnLogEvent.MakeGenericMethod(logEventType).Invoke(null, [owner, handler])!;
    }

    public void Dispose()
    {
    }
}
