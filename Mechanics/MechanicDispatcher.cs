using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ECommons.DalamudServices;
using PromeRotation.Hosting;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics;

public sealed class MechanicDispatcher : IDisposable
{
    private readonly ConcurrentQueue<LogSystemCastStartEvent> _castStartQueue = new();
    private readonly ConcurrentQueue<LogSystemTetherEvent> _tetherQueue = new();
    private readonly ConcurrentQueue<LogSystemTargetIconEvent> _targetIconQueue = new();
    private readonly List<IMechanicHandler> _handlers = new();
    private readonly IDisposable _castStartSubscription;
    private readonly IDisposable _tetherSubscription;
    private readonly IDisposable _targetIconSubscription;
    private bool _disposed;

    public event Action<LogSystemCastStartEvent>? CastStart;
    public event Action<LogSystemTetherEvent>? Tether;
    public event Action<LogSystemTargetIconEvent>? TargetIcon;

    public MechanicDispatcher(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        _castStartSubscription = PromeEventBus.OnStartCast(owner, _castStartQueue.Enqueue);
        _tetherSubscription = PromeEventBus.OnTether(owner, _tetherQueue.Enqueue);
        _targetIconSubscription = PromeEventBus.OnTargetIcon(owner, _targetIconQueue.Enqueue);

        RegisterHandler(new Handlers.ExampleMechanicHandler());
    }

    public IReadOnlyList<IMechanicHandler> Handlers => _handlers;

    public void RegisterHandler(IMechanicHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (_disposed)
            return;

        _handlers.Add(handler);
        handler.Attach(this);
    }

    public void Update()
    {
        if (_disposed)
            return;

        if (!Settings.MasterPlayerSettings.Instance.EnableMechanics)
        {
            DrainQueues();
            return;
        }

        while (_castStartQueue.TryDequeue(out var castStart))
            SafeInvoke(() => CastStart?.Invoke(castStart));
        while (_tetherQueue.TryDequeue(out var tether))
            SafeInvoke(() => Tether?.Invoke(tether));
        while (_targetIconQueue.TryDequeue(out var targetIcon))
            SafeInvoke(() => TargetIcon?.Invoke(targetIcon));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        SafeDispose(_castStartSubscription);
        SafeDispose(_tetherSubscription);
        SafeDispose(_targetIconSubscription);

        foreach (var handler in _handlers)
            SafeDispose(handler);
        _handlers.Clear();

        CastStart = null;
        Tether = null;
        TargetIcon = null;

        DrainQueues();
    }

    private void DrainQueues()
    {
        while (_castStartQueue.TryDequeue(out _))
        {
        }

        while (_tetherQueue.TryDequeue(out _))
        {
        }

        while (_targetIconQueue.TryDequeue(out _))
        {
        }
    }

    private static void SafeInvoke(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            SafeLogError(ex, "[MasterPlayer] 机制事件处理异常");
        }
    }

    private static void SafeDispose(IDisposable? disposable)
    {
        try
        {
            disposable?.Dispose();
        }
        catch (Exception ex)
        {
            SafeLogError(ex, "[MasterPlayer] MechanicDispatcher 退订/释放异常");
        }
    }

    private static void SafeLogError(Exception ex, string message)
    {
        try
        {
            Svc.Log.Error(ex, message);
        }
        catch
        {
        }
    }
}
