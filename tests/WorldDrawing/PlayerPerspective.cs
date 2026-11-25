namespace MasterPlayer;

// 离屏专项不启动游戏会话；生产端的回放状态由已有统一入口提供。
internal static class PlayerPerspective
{
    internal static bool IsReplay { get; set; }
}
