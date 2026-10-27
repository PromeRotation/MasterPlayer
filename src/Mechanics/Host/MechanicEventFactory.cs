using MasterPlayer.MechanicRuntime;
using PromeRotation.LogSystem;

namespace MasterPlayer.MechanicHost;

internal static class MechanicEventFactory
{
    public static MechanicFeedEvent FromTether(LogSystemTetherEvent e)
        => new MechanicEventTether(e.SourceId, e.TetherId, e.TargetId);

    public static MechanicFeedEvent FromTetherCancel(LogSystemTetherCancelEvent e)
        => new MechanicEventTetherCancel(e.SourceId);

    public static MechanicFeedEvent FromTargetIcon(LogSystemTargetIconEvent e)
        => new MechanicEventIcon(e.MarkerId, e.TargetId, e.SourceId);

    public static MechanicFeedEvent FromTargetVfx(LogSystemTargetVfxEvent e)
        => new MechanicEventVfx(e.VfxId, 0, e.SourceId);

    public static MechanicFeedEvent? FromMarker(LogSystemMarkerEvent e)
        => e.MarkerId is >= 1 and <= (uint)BossMod.Sign.Count
            ? new MechanicEventHeadMarker((byte)(e.MarkerId - 1), e.TargetId)
            : null;

    public static MechanicFeedEvent FromMapEffect(LogSystemMapEffectEvent e)
        => new MechanicEventMapEffect((byte)e.Position, e.Param1 | ((uint)e.Param2 << 16));

    public static MechanicFeedEvent FromEObjAnimation(LogSystemEObjAnimationEvent e)
        => new MechanicEventEObjAnimation(e.SourceId, e.Param1, e.Param2);

    public static MechanicFeedEvent FromEObjSetState(LogSystemEObjSetStateEvent e)
        => new MechanicEventEObjSetState(e.SourceId, e.State);

    public static MechanicFeedEvent FromDirectorUpdate(LogSystemDirectorUpdateEvent e)
        => new MechanicEventDirectorUpdate(e.Instance, e.Command, e.Param1, e.Param2, e.Param3, e.Param4);

    public static MechanicFeedEvent FromNpcYell(LogSystemNpcYellEvent e)
        => new MechanicEventNpcYell(e.SourceId, e.NpcYellId);

    public static MechanicFeedEvent FromSystemLog(uint messageId)
        => new MechanicEventSystemLog(messageId, []);

    public static MechanicFeedEvent? FromPlayActionTimeline(LogSystemPlayActionTimelineEvent e)
        => e.TimelineId <= ushort.MaxValue ? new MechanicEventPlayActionTimeline(e.SourceId, (ushort)e.TimelineId) : null;
}
