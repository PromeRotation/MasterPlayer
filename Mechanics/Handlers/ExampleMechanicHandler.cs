using System;
using System.Numerics;
using ECommons.DalamudServices;
using PromeRotation.LogSystem;
using PromeRotation.Spatial.Drawing;
using PromeRotation.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Handlers;

public sealed class ExampleMechanicHandler : IMechanicHandler
{
    private const string DrawIdPrefix = "MasterPlayer_Mech_";
    private const string DrawCleanupPattern = "^MasterPlayer_Mech_";

    private MechanicDispatcher? _dispatcher;
    private bool _enabled;
    private bool _subscribed;
    private bool _disposed;

    public string Id => "example.target-icon";

    public string DisplayName => "示例：点名跟随标记";

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;
            _enabled = value;

            if (_disposed || _dispatcher == null)
                return;
            if (value)
                Subscribe();
            else
                Unsubscribe();
        }
    }

    public void Attach(MechanicDispatcher dispatcher)
    {
        if (_disposed)
            return;

        _dispatcher = dispatcher;
        if (Enabled)
            Subscribe();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            Unsubscribe();
        }
        catch (Exception ex)
        {
            SafeLogError(ex, "[MasterPlayer] ExampleMechanicHandler 退订事件失败");
        }

        try
        {
            PromeRotation.Plugin.Instance.DrawManager.RemoveByRegex(DrawCleanupPattern);
        }
        catch
        {
        }

        _dispatcher = null;
    }

    private void Subscribe()
    {
        if (_subscribed || _dispatcher == null)
            return;

        _dispatcher.TargetIcon += OnTargetIcon;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || _dispatcher == null)
            return;

        _dispatcher.TargetIcon -= OnTargetIcon;
        _subscribed = false;
    }

    private void OnTargetIcon(LogSystemTargetIconEvent ev)
    {
        if (_disposed || !Enabled || ev.TargetId > uint.MaxValue)
            return;

        var entityId = (uint)ev.TargetId;
        PromeRotation.Plugin.Instance.DrawManager.AddBound(
            $"{DrawIdPrefix}TargetIcon_{entityId:X8}",
            new CircleGeometry(Vector3.Zero, 1.5f),
            DrawBinding.Follow(entityId),
            DrawStyle.Danger,
            durationMs: 3000f);

        // 机制移动示例（演示用，保持注释）：自己被点名时让 GreenMove 向 +X 方向走 3 米
        // var me = Svc.ClientState.LocalPlayer;
        // if (me != null && ev.TargetId == me.EntityId)
        //     PromeRotation.Plugin.Instance.GreenMoveSystem.MoveTo(
        //         new PromeRotation.GreenMoveSystem.GreenMovePoint(me.Position + new Vector3(3f, 0f, 0f)));
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
