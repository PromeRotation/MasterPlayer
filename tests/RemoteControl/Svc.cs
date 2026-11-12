using MasterPlayer.RemoteControlTests;

namespace ECommons.DalamudServices;

public static class Svc
{
    public static TestPluginInterface PluginInterface { get; } = new();
    public static TestLog Log { get; } = new();
}
