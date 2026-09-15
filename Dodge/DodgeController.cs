using System;
using System.Collections.Generic;
using System.Numerics;
using ECommons.DalamudServices;
using MasterPlayer.Settings;
using PromeRotation.Hosting;
using PromeRotation.LogSystem;
using PromeRotation.Spatial.SafeZone;

namespace MasterPlayer.Dodge;

public sealed class DodgeController : IDisposable
{
    private static readonly TimeSpan PruneInterval = TimeSpan.FromSeconds(0.5);

    private readonly List<SafeZoneAreaHandle> _registeredHandles = new();
    private readonly IDisposable _startCastSubscription;
    private DateTime _nextPruneUtc = DateTime.MinValue;
    private bool _disposed;

    public DodgeController()
    {
        _startCastSubscription = PromeEventBus.OnStartCast(this, HandleStartCast);
    }

    // PromeEventBus 在主线程派发（底层是 Plugin.Instance.LogSystem.Events），回调里直接注册危险区。
    private void HandleStartCast(LogSystemCastStartEvent e)
    {
        if (_disposed || !MasterPlayerSettings.Instance.EnableAutoDodge)
        {
            return;
        }

        ProcessCast(e);
    }

    public void Update()
    {
        if (_disposed)
        {
            return;
        }

        PruneHandles();
    }

    private void ProcessCast(LogSystemCastStartEvent e)
    {
        if (!AoeShapeDatabase.TryGetShape(e.ActionId, out var descriptor) || descriptor is null)
        {
            return;
        }

        var template = CreateTemplate(descriptor);
        if (template is null)
        {
            return;
        }

        template = DangerMarginInflator.Inflate(template, MasterPlayerSettings.Instance.DodgeDangerMargin);

        var sourceId = (uint)e.SourceId;
        var binding = descriptor.FollowCaster && sourceId != 0u
            ? (TransformBinding)new FollowEntityPositionAndRotation(sourceId, Vector2.Zero, MissingEntityPolicy.KeepLastResolved)
            : new StaticTransform(e.EffectPosition, e.SourceRotation);
        var delaySeconds = MathF.Max(0f, e.CastTime + descriptor.ExtraDelaySeconds);

        try
        {
            var handle = PromeRotation.Plugin.Instance.SafeZoneRegistry.AddDelayedHit(template, binding, delaySeconds);
            if (handle.IsValid)
            {
                _registeredHandles.Add(handle);
                if (MasterPlayerSettings.Instance.DebugMode)
                {
                    Svc.Log.Info($"[MasterPlayer] 注册躲避危险区 ActionId={e.ActionId} Kind={descriptor.Kind} Delay={delaySeconds:0.0}s");
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"[MasterPlayer] 注册躲避危险区失败 (ActionId={e.ActionId})");
        }
    }

    private static GeometryTemplate? CreateTemplate(AoeShapeDescriptor descriptor)
    {
        switch (descriptor.Kind)
        {
            case AoeShapeKind.Circle when descriptor.Param1 > 0f:
                return new CircleTemplate(descriptor.Param1);
            case AoeShapeKind.Donut when descriptor.Param1 >= 0f && descriptor.Param2 > descriptor.Param1:
                return new DonutTemplate(descriptor.Param1, descriptor.Param2);
            case AoeShapeKind.Fan when descriptor.Param1 > 0f && descriptor.Param2 > 0f:
                return new FanTemplate(descriptor.Param1, MathF.Min(descriptor.Param2, MathF.PI * 2f));
            case AoeShapeKind.Rectangle when descriptor.Param1 > 0f && descriptor.Param2 > 0f:
                return new RectangleTemplate(descriptor.Param1, descriptor.Param2);
            default:
                return null;
        }
    }

    private void PruneHandles()
    {
        if (_registeredHandles.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now < _nextPruneUtc)
        {
            return;
        }
        _nextPruneUtc = now + PruneInterval;

        try
        {
            var manager = PromeRotation.Plugin.Instance.SafeZoneManager;
            for (var i = _registeredHandles.Count - 1; i >= 0; i--)
            {
                if (!manager.ContainsArea(_registeredHandles[i]))
                {
                    _registeredHandles.RemoveAt(i);
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[MasterPlayer] 清理失效躲避危险区句柄失败");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        try
        {
            _startCastSubscription.Dispose();
        }
        catch (Exception ex)
        {
            SafeLogError(ex, "[MasterPlayer] 退订 OnStartCast 失败");
        }

        try
        {
            var registry = PromeRotation.Plugin.Instance.SafeZoneRegistry;
            foreach (var handle in _registeredHandles)
            {
                try
                {
                    registry.Remove(handle);
                }
                catch (Exception ex)
                {
                    SafeLogError(ex, "[MasterPlayer] 移除躲避危险区失败");
                }
            }
        }
        catch (Exception ex)
        {
            SafeLogError(ex, "[MasterPlayer] 获取 SafeZoneRegistry 失败，残留危险区由本体生命周期事件清理");
        }

        _registeredHandles.Clear();
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
