using MasterPlayer.RemoteControlTests;

namespace ECommons.DalamudServices;

public static class Svc
{
    public static Dalamud.Plugin.Services.ICondition? Condition { get; set; }
    public static TestPluginInterface PluginInterface { get; } = new();
    public static TestLog Log { get; } = new();
}
