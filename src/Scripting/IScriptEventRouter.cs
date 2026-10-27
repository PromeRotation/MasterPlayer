namespace MasterPlayer.Scripting;

/// <summary>
/// 脚本事件路由器：MasterPlayerScriptContext 的全部事件订阅经此抽象落地。
/// 实时模式由 <see cref="LiveScriptEventRouter"/> 转发 PromeEventBus；
/// 回放模式由 <see cref="Replay.ReplayScriptEventRouter"/> 本地注册、按录制事件派发。
/// eventName 与 PromeEventBus.Subscribe 的字符串名一致（"StartCast" 等）；
/// logEventType 为 LogSystem 强类型事件的 record 类型（含 LogEvent 兜底/泛型订阅），简单事件为 null。
/// </summary>
internal interface IScriptEventRouter : IDisposable
{
    IDisposable Subscribe(object owner, string eventName, Delegate handler, Type? logEventType = null);
}
