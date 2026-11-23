namespace MasterPlayer.Scripting;

/// <summary>仅主线程使用；补充宿主尚未发布的 Director UnionData 事件。</summary>
internal static class LegacyMapEffectEvents
{
    private static readonly List<Subscription> subscriptions = [];

    internal static IDisposable Subscribe(object owner, Action<LegacyMapEffectEvent> handler, IDisposable? upstream = null)
    {
        var subscription = new Subscription(owner, handler, upstream);
        subscriptions.Add(subscription);
        return subscription;
    }

    internal static void Publish(LegacyMapEffectEvent ev, Action<Exception> onError)
    {
        foreach (var subscription in subscriptions.ToArray())
        {
            if (subscription.Disposed) continue;
            try { subscription.Handler(ev); }
            catch (Exception ex) { onError(ex); }
        }
    }

    internal static void UnsubscribeAll(object owner)
    {
        foreach (var subscription in subscriptions.Where(s => ReferenceEquals(s.Owner, owner)).ToArray())
            subscription.Dispose();
    }

    private sealed class Subscription(object owner, Action<LegacyMapEffectEvent> handler, IDisposable? upstream) : IDisposable
    {
        internal object Owner { get; } = owner;
        internal Action<LegacyMapEffectEvent> Handler { get; } = handler;
        internal bool Disposed { get; private set; }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            subscriptions.Remove(this);
            upstream?.Dispose();
        }
    }
}
