using System.Numerics;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Replay;
using PromeRotation.LogSystem;

namespace MasterPlayer.MechanicHost;

internal sealed class ReplayFeedAdapter
{
    private const byte ObjectKindPc = 1;
    private const byte ObjectKindBattleNpc = 2;
    private const float MaxStatusRemainingSeconds = 100000f;

    private readonly ushort _territory;
    private readonly ushort _cfc;
    private readonly (byte Id, Vector3? Pos)[] _waymarks = new (byte, Vector3?)[8];
    private readonly Dictionary<ulong, uint> _lastEAnim = [];
    private readonly List<MechanicFeedEvent> _frameEvents = [];

    private ulong _pendingCaster;
    private uint _pendingActionId;
    private uint _pendingGlobalSequence;
    private ulong _pendingMainTarget;
    private Vector3 _pendingTargetPos;
    private ushort _pendingRotationInt;
    private List<MechanicActionEffectTarget>? _pendingTargets;

    public ReplayFeedAdapter(ushort territory, Func<ushort, ushort>? cfcResolver = null)
    {
        _territory = territory;
        _cfc = cfcResolver?.Invoke(territory) ?? (ushort)0;
    }

    public int DegradedEffectCount { get; private set; }

    public MechanicWorldFrame ConvertFrame(ReplayWorldState world)
    {
        var actors = new List<MechanicActorSnapshot>(world.Actors.Count);
        var index = 0;
        foreach (var actor in world.Actors.Values)
        {
            actors.Add(ConvertActor(actor, index++, world.CurrentTime));
            if (actor.EventAnimation is { } animation)
            {
                if (!_lastEAnim.TryGetValue(actor.Id, out var last) || last != animation)
                {
                    _lastEAnim[actor.Id] = animation;
                    _frameEvents.Add(new MechanicEventEObjAnimation(actor.Id, (ushort)(animation >> 16), (ushort)animation));
                }
            }
            else
            {
                _lastEAnim.Remove(actor.Id);
            }
        }
        return new(_territory, _cfc, world.LocalPlayerId, world.TargetId,
            ConvertParty(world), _waymarks, actors.ToArray());
    }

    public IReadOnlyList<MechanicFeedEvent> DrainFrameEvents()
    {
        var events = _frameEvents.ToArray();
        _frameEvents.Clear();
        return events;
    }

    public MechanicFeedEvent? FlushPendingEffect()
    {
        if (_pendingTargets == null)
            return null;
        var effect = new MechanicEventActionEffect(_pendingCaster, 1, _pendingActionId, _pendingMainTarget, 0f,
            _pendingTargetPos, _pendingGlobalSequence, 0, _pendingRotationInt, _pendingTargets.ToArray());
        _pendingTargets = null;
        return effect;
    }

    public MechanicFeedEvent? TryConvertEvent(LogSystemEvent ev)
    {
        switch (ev)
        {
            case LogSystemTetherEvent e: return MechanicEventFactory.FromTether(e);
            case LogSystemTetherCancelEvent e: return MechanicEventFactory.FromTetherCancel(e);
            case LogSystemTargetIconEvent e: return MechanicEventFactory.FromTargetIcon(e);
            case LogSystemTargetVfxEvent e: return MechanicEventFactory.FromTargetVfx(e);
            case LogSystemMarkerEvent e: return MechanicEventFactory.FromMarker(e);
            case LogSystemMapEffectEvent e: return MechanicEventFactory.FromMapEffect(e);
            case LogSystemEObjAnimationEvent e: return MechanicEventFactory.FromEObjAnimation(e);
            case LogSystemEObjSetStateEvent e: return MechanicEventFactory.FromEObjSetState(e);
            case LogSystemDirectorUpdateEvent e: return MechanicEventFactory.FromDirectorUpdate(e);
            case LogSystemNpcYellEvent e: return MechanicEventFactory.FromNpcYell(e);
            case LogSystemPlayActionTimelineEvent e: return MechanicEventFactory.FromPlayActionTimeline(e);
            case LogSystemActionEffectEvent e: return MergeEffect(e);
            case LogSystemWaymarkEvent e:
                if (e.MarkerId < _waymarks.Length)
                    _waymarks[e.MarkerId] = (e.MarkerId, e.Active ? e.Position : null);
                return null;
            default:
                return null;
        }
    }

    private MechanicFeedEvent? MergeEffect(LogSystemActionEffectEvent e)
    {
        MechanicFeedEvent? flushed = null;
        if (_pendingTargets != null
            && (_pendingGlobalSequence != e.GlobalSequence || _pendingCaster != e.SourceId || _pendingActionId != e.ActionId))
        {
            flushed = FlushPendingEffect();
        }
        if (_pendingTargets == null)
        {
            ++DegradedEffectCount;
            var rotation = e.SourceRotation ?? 0f;
            _pendingRotationInt = (ushort)Math.Clamp((int)MathF.Round((rotation + MathF.PI) * (65535f / (2f * MathF.PI))), 0, 65535);
            _pendingCaster = e.SourceId;
            _pendingActionId = e.ActionId;
            _pendingGlobalSequence = e.GlobalSequence;
            _pendingMainTarget = e.TargetId;
            _pendingTargetPos = e.EffectPosition ?? e.TargetPosition ?? e.SourcePosition ?? default;
            _pendingTargets = [];
        }
        if (!_pendingTargets.Any(t => t.Id == e.TargetId))
            _pendingTargets.Add(new(e.TargetId, new ulong[8]));
        return flushed;
    }

    private static MechanicActorSnapshot ConvertActor(ReplayActorState actor, int spawnIndex, DateTime now)
    {
        var isAlly = actor.Kind == ObjectKindPc || actor.Kind != ObjectKindBattleNpc || !actor.Hostile;
        var tether = actor.Tethers.FirstOrDefault(t => t.Id != 0);
        MechanicCastSnapshot? cast = null;
        if (actor.Cast is { } c)
        {
            var total = Math.Max(0.05f, (float)(c.FinishAt - c.StartedAt).TotalSeconds);
            cast = new(c.ActionId, true, c.TargetId, c.Rotation ?? actor.Rotation, c.Location, total, c.Interruptible);
        }
        var statuses = new MechanicStatusDto[actor.Statuses.Length];
        for (var i = 0; i < statuses.Length; ++i)
        {
            var status = actor.Statuses[i];
            var remaining = status.ExpiresAt == DateTime.MaxValue
                ? MaxStatusRemainingSeconds
                : Math.Clamp((float)(status.ExpiresAt - now).TotalSeconds, 0f, MaxStatusRemainingSeconds);
            statuses[i] = new(i, status.Id, status.Param, status.SourceId, remaining);
        }
        return new(actor.Id, actor.Oid, spawnIndex, 0, actor.Name, actor.NameId, actor.Kind, actor.SubKind,
            actor.ClassJob, actor.Level, actor.Position, actor.Rotation, actor.HitboxRadius,
            actor.Hp, actor.MaxHp, actor.Shield, actor.Mp, actor.MaxMp,
            actor.Targetable, isAlly, actor.Dead, actor.InCombat,
            actor.OwnerId, actor.TargetId, 0, 0, actor.EventState,
            actor.ModelState, actor.AnimationState1, actor.AnimationState2,
            tether?.Id ?? 0, tether?.TargetId ?? 0, cast, statuses);
    }

    private MechanicPartySlotDto[] ConvertParty(ReplayWorldState world)
    {
        var slots = new List<MechanicPartySlotDto>(8);
        foreach (var member in world.Party)
        {
            if (member.EntityId == world.LocalPlayerId)
            {
                slots.Insert(0, new(0, 0, member.EntityId, NameOf(world, member.EntityId)));
                break;
            }
        }
        foreach (var member in world.Party)
        {
            if (member.EntityId == world.LocalPlayerId || slots.Count >= 8)
                continue;
            slots.Add(new(slots.Count, 0, member.EntityId, NameOf(world, member.EntityId)));
        }
        return slots.ToArray();
    }

    private static string NameOf(ReplayWorldState world, ulong entityId)
        => world.Actors.TryGetValue(entityId, out var actor) ? actor.Name : "";
}
