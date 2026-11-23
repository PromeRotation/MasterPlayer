namespace MasterPlayer.Scripting;

internal static class NativeActionEffectEvents
{
    private static readonly List<Subscription> subscriptions = [];

    internal static IDisposable Subscribe(object owner, Action<NativeActionEffectEvent> handler, IDisposable? upstream = null)
    {
        var subscription = new Subscription(owner, handler, upstream);
        subscriptions.Add(subscription);
        return subscription;
    }

    internal static void Publish(NativeActionEffectEvent ev, Action<Exception> onError)
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

    private sealed class Subscription(object owner, Action<NativeActionEffectEvent> handler, IDisposable? upstream) : IDisposable
    {
        internal object Owner { get; } = owner;
        internal Action<NativeActionEffectEvent> Handler { get; } = handler;
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
