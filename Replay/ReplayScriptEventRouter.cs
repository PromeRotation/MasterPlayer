using MasterPlayer.Scripting;
using PromeRotation.LogSystem;

namespace MasterPlayer.Replay;

/// <summary>
/// 回放事件路由：脚本订阅落在本地注册表，回放时由 ReplayController 按录制时间轴 Raise 合成事件。
/// 每脚本实例一个；派发语义对齐 LogSystemHub（按类型匹配 + 逐订阅者异常隔离，单脚本异常不影响他人）。
/// 简单事件（BattleStarted 等）由 LogSystem 记录映射补发；无对应记录的（PlayerDied/SystemLog 等）回放不会触发。
/// </summary>
internal sealed class ReplayScriptEventRouter : IScriptEventRouter
{
    private readonly List<(Type EventType, Delegate Handler)> _logHandlers = [];
    private readonly Dictionary<string, List<Delegate>> _namedHandlers = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>最近一个 handler 异常信息（供诊断展示）。</summary>
    public string? LastError { get; private set; }

    /// <summary>派发过程中累计的 handler 异常数。</summary>
    public int ErrorCount { get; private set; }

    public IDisposable Subscribe(object owner, string eventName, Delegate handler, Type? logEventType = null)
    {
        if (_disposed)
            return new Subscription(null);

        if (logEventType != null)
        {
            var entry = (logEventType, handler);
            _logHandlers.Add(entry);
            return new Subscription(() => _logHandlers.Remove(entry));
        }

        if (!_namedHandlers.TryGetValue(eventName, out var handlers))
        {
            handlers = [];
            _namedHandlers[eventName] = handlers;
        }
        handlers.Add(handler);
        return new Subscription(() => handlers.Remove(handler));
    }

    /// <summary>向本脚本的订阅者派发一个合成事件（仅主线程）。</summary>
    public void Raise(LogSystemEvent ev)
    {
        if (_disposed)
            return;

        var type = ev.GetType();
        foreach (var (registered, handler) in _logHandlers.ToArray())
            if (registered.IsAssignableFrom(type))
                InvokeSafe(handler, [ev]);

        if (SimpleEventMappings.TryGetValue(type, out var mapping))
            mapping(this, ev);
    }

    private void InvokeNamed(string name, params object?[] args)
    {
        if (!_namedHandlers.TryGetValue(name, out var handlers))
            return;
        foreach (var handler in handlers.ToArray())
            InvokeSafe(handler, args);
    }

    private void InvokeSafe(Delegate handler, object?[] args)
    {
        try
        {
            handler.DynamicInvoke(args);
        }
        catch (Exception ex)
        {
            ErrorCount++;
            LastError = ex.InnerException?.Message ?? ex.Message;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _logHandlers.Clear();
        _namedHandlers.Clear();
    }

    private sealed class Subscription(Action? remove) : IDisposable
    {
        private Action? _remove = remove;

        public void Dispose()
        {
            _remove?.Invoke();
            _remove = null;
        }
    }

    // 录制只覆盖 LogSystem 事件流；脚本侧的简单事件订阅由对应 LogSystem 记录映射补发。
    // EventManager/LogMessageManager 独有的简单事件（PlayerDied/PlayerRevived/PlayerMoveStateChanged/
    // SystemLog/BattleLog/ShowMessage）没有 LogSystem 对应记录，回放中不会触发。
    private static readonly Dictionary<Type, Action<ReplayScriptEventRouter, LogSystemEvent>> SimpleEventMappings = new()
    {
        [typeof(LogSystemBattleStartedEvent)] = static (r, _) => r.InvokeNamed("BattleStarted"),
        [typeof(LogSystemBattleEndedEvent)] = static (r, _) => r.InvokeNamed("BattleEnded"),
        [typeof(LogSystemCountdownStartedEvent)] = static (r, _) => r.InvokeNamed("CountdownStarted"),
        [typeof(LogSystemWeatherChangedEvent)] = static (r, e) => r.InvokeNamed("WeatherChanged", ((LogSystemWeatherChangedEvent)e).WeatherId),
        [typeof(LogSystemTerritoryEvent)] = static (r, e) => r.InvokeNamed("TerritoryChanged", (ushort)((LogSystemTerritoryEvent)e).TerritoryId),
        [typeof(LogSystemChatLogEvent)] = static (r, e) =>
        {
            var chat = (LogSystemChatLogEvent)e;
            r.InvokeNamed("ChatLog", chat.Sender, chat.Message, chat.LogKind);
        },
    };
}
