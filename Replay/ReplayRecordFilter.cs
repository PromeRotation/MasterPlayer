using PromeRotation.LogSystem;

namespace MasterPlayer.Replay;

/// <summary>
/// 录制过滤：来源是玩家（PC）或玩家召唤物（OwnerId 属于 PC）的动作/状态/读条不写入录制；
/// 聊天日志（ChatLog）一律不录制。
/// 连线/点名/场地/战斗等其他事件不受影响；SourceId 无法判定（0 或超出实体范围）的一律放行，避免丢机制。
/// </summary>
public static class ReplayRecordFilter
{
    /// <summary>isPlayerOwnedSource 返回 true 表示该来源属于玩家或其召唤物。</summary>
    public static bool ShouldRecord(LogSystemEvent ev, Func<ulong, bool> isPlayerOwnedSource)
        => ev switch
        {
            LogSystemChatLogEvent => false,
            LogSystemCastStartEvent e => !isPlayerOwnedSource(e.SourceId),
            LogSystemCancelCastEvent e => !isPlayerOwnedSource(e.SourceId),
            LogSystemInterruptCastEvent e => !isPlayerOwnedSource(e.SourceId),
            LogSystemActionEffectEvent e => !isPlayerOwnedSource(e.SourceId),
            LogSystemStatusAddEvent e => !isPlayerOwnedSource(e.SourceId),
            LogSystemStatusRemoveEvent e => !isPlayerOwnedSource(e.SourceId),
            _ => true,
        };
}
