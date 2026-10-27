namespace MasterPlayer.Spatial;

internal static class HostMovement
{
    private static readonly System.Reflection.PropertyInfo? RebornSetting = typeof(PromeRotation.Plugin).GetProperty("SafeZoneRebornSetting");
    private static readonly System.Reflection.PropertyInfo? RebornStatus = typeof(PromeRotation.Plugin).GetProperty("SafeZoneRebornMovementStatus");
    private static readonly System.Reflection.PropertyInfo? RebornHasControl = RebornStatus?.PropertyType.GetProperty("HasControl");

    public static bool HasControl => PromeRotation.Plugin.Instance.SafeZoneMovementStatus.HasControl
        || RebornStatus?.GetValue(PromeRotation.Plugin.Instance) is { } status && RebornHasControl?.GetValue(status) is true;

    public static bool AutomaticMovementEnabled
    {
        get
        {
            var host = PromeRotation.Plugin.Instance;
            var reborn = RebornSetting?.GetValue(host) as PromeRotation.Spatial.SafeZone.SafeZoneSetting;
            return host.SafeZoneSetting.EnableSafeZone && host.SafeZoneSetting.EnableAutoMovement
                || reborn is { EnableSafeZone: true, EnableAutoMovement: true };
        }
    }

    public static void DisableAutomaticMovement()
    {
        var host = PromeRotation.Plugin.Instance;
        host.SafeZoneSetting.EnableAutoMovement = false;
        if (RebornSetting?.GetValue(host) is PromeRotation.Spatial.SafeZone.SafeZoneSetting reborn)
            reborn.EnableAutoMovement = false;
    }
}
