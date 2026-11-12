namespace MasterPlayer.Settings;

public sealed class MasterPlayerSettings
{
    public static MasterPlayerSettings Instance { get; } = new();
    public bool EnableSmartMovement { get; set; }
}
