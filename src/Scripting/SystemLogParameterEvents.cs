using System.Collections.Concurrent;
using Dalamud.Game.Chat;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using MasterPlayer.Replay;

namespace MasterPlayer.Scripting;

// PR's SystemLog event carries only the ID. Overcap needs the typed local
// parameter; copy it while Dalamud owns the queue item, then dispatch on Framework.
internal static class SystemLogParameterEvents
{
    private static readonly List<Subscription> subscriptions = [];
    private static readonly ConcurrentQueue<(Subscription Subscription, ReplaySystemLogEvent Event)> pending = new();

    internal static IDisposable Subscribe(object owner, Action<ReplaySystemLogEvent> handler, IDisposable? upstream = null)
    {
        try
        {
            var subscription = new Subscription(owner, handler, Svc.Chat, upstream);
            subscriptions.Add(subscription);
            return subscription;
        }
        catch
        {
            upstream?.Dispose();
            throw;
        }
    }

    internal static ReplaySystemLogEvent Capture(ILogMessage message, DateTime now)
    {
        var arguments = new int?[message.ParameterCount];
        for (var i = 0; i < arguments.Length; ++i)
            if (message.TryGetIntParameter(i, out var value)) arguments[i] = value;
        return new() { MessageId = message.LogMessageId, Arguments = arguments, Timestamp = now };
    }

    internal static void Drain(bool replay)
    {
        while (pending.TryDequeue(out var entry))
        {
            if (replay || entry.Subscription.Disposed) continue;
            try { entry.Subscription.Handler(entry.Event); }
            catch (Exception ex) { Svc.Log.Error(ex, "[PromeRadar] 系统日志参数订阅异常"); }
        }
    }

    internal static void UnsubscribeAll(object owner)
    {
        foreach (var subscription in subscriptions.Where(s => ReferenceEquals(s.Owner, owner)).ToArray())
            subscription.Dispose();
    }

    private sealed class Subscription : IDisposable
    {
        private readonly IChatGui chat;
        private readonly IDisposable? upstream;
        internal object Owner { get; }
        internal Action<ReplaySystemLogEvent> Handler { get; }
        internal bool Disposed { get; private set; }

        internal Subscription(object owner, Action<ReplaySystemLogEvent> handler, IChatGui chat, IDisposable? upstream)
        {
            Owner = owner;
            Handler = handler;
            this.chat = chat;
            this.upstream = upstream;
            chat.LogMessage += OnMessage;
        }

        private void OnMessage(ILogMessage message)
        {
            // Other IDs retain the existing PR ID-only route and recording.
            if (!Disposed && message.LogMessageId == 7222)
                pending.Enqueue((this, Capture(message, DateTime.UtcNow)));
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            try { chat.LogMessage -= OnMessage; }
            finally
            {
                subscriptions.Remove(this);
                upstream?.Dispose();
            }
        }
    }
}
