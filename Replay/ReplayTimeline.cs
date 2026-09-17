using PromeRotation.LogSystem;

namespace MasterPlayer.Replay;

/// <summary>时间轴泳道分类。</summary>
public enum ReplayLane
{
    Cast,       // 读条（横条）
    Effect,     // 技能生效
    Tether,     // 连线
    Status,     // 状态增减
    Mark,       // 点名/标记
    Arena,      // 场地（MapEffect/EObj/Waymark）
    Battle,     // 战斗节点（开始/结束/倒数）
    Misc,       // 其他
}

/// <summary>一段读条：Start→End（被取消/打断时截断并标记）。</summary>
public sealed record ReplayCastSpan(
    double Start,
    double End,
    ushort ActionId,
    ulong SourceId,
    string? SourceName,
    bool Cancelled,
    bool Interrupted);

/// <summary>一个时间点事件。</summary>
public sealed record ReplayTick(
    double T,
    ReplayLane Lane,
    string Kind,
    string Detail,
    ulong? SourceId,
    uint? ActionOrStatusId);

/// <summary>
/// 录制的时间轴模型：读条区间 + 分类打点 + 实体名映射。
/// 纯数据构建（不解析 Lumina 名称，名称留给 UI 层），供回放窗口时间轴与日志对比共用。
/// </summary>
public sealed class ReplayTimeline
{
    private ReplayTimeline(double duration, IReadOnlyList<ReplayCastSpan> casts, IReadOnlyList<ReplayTick> ticks,
        IReadOnlyDictionary<ulong, string> entityNames)
    {
        Duration = duration;
        Casts = casts;
        Ticks = ticks;
        EntityNames = entityNames;
    }

    public double Duration { get; }
    public IReadOnlyList<ReplayCastSpan> Casts { get; }
    public IReadOnlyList<ReplayTick> Ticks { get; }

    /// <summary>录制中出现的实体名（SourceId → 名称，来自 ObjectCreate / ActionEffect）。</summary>
    public IReadOnlyDictionary<ulong, string> EntityNames { get; }

    public static ReplayTimeline Build(ReplayRecording recording)
    {
        var entityNames = new Dictionary<ulong, string>();
        foreach (var entry in recording.Events)
        {
            switch (entry.Event)
            {
                case LogSystemObjectCreateEvent create when !string.IsNullOrEmpty(create.Name):
                    entityNames.TryAdd(create.SourceId, create.Name);
                    break;
                case LogSystemActionEffectEvent effect when !string.IsNullOrEmpty(effect.SourceName):
                    entityNames.TryAdd(effect.SourceId, effect.SourceName);
                    break;
            }
        }

        var casts = new List<ReplayCastSpan>();
        var ticks = new List<ReplayTick>();
        var cancelCandidates = new List<(double T, ulong SourceId, bool Interrupt)>();

        foreach (var entry in recording.Events)
        {
            var t = entry.T;
            switch (entry.Event)
            {
                case LogSystemCastStartEvent cast:
                    casts.Add(new ReplayCastSpan(t, t + Math.Max(cast.CastTime, 0.05), cast.ActionId, cast.SourceId,
                        entityNames.GetValueOrDefault(cast.SourceId), false, false));
                    break;
                case LogSystemCancelCastEvent cancel:
                    cancelCandidates.Add((t, cancel.SourceId, false));
                    break;
                case LogSystemInterruptCastEvent interrupt:
                    cancelCandidates.Add((t, interrupt.SourceId, true));
                    break;
            }
        }

        // 截断读条：同 SourceId 首个落在 (Start, End] 的取消/打断事件。
        for (var i = 0; i < casts.Count; i++)
        {
            var span = casts[i];
            foreach (var (ct, sourceId, interrupt) in cancelCandidates)
            {
                if (sourceId != span.SourceId || ct <= span.Start || ct > span.End)
                    continue;
                casts[i] = span with { End = ct, Cancelled = !interrupt, Interrupted = interrupt };
                break;
            }
        }

        foreach (var entry in recording.Events)
        {
            var t = entry.T;
            switch (entry.Event)
            {
                case LogSystemCastStartEvent cast:
                    ticks.Add(new ReplayTick(t, ReplayLane.Cast, "CastStart", $"#{cast.ActionId}", cast.SourceId, cast.ActionId));
                    break;
                case LogSystemCancelCastEvent cancel:
                    ticks.Add(new ReplayTick(t, ReplayLane.Cast, "CancelCast", $"#{cancel.ActionId}", cancel.SourceId, cancel.ActionId));
                    break;
                case LogSystemInterruptCastEvent interrupt:
                    ticks.Add(new ReplayTick(t, ReplayLane.Cast, "InterruptCast", $"#{interrupt.ActionId}", interrupt.SourceId, interrupt.ActionId));
                    break;
                case LogSystemActionEffectEvent effect:
                    ticks.Add(new ReplayTick(t, ReplayLane.Effect, "ActionEffect",
                        $"#{effect.ActionId} {effect.SourceName}→{effect.TargetName}", effect.SourceId, effect.ActionId));
                    break;
                case LogSystemTetherEvent tether:
                    ticks.Add(new ReplayTick(t, ReplayLane.Tether, "Tether",
                        $"连线 #{tether.TetherId}（{NameOf(tether.SourceId)}→{NameOf(tether.TargetId)}）", tether.SourceId, tether.TetherId));
                    break;
                case LogSystemTetherCancelEvent tetherCancel:
                    ticks.Add(new ReplayTick(t, ReplayLane.Tether, "TetherCancel",
                        $"断线 #{tetherCancel.TetherId}（{NameOf(tetherCancel.SourceId)}→{NameOf(tetherCancel.TargetId)}）", tetherCancel.SourceId, tetherCancel.TetherId));
                    break;
                case LogSystemStatusAddEvent statusAdd:
                    ticks.Add(new ReplayTick(t, ReplayLane.Status, "StatusAdd",
                        $"状态 #{statusAdd.StatusId} ×{statusAdd.Duration:F0}s → {NameOf(statusAdd.TargetId)}", statusAdd.TargetId, statusAdd.StatusId));
                    break;
                case LogSystemStatusRemoveEvent statusRemove:
                    ticks.Add(new ReplayTick(t, ReplayLane.Status, "StatusRemove",
                        $"移除 #{statusRemove.StatusId} ← {NameOf(statusRemove.TargetId)}", statusRemove.TargetId, statusRemove.StatusId));
                    break;
                case LogSystemTargetIconEvent icon:
                    ticks.Add(new ReplayTick(t, ReplayLane.Mark, "TargetIcon", $"点名 #{icon.MarkerId} → {NameOf(icon.TargetId)}", icon.TargetId, icon.MarkerId));
                    break;
                case LogSystemMarkerEvent marker:
                    ticks.Add(new ReplayTick(t, ReplayLane.Mark, "Marker", $"标记 #{marker.MarkerId} → {NameOf(marker.TargetId)}", marker.TargetId, marker.MarkerId));
                    break;
                case LogSystemMapEffectEvent mapEffect:
                    ticks.Add(new ReplayTick(t, ReplayLane.Arena, "MapEffect", $"位 {mapEffect.Position} 参 {mapEffect.Param1}/{mapEffect.Param2}", null, null));
                    break;
                case LogSystemEObjAnimationEvent eobjAnim:
                    ticks.Add(new ReplayTick(t, ReplayLane.Arena, "EObjAnimation", $"参 {eobjAnim.Param1}/{eobjAnim.Param2}", eobjAnim.SourceId, null));
                    break;
                case LogSystemEObjSetStateEvent eobjState:
                    ticks.Add(new ReplayTick(t, ReplayLane.Arena, "EObjSetState", $"状态 {eobjState.State}", eobjState.SourceId, null));
                    break;
                case LogSystemWaymarkEvent waymark:
                    ticks.Add(new ReplayTick(t, ReplayLane.Arena, "Waymark", $"标 {waymark.MarkerId} {(waymark.Active ? "放" : "收")}", null, null));
                    break;
                case LogSystemBattleStartedEvent:
                    ticks.Add(new ReplayTick(t, ReplayLane.Battle, "BattleStarted", "战斗开始", null, null));
                    break;
                case LogSystemBattleEndedEvent:
                    ticks.Add(new ReplayTick(t, ReplayLane.Battle, "BattleEnded", "战斗结束", null, null));
                    break;
                case LogSystemCountdownStartedEvent:
                    ticks.Add(new ReplayTick(t, ReplayLane.Battle, "CountdownStarted", "倒数开始", null, null));
                    break;
                case LogSystemChatLogEvent chat:
                    ticks.Add(new ReplayTick(t, ReplayLane.Misc, "ChatLog", $"{chat.Sender}：{chat.Message}", null, null));
                    break;
                case LogSystemNpcYellEvent yell:
                    ticks.Add(new ReplayTick(t, ReplayLane.Misc, "NpcYell", $"台词 #{yell.NpcYellId}", yell.SourceId, null));
                    break;
                case LogSystemInstanceContentTextEvent contentText:
                    ticks.Add(new ReplayTick(t, ReplayLane.Misc, "InstanceContentText", $"台词 {contentText.RowID}：{contentText.Text}", null, null));
                    break;
                case LogSystemObjectCreateEvent create:
                    ticks.Add(new ReplayTick(t, ReplayLane.Misc, "ObjectCreate", $"出现 {create.Name}（{create.DataId}）", create.SourceId, create.DataId));
                    break;
                case LogSystemObjectRemoveEvent remove:
                    ticks.Add(new ReplayTick(t, ReplayLane.Misc, "ObjectRemove", $"消失 {NameOf(remove.SourceId)}（{remove.DataId}）", remove.SourceId, remove.DataId));
                    break;
                case LogSystemTargetableEvent targetable:
                    ticks.Add(new ReplayTick(t, ReplayLane.Misc, "Targetable", $"{NameOf(targetable.SourceId)} 可选中={targetable.IsTargetable}", targetable.SourceId, null));
                    break;
                case LogSystemSetObjPosEvent setPos:
                    ticks.Add(new ReplayTick(t, ReplayLane.Misc, "SetObjPos", $"{NameOf(setPos.SourceId)} 位移 → {setPos.Position:F1}", setPos.SourceId, null));
                    break;
                case LogSystemPlayActionTimelineEvent timeline:
                    ticks.Add(new ReplayTick(t, ReplayLane.Misc, "PlayActionTimeline", $"动作 #{timeline.TimelineId}", timeline.SourceId, null));
                    break;
                case LogSystemTargetVfxEvent vfx:
                    ticks.Add(new ReplayTick(t, ReplayLane.Misc, "TargetVfx", $"特效 #{vfx.VfxId}", vfx.SourceId, null));
                    break;
                case LogSystemKnockbackEvent knockback:
                    ticks.Add(new ReplayTick(t, ReplayLane.Misc, "Knockback", $"击退 {NameOf(knockback.SourceId)} {knockback.Distance:F1}y", knockback.SourceId, null));
                    break;
                case LogSystemDirectorUpdateEvent director:
                    ticks.Add(new ReplayTick(t, ReplayLane.Misc, "DirectorUpdate", $"指令 {director.Command} 参 {director.Param1}/{director.Param2}", null, null));
                    break;
                case LogSystemTerritoryEvent territory:
                    ticks.Add(new ReplayTick(t, ReplayLane.Misc, "Territory", $"区域 {territory.TerritoryId} {territory.TerritoryName}", null, null));
                    break;
                case LogSystemChangeMapEvent changeMap:
                    ticks.Add(new ReplayTick(t, ReplayLane.Misc, "ChangeMap", $"地图 {changeMap.MapId} {changeMap.PlaceName}", null, null));
                    break;
                case LogSystemWeatherChangedEvent weather:
                    ticks.Add(new ReplayTick(t, ReplayLane.Misc, "WeatherChanged", $"天气 #{weather.WeatherId}", null, null));
                    break;
                default:
                    ticks.Add(new ReplayTick(t, ReplayLane.Misc, entry.Event.Kind.ToString(), "", null, null));
                    break;
            }
        }

        return new ReplayTimeline(recording.Duration, casts, ticks, entityNames);

        string NameOf(ulong id) => entityNames.TryGetValue(id, out var name) ? name : $"0x{id:X}";
    }
}
