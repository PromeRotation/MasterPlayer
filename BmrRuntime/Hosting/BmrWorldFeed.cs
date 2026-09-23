using BossMod;

namespace MasterPlayer.BmrRuntime;

public sealed record BmrCastSnapshot(uint ActionId, bool IsSpell, ulong TargetId, float Rotation, Vector3 Location, float TotalTime, bool Interruptible);
public sealed record BmrStatusDto(int Slot, uint Id, ushort Extra, ulong SourceId, float RemainingSeconds);
public sealed record BmrActorSnapshot(ulong EntityId, uint OID, int SpawnIndex, uint LayoutID, string Name, uint NameID,
    byte ObjectKind, byte SubKind, uint ClassJobId, int Level, Vector3 Position, float Rotation, float HitboxRadius,
    uint CurHp, uint MaxHp, byte ShieldPercent, uint CurMp, uint MaxMp,
    bool IsTargetable, bool IsAlly, bool IsDead, bool InCombat,
    ulong OwnerId, ulong TargetId, uint FateId, int Renderflags, byte EventState,
    byte ModelState, byte AnimState1, byte AnimState2,
    uint TetherId, ulong TetherTargetId, BmrCastSnapshot? Cast, BmrStatusDto[] Statuses);
public sealed record BmrPartySlotDto(int Slot, ulong ContentId, ulong InstanceId, string Name);
public sealed record BmrWorldFrame(ushort Territory, ushort ContentFinderConditionId, ulong LocalPlayerId, ulong TargetId,
    BmrPartySlotDto[] Party, (byte Id, Vector3? Pos)[] Waymarks, BmrActorSnapshot[] Actors);

public abstract record BmrFeedEvent;
public sealed record BmrEventTether(ulong SourceId, uint TetherId, ulong TargetId) : BmrFeedEvent;
public sealed record BmrEventTetherCancel(ulong SourceId) : BmrFeedEvent;
public sealed record BmrEventIcon(uint IconId, ulong TargetId, ulong SourceId) : BmrFeedEvent;
public sealed record BmrEventVfx(uint VfxId, ulong TargetId, ulong SourceId) : BmrFeedEvent;
public sealed record BmrEventHeadMarker(byte SignId, ulong TargetId) : BmrFeedEvent;
public sealed record BmrEventMapEffect(byte Index, uint State) : BmrFeedEvent;
public sealed record BmrEventDirectorUpdate(uint DirectorID, uint UpdateID, uint Param1, uint Param2, uint Param3, uint Param4) : BmrFeedEvent;
public sealed record BmrEventEObjAnimation(ulong SourceId, ushort Param1, ushort Param2) : BmrFeedEvent;
public sealed record BmrEventEObjSetState(ulong SourceId, ushort State) : BmrFeedEvent;
public sealed record BmrEventNpcYell(ulong SourceId, ushort Message) : BmrFeedEvent;
public sealed record BmrEventSystemLog(uint MessageId, int[] Args) : BmrFeedEvent;
public sealed record BmrEventPlayActionTimeline(ulong SourceId, ushort TimelineId) : BmrFeedEvent;
public sealed record BmrActionEffectTarget(ulong Id, ulong[] RawEffects);
public sealed record BmrEventActionEffect(ulong CasterId, byte ActionType, uint ActionId, ulong MainTargetId, float AnimationLock,
    Vector3 TargetPos, uint GlobalSequence, uint SourceSequence, ushort RotationInt, BmrActionEffectTarget[] Targets) : BmrFeedEvent;

public sealed class BmrWorldFeed
{
    private const ulong InvalidEntityId = 0xE0000000;
    private const float MaxStatusRemainingSeconds = 100000f;

    private readonly BmrSession _session;
    private readonly List<BmrFeedEvent> _pendingEvents = [];

    private sealed class TrackedActor(BmrActorSnapshot snapshot)
    {
        public BmrActorSnapshot Snapshot = snapshot;
        public readonly ActorStatus[] Statuses = new ActorStatus[Actor.NumStatuses];
    }

    private readonly Dictionary<ulong, TrackedActor> _tracked = [];

    public BmrWorldFeed(BmrSession session) => _session = session;

    private WorldState World => _session.World;

    public void ApplyFrame(BmrWorldFrame frame)
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
            ApplyActor(actor);

        for (var slot = 0; slot < PartyState.MaxPartySize; ++slot)
        {
            var dto = frame.Party.FirstOrDefault(p => p.Slot == slot);
            var member = dto != null ? new PartyState.Member(dto.ContentId, dto.InstanceId, false, dto.Name) : PartyState.EmptySlot;
            if (World.Party.Members[slot] != member)
                _session.Execute(new PartyState.OpModify(slot, member));
        }
    }

    public void Enqueue(BmrFeedEvent feedEvent) => _pendingEvents.Add(feedEvent);

    public int Flush()
    {
        var count = _pendingEvents.Count;
        for (var i = 0; i < count; ++i)
            ApplyEvent(_pendingEvents[i]);
        _pendingEvents.Clear();
        return count;
    }

    public void ApplyEvent(BmrFeedEvent feedEvent)
    {
        switch (feedEvent)
        {
            case BmrEventTether e:
                _session.Execute(new ActorState.OpTether(e.SourceId, new(e.TetherId, Sanitize(e.TargetId))));
                break;
            case BmrEventTetherCancel e:
                _session.Execute(new ActorState.OpTether(e.SourceId, default));
                break;
            case BmrEventIcon e:
                _session.Execute(new ActorState.OpIcon(e.SourceId, e.IconId - BossMod.Network.IDScramble.Delta, e.TargetId));
                break;
            case BmrEventVfx e:
                _session.Execute(new ActorState.OpVFX(e.SourceId, e.VfxId, e.TargetId));
                break;
            case BmrEventHeadMarker e:
                if (e.SignId < (byte)Sign.Count)
                    _session.Execute(new WaymarkState.OpSignChange((Sign)e.SignId, Sanitize(e.TargetId)));
                break;
            case BmrEventMapEffect e:
                _session.Execute(new WorldState.OpMapEffect(e.Index, e.State));
                break;
            case BmrEventDirectorUpdate e:
                _session.Execute(new WorldState.OpDirectorUpdate(e.DirectorID, e.UpdateID, e.Param1, e.Param2, e.Param3, e.Param4));
                break;
            case BmrEventEObjAnimation e:
                _session.Execute(new ActorState.OpEventObjectAnimation(e.SourceId, e.Param1, e.Param2));
                break;
            case BmrEventEObjSetState e:
                _session.Execute(new ActorState.OpEventObjectStateChange(e.SourceId, e.State));
                break;
            case BmrEventNpcYell e:
                _session.Execute(new ActorState.OpEventNpcYell(e.SourceId, e.Message));
                break;
            case BmrEventSystemLog e:
                _session.Execute(new WorldState.OpSystemLogMessage(e.MessageId, e.Args));
                break;
            case BmrEventPlayActionTimeline e:
                _session.Execute(new ActorState.OpPlayActionTimelineEvent(e.SourceId, e.TimelineId));
                break;
            case BmrEventActionEffect e:
                ApplyActionEffect(e);
                break;
        }
    }

    private void ApplyActionEffect(BmrEventActionEffect e)
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

    private void ApplyActor(BmrActorSnapshot cur)
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
    }

    private void ApplySharedDiffs(TrackedActor tracked, BmrActorSnapshot cur, BmrActorSnapshot? prev)
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

    private void ApplyCast(ulong id, BmrCastSnapshot? prev, BmrCastSnapshot? cur)
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

    private void ApplyStatuses(TrackedActor tracked, BmrActorSnapshot cur)
    {
        var actor = World.Actors.Find(cur.EntityId);
        Span<BmrStatusDto?> bySlot = new BmrStatusDto?[Actor.NumStatuses];
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

    private static ActorHPMP BuildHpmp(BmrActorSnapshot actor) =>
        new(actor.CurHp, actor.MaxHp, (uint)(actor.ShieldPercent * 0.01f * actor.MaxHp), actor.CurMp, actor.MaxMp);

    private static ulong Sanitize(ulong raw) => raw != InvalidEntityId ? raw : 0;
}
