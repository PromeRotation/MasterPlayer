namespace MasterPlayer.Spatial;

internal static class HostMovement
{
    public static bool HasControl => PromeRotation.Plugin.Instance.SafeZoneMovementStatus.HasControl
        || PromeRotation.Plugin.Instance.SafeZoneRebornMovementStatus.HasControl;

    public static bool AutomaticMovementEnabled
    {
        get
        {
            var host = PromeRotation.Plugin.Instance;
            return host.SafeZoneSetting.EnableSafeZone && host.SafeZoneSetting.EnableAutoMovement
                || host.SafeZoneRebornSetting.EnableSafeZone && host.SafeZoneRebornSetting.EnableAutoMovement;
        }
    }

    public static void DisableAutomaticMovement()
    {
        var host = PromeRotation.Plugin.Instance;
        host.SafeZoneSetting.EnableAutoMovement = false;
        host.SafeZoneRebornSetting.EnableAutoMovement = false;
    }
}
