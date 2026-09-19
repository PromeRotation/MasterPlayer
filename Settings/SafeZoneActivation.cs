using PromeRotation.Spatial.SafeZone;

namespace MasterPlayer.Settings;

internal sealed class SafeZoneActivation(SafeZoneSetting setting, Func<DateTime>? utcNow = null) : IDisposable
{
    private bool _safeZoneRequested;
    private bool _autoMovementRequested;
    private bool _ownsSafeZone;
    private bool _ownsAutoMovement;
    private bool _disposed;
    private DateTime _uptimeRequest;

    public void Update(bool enableRadar, bool enableRadarOverlay, bool enableAutoDodge, bool optimizeDodgeTiming = false)
    {
        if (_disposed)
            return;

        // 本体开关被外部关闭后放弃归属，避免之后撤销用户手动重新开启的状态。
        _ownsSafeZone &= setting.EnableSafeZone;
        _ownsAutoMovement &= setting.EnableAutoMovement;

        var needsSafeZone = enableRadar || enableRadarOverlay || enableAutoDodge;
        if (needsSafeZone && !_safeZoneRequested)
        {
            _safeZoneRequested = true;
            _ownsSafeZone = !setting.EnableSafeZone;
            setting.EnableSafeZone = true;
        }

        if (enableAutoDodge && !_autoMovementRequested)
        {
            _autoMovementRequested = true;
            _ownsAutoMovement = !setting.EnableAutoMovement;
            setting.EnableAutoMovement = true;
        }
        else if (!enableAutoDodge)
        {
            ReleaseAutoMovement();
        }

        if (!needsSafeZone)
            ReleaseSafeZone();

        if (enableAutoDodge && optimizeDodgeTiming && setting.EnableSafeZone && setting.EnableAutoMovement)
            setting.AttackUptimeExpiresAt = _uptimeRequest = (utcNow?.Invoke() ?? DateTime.UtcNow).AddSeconds(0.5);
        else
            ReleaseUptime();
    }

    private void ReleaseUptime()
    {
        if (_uptimeRequest != default && setting.AttackUptimeExpiresAt == _uptimeRequest)
            setting.AttackUptimeExpiresAt = default;
        _uptimeRequest = default;
    }

    private void ReleaseAutoMovement()
    {
        if (_ownsAutoMovement)
            setting.EnableAutoMovement = false;
        _autoMovementRequested = false;
        _ownsAutoMovement = false;
    }

    private void ReleaseSafeZone()
    {
        if (_ownsSafeZone)
            setting.EnableSafeZone = false;
        _safeZoneRequested = false;
        _ownsSafeZone = false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        ReleaseUptime();
        ReleaseAutoMovement();
        ReleaseSafeZone();
    }
}
