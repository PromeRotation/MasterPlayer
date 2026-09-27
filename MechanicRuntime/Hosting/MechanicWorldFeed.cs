using BossMod;

namespace MasterPlayer.MechanicRuntime;

public sealed record MechanicCastSnapshot(uint ActionId, bool IsSpell, ulong TargetId, float Rotation, Vector3 Location, float TotalTime, bool Interruptible);
public sealed record MechanicStatusDto(int Slot, uint Id, ushort Extra, ulong SourceId, float RemainingSeconds);
public sealed record MechanicActorSnapshot(ulong EntityId, uint OID, int SpawnIndex, uint LayoutID, string Name, uint NameID,
    byte ObjectKind, byte SubKind, uint ClassJobId, int Level, Vector3 Position, float Rotation, float HitboxRadius,
    uint CurHp, uint MaxHp, byte ShieldPercent, uint CurMp, uint MaxMp,
    bool IsTargetable, bool IsAlly, bool IsDead, bool InCombat,
    ulong OwnerId, ulong TargetId, uint FateId, int Renderflags, byte EventState,
    byte ModelState, byte AnimState1, byte AnimState2,
    uint TetherId, ulong TetherTargetId, MechanicCastSnapshot? Cast, MechanicStatusDto[] Statuses);
public sealed record MechanicPartySlotDto(int Slot, ulong ContentId, ulong InstanceId, string Name);
public sealed record MechanicIncomingEffectSlot(int Slot, uint GlobalSequence, int TargetIndex, byte ActionType, uint ActionId,
    ulong SourceId, bool TargetConfirmed, ulong[] RawEffects);
public sealed record MechanicActorIncomingEffects(ulong EntityId, MechanicIncomingEffectSlot[] Slots);
public sealed record MechanicWorldFrame(ushort Territory, ushort ContentFinderConditionId, ulong LocalPlayerId, ulong TargetId,
    MechanicPartySlotDto[] Party, (byte Id, Vector3? Pos)[] Waymarks, MechanicActorSnapshot[] Actors)
{
    // 逐目标原生 IncomingEffects 槽位（仅实时采样提供；回放录制未存该数据，保持为空）
    public MechanicActorIncomingEffects[] IncomingEffects { get; init; } = [];
}

public abstract record MechanicFeedEvent;
public sealed record MechanicEventTether(ulong SourceId, uint TetherId, ulong TargetId) : MechanicFeedEvent;
public sealed record MechanicEventTetherCancel(ulong SourceId) : MechanicFeedEvent;
public sealed record MechanicEventIcon(uint IconId, ulong TargetId, ulong SourceId) : MechanicFeedEvent;
public sealed record MechanicEventVfx(uint VfxId, ulong TargetId, ulong SourceId) : MechanicFeedEvent;
public sealed record MechanicEventHeadMarker(byte SignId, ulong TargetId) : MechanicFeedEvent;
public sealed record MechanicEventMapEffect(byte Index, uint State) : MechanicFeedEvent;
public sealed record MechanicEventDirectorUpdate(uint DirectorID, uint UpdateID, uint Param1, uint Param2, uint Param3, uint Param4) : MechanicFeedEvent;
public sealed record MechanicEventEObjAnimation(ulong SourceId, ushort Param1, ushort Param2) : MechanicFeedEvent;
public sealed record MechanicEventEObjSetState(ulong SourceId, ushort State) : MechanicFeedEvent;
public sealed record MechanicEventNpcYell(ulong SourceId, ushort Message) : MechanicFeedEvent;
public sealed record MechanicEventSystemLog(uint MessageId, int[] Args) : MechanicFeedEvent;
public sealed record MechanicEventPlayActionTimeline(ulong SourceId, ushort TimelineId) : MechanicFeedEvent;
public sealed record MechanicActionEffectTarget(ulong Id, ulong[] RawEffects);
public sealed record MechanicEventActionEffect(ulong CasterId, byte ActionType, uint ActionId, ulong MainTargetId, float AnimationLock,
    Vector3 TargetPos, uint GlobalSequence, uint SourceSequence, ushort RotationInt, MechanicActionEffectTarget[] Targets) : MechanicFeedEvent;

public sealed class MechanicWorldFeed
{
    private const ulong InvalidEntityId = 0xE0000000;
    private const float MaxStatusRemainingSeconds = 100000f;

    private readonly MechanicSession _session;
    private readonly List<MechanicFeedEvent> _pendingEvents = [];

    private sealed class TrackedActor(MechanicActorSnapshot snapshot)
    {
        public MechanicActorSnapshot Snapshot = snapshot;
        public readonly ActorStatus[] Statuses = new ActorStatus[Actor.NumStatuses];
        public readonly (uint Seq, int TargetIndex, bool Confirmed)[] Incoming = new (uint, int, bool)[Actor.NumIncomingEffects];
    }

    private readonly Dictionary<ulong, TrackedActor> _tracked = [];

    public MechanicWorldFeed(MechanicSession session) => _session = session;

    private WorldState World => _session.World;

    public void ApplyFrame(MechanicWorldFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (World.CurrentZone != frame.Territory || World.CurrentCFCID != frame.ContentFinderConditionId)
            _session.Execute(new WorldState.OpZoneChange(frame.Territory, frame.ContentFinderConditionId));

        foreach (var (id, pos) in frame.Waymarks)
        {
            if (id >= (byte)Waymark.Count)
                continue;
            var wm = (Waymark)id;
            if (World.Waymarks[wm] != pos)
                _session.Execute(new WaymarkState.OpWaymarkChange(wm, pos));
        }

        var present = new HashSet<ulong>(frame.Actors.Length);
        foreach (var actor in frame.Actors)
            present.Add(actor.EntityId);
        List<ulong>? removed = null;
        foreach (var id in _tracked.Keys)
        {
            if (!present.Contains(id))
                (removed ??= []).Add(id);
        }
        if (removed != null)
        {
            foreach (var id in removed)
            {
                _session.Execute(new ActorState.OpDestroy(id));
                _tracked.Remove(id);
            }
        }

        foreach (var actor in frame.Actors)
        {
            MechanicIncomingEffectSlot[]? incoming = null;
            foreach (var entry in frame.IncomingEffects)
            {
                if (entry.EntityId == actor.EntityId)
                {
                    incoming = entry.Slots;
                    break;
                }
            }
            ApplyActor(actor, incoming);
        }

        for (var slot = 0; slot < PartyState.MaxPartySize; ++slot)
        {
            var dto = frame.Party.FirstOrDefault(p => p.Slot == slot);
            var member = dto != null ? new PartyState.Member(dto.ContentId, dto.InstanceId, false, dto.Name) : PartyState.EmptySlot;
            if (World.Party.Members[slot] != member)
                _session.Execute(new PartyState.OpModify(slot, member));
        }
    }

    public void Enqueue(MechanicFeedEvent feedEvent) => _pendingEvents.Add(feedEvent);

    public int Flush()
    {
        var count = _pendingEvents.Count;
        for (var i = 0; i < count; ++i)
            ApplyEvent(_pendingEvents[i]);
        _pendingEvents.Clear();
        return count;
    }

    public void ApplyEvent(MechanicFeedEvent feedEvent)
    {
        switch (feedEvent)
        {
            case MechanicEventTether e:
                _session.Execute(new ActorState.OpTether(e.SourceId, new(e.TetherId, Sanitize(e.TargetId))));
                break;
            case MechanicEventTetherCancel e:
                _session.Execute(new ActorState.OpTether(e.SourceId, default));
                break;
            case MechanicEventIcon e:
                _session.Execute(new ActorState.OpIcon(e.SourceId, e.IconId - BossMod.Network.IDScramble.Delta, e.TargetId));
                break;
            case MechanicEventVfx e:
                _session.Execute(new ActorState.OpVFX(e.SourceId, e.VfxId, e.TargetId));
                break;
            case MechanicEventHeadMarker e:
                if (e.SignId < (byte)Sign.Count)
                    _session.Execute(new WaymarkState.OpSignChange((Sign)e.SignId, Sanitize(e.TargetId)));
                break;
            case MechanicEventMapEffect e:
                _session.Execute(new WorldState.OpMapEffect(e.Index, e.State));
                break;
            case MechanicEventDirectorUpdate e:
                _session.Execute(new WorldState.OpDirectorUpdate(e.DirectorID, e.UpdateID, e.Param1, e.Param2, e.Param3, e.Param4));
                break;
            case MechanicEventEObjAnimation e:
                _session.Execute(new ActorState.OpEventObjectAnimation(e.SourceId, e.Param1, e.Param2));
                break;
            case MechanicEventEObjSetState e:
                _session.Execute(new ActorState.OpEventObjectStateChange(e.SourceId, e.State));
                break;
            case MechanicEventNpcYell e:
                _session.Execute(new ActorState.OpEventNpcYell(e.SourceId, e.Message));
                break;
            case MechanicEventSystemLog e:
                _session.Execute(new WorldState.OpSystemLogMessage(e.MessageId, e.Args));
                break;
            case MechanicEventPlayActionTimeline e:
                _session.Execute(new ActorState.OpPlayActionTimelineEvent(e.SourceId, e.TimelineId));
                break;
            case MechanicEventActionEffect e:
                ApplyActionEffect(e);
                break;
        }
    }

    private void ApplyActionEffect(MechanicEventActionEffect e)
    {
        var info = new ActorCastEvent(new((ActionType)e.ActionType, e.ActionId), e.MainTargetId, e.AnimationLock,
            (uint)e.Targets.Length, e.TargetPos, e.GlobalSequence, e.SourceSequence, BossMod.Network.PacketDecoder.IntToFloatAngle(e.RotationInt));
        foreach (var target in e.Targets)
        {
            var effects = new ActionEffects();
            var raw = target.RawEffects;
            var count = Math.Min(raw.Length, ActionEffects.MaxCount);
            for (var j = 0; j < count; ++j)
                effects[j] = raw[j];
            info.Targets.Add(new(target.Id, effects));
        }
        _session.Execute(new ActorState.OpCastEvent(e.CasterId, info));
    }

    private void ApplyActor(MechanicActorSnapshot cur, MechanicIncomingEffectSlot[]? incoming)
    {
        if (!_tracked.TryGetValue(cur.EntityId, out var tracked))
        {
            var posRot = new Vector4(cur.Position, cur.Rotation);
            _session.Execute(new ActorState.OpCreate(cur.EntityId, cur.OID, cur.SpawnIndex, cur.LayoutID, cur.Name, cur.NameID,
                (ActorType)(cur.ObjectKind << 8 | cur.SubKind), (Class)cur.ClassJobId, cur.Level, posRot, cur.HitboxRadius,
                BuildHpmp(cur), cur.IsTargetable, cur.IsAlly, Sanitize(cur.OwnerId), cur.FateId, cur.Renderflags));
            tracked = new(cur);
            _tracked.Add(cur.EntityId, tracked);
            if (cur.TetherId != 0)
                _session.Execute(new ActorState.OpTether(cur.EntityId, new(cur.TetherId, Sanitize(cur.TetherTargetId))));
            ApplySharedDiffs(tracked, cur, null);
            ApplyIncomingEffects(tracked, cur.EntityId, incoming);
            return;
        }

        var prev = tracked.Snapshot;
        var id = cur.EntityId;
        if (prev.Name != cur.Name || prev.NameID != cur.NameID)
            _session.Execute(new ActorState.OpRename(id, cur.Name, cur.NameID));
        if (prev.ClassJobId != cur.ClassJobId || prev.Level != cur.Level)
            _session.Execute(new ActorState.OpClassChange(id, (Class)cur.ClassJobId, cur.Level));
        var curPosRot = new Vector4(cur.Position, cur.Rotation);
        if (new Vector4(prev.Position, prev.Rotation) != curPosRot)
            _session.Execute(new ActorState.OpMove(id, curPosRot));
        if (prev.HitboxRadius != cur.HitboxRadius)
            _session.Execute(new ActorState.OpSizeChange(id, cur.HitboxRadius));
        var hpmp = BuildHpmp(cur);
        if (BuildHpmp(prev) != hpmp)
            _session.Execute(new ActorState.OpHPMP(id, hpmp));
        if (prev.IsTargetable != cur.IsTargetable)
            _session.Execute(new ActorState.OpTargetable(id, cur.IsTargetable));
        if (prev.IsAlly != cur.IsAlly)
            _session.Execute(new ActorState.OpAlly(id, cur.IsAlly));
        if (prev.Renderflags != cur.Renderflags)
            _session.Execute(new ActorState.OpRenderflags(id, cur.Renderflags));

        ApplySharedDiffs(tracked, cur, prev);

        var actor = World.Actors.Find(id);
        var tether = new ActorTetherInfo(cur.TetherId, Sanitize(cur.TetherTargetId));
        if (actor != null && (actor.Tether.ID != tether.ID || actor.Tether.Target != tether.Target))
            _session.Execute(new ActorState.OpTether(id, tether));

        tracked.Snapshot = cur;
        ApplyIncomingEffects(tracked, id, incoming);
    }

    // 逐目标原生 IncomingEffects 槽位差分（对齐 BMR WorldStateGameSync 的槽位语义）：
    // 槽位值变化 → OpIncomingEffect；TargetConfirmed 翻转为已确认 → OpEffectResult（加速待定 HP/状态确认）；
    // 本帧消失的非零槽位 → 写入空值（游戏侧已清除，触发待击退等清理）。
    private void ApplyIncomingEffects(TrackedActor tracked, ulong id, MechanicIncomingEffectSlot[]? incoming)
    {
        var trackedSlots = tracked.Incoming;
        Span<bool> present = stackalloc bool[trackedSlots.Length];
        if (incoming != null)
        {
            foreach (var slot in incoming)
            {
                var i = slot.Slot;
                if ((uint)i >= (uint)trackedSlots.Length)
                    continue;
                present[i] = true;
                var prev = trackedSlots[i];
                var cur = (slot.GlobalSequence, slot.TargetIndex, slot.TargetConfirmed);
                if (prev == cur)
                    continue;
                if (prev.Seq == slot.GlobalSequence && prev.TargetIndex == slot.TargetIndex)
                {
                    // 同一效果的确认翻转：只补 OpEffectResult，重发 OpIncomingEffect 会重复武装待击退
                    if (!prev.Confirmed && slot.TargetConfirmed && slot.GlobalSequence != 0)
                        _session.Execute(new ActorState.OpEffectResult(id, slot.GlobalSequence, slot.TargetIndex));
                    trackedSlots[i] = cur;
                    continue;
                }
                var effects = new ActionEffects();
                var raw = slot.RawEffects;
                var count = Math.Min(raw.Length, ActionEffects.MaxCount);
                for (var j = 0; j < count; ++j)
                    effects[j] = raw[j];
                _session.Execute(new ActorState.OpIncomingEffect(id, i,
                    new(slot.GlobalSequence, slot.TargetIndex, Sanitize(slot.SourceId), new((ActionType)slot.ActionType, slot.ActionId), effects)));
                if (!prev.Confirmed && slot.TargetConfirmed && slot.GlobalSequence != 0)
                    _session.Execute(new ActorState.OpEffectResult(id, slot.GlobalSequence, slot.TargetIndex));
                trackedSlots[i] = cur;
            }
        }
        for (var i = 0; i < trackedSlots.Length; ++i)
        {
            if (trackedSlots[i].Seq == 0 || present[i])
                continue;
            _session.Execute(new ActorState.OpIncomingEffect(id, i, default));
            trackedSlots[i] = default;
        }
    }

    private void ApplySharedDiffs(TrackedActor tracked, MechanicActorSnapshot cur, MechanicActorSnapshot? prev)
    {
        var id = cur.EntityId;
        if (prev == null || prev.IsDead != cur.IsDead)
        {
            if (cur.IsDead)
                _session.Execute(new ActorState.OpDead(id, true));
            else if (prev != null)
                _session.Execute(new ActorState.OpDead(id, false));
        }
        if (prev == null || prev.InCombat != cur.InCombat)
        {
            if (cur.InCombat)
                _session.Execute(new ActorState.OpCombat(id, true));
            else if (prev != null)
                _session.Execute(new ActorState.OpCombat(id, false));
        }
        var modelState = new ActorModelState(cur.ModelState, cur.AnimState1, cur.AnimState2);
        var modelChanged = prev == null ? modelState != default : new ActorModelState(prev.ModelState, prev.AnimState1, prev.AnimState2) != modelState;
        if (modelChanged)
            _session.Execute(new ActorState.OpModelState(id, modelState));
        var eventChanged = prev == null ? cur.EventState != default : prev.EventState != cur.EventState;
        if (eventChanged)
            _session.Execute(new ActorState.OpEventState(id, cur.EventState));
        var target = Sanitize(cur.TargetId);
        var targetChanged = prev == null ? target != default : Sanitize(prev.TargetId) != target;
        if (targetChanged)
            _session.Execute(new ActorState.OpTarget(id, target));

        ApplyCast(id, prev?.Cast, cur.Cast);
        ApplyStatuses(tracked, cur);
    }

    private void ApplyCast(ulong id, MechanicCastSnapshot? prev, MechanicCastSnapshot? cur)
    {
        var changed = (prev == null) != (cur == null);
        if (!changed && prev != null && cur != null)
        {
            changed = prev.ActionId != cur.ActionId || prev.IsSpell != cur.IsSpell
                || Sanitize(prev.TargetId) != Sanitize(cur.TargetId) || prev.TotalTime != cur.TotalTime
                || prev.Location != cur.Location || prev.Rotation != cur.Rotation;
        }
        if (!changed)
            return;
        ActorCastInfo? value = cur == null ? null : new()
        {
            Action = new(cur.IsSpell ? ActionType.Spell : ActionType.Ability, cur.ActionId),
            TargetID = Sanitize(cur.TargetId),
            Rotation = cur.Rotation.Radians(),
            Location = cur.Location,
            ElapsedTime = 0,
            TotalTime = cur.TotalTime,
            Interruptible = cur.Interruptible,
        };
        _session.Execute(new ActorState.OpCastInfo(id, value));
    }

    private void ApplyStatuses(TrackedActor tracked, MechanicActorSnapshot cur)
    {
        var actor = World.Actors.Find(cur.EntityId);
        Span<MechanicStatusDto?> bySlot = new MechanicStatusDto?[Actor.NumStatuses];
        foreach (var status in cur.Statuses)
        {
            if (status.Slot >= 0 && status.Slot < Actor.NumStatuses)
                bySlot[status.Slot] = status;
        }
        for (var i = 0; i < Actor.NumStatuses; ++i)
        {
            var dto = bySlot[i];
            ref var prev = ref tracked.Statuses[i];
            if (dto == null || dto.Id == 0)
            {
                if (prev.ID != 0 || (actor != null && actor.Statuses[i].ID != 0))
                {
                    _session.Execute(new ActorState.OpStatus(cur.EntityId, i, default));
                    prev = default;
                }
                continue;
            }
            var source = Sanitize(dto.SourceId);
            var expireAt = World.CurrentTime.AddSeconds(Math.Min(MathF.Abs(dto.RemainingSeconds), MaxStatusRemainingSeconds));
            if (prev.ID == dto.Id && prev.SourceID == source && prev.Extra == dto.Extra && (expireAt - prev.ExpireAt).TotalSeconds <= 1d)
            {
                prev.ExpireAt = expireAt;
                if (actor != null)
                    actor.Statuses[i].ExpireAt = expireAt;
                continue;
            }
            prev = new(dto.Id, dto.Extra, expireAt, source);
            _session.Execute(new ActorState.OpStatus(cur.EntityId, i, prev));
        }
    }

    private static ActorHPMP BuildHpmp(MechanicActorSnapshot actor) =>
        new(actor.CurHp, actor.MaxHp, (uint)(actor.ShieldPercent * 0.01f * actor.MaxHp), actor.CurMp, actor.MaxMp);

    private static ulong Sanitize(ulong raw) => raw != InvalidEntityId ? raw : 0;
}
