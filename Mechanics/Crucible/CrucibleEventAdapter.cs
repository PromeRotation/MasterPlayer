using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Crucible;

internal sealed class CrucibleEventAdapter(CrucibleEngine engine, Func<ulong, Actor?> find, Func<LogSystemCastStartEvent, float?> rotation, Action scan, Action<string> logError)
{
    private readonly CrucibleEngine _engine = engine;
    private WorldState _world => _engine.World;
    private readonly Dictionary<(uint, ulong, uint), List<LogSystemActionEffectEvent>> _effects = [];
    private Actor? Find(ulong id) => find((uint)id);
    private void Scan() => scan();
    private void LogError(string text) => logError(text);
    private Actor EffectSource(LogSystemActionEffectEvent e) => Find(e.SourceId) ?? new Actor
    {
        InstanceID = (uint)e.SourceId, OID = e.SourceDataId, Name = e.SourceName,
        Position = WPos.FromVec3(e.SourcePosition ?? e.EffectPosition ?? default), Rotation = new(e.SourceRotation ?? 0), Type = ActorType.Enemy
    };
    private bool TryRotation(LogSystemCastStartEvent cast, out float value)
    {
        var result = rotation(cast); value = result ?? 0; return result.HasValue;
    }
    public void Clear() => _effects.Clear();
    public void Handle(LogSystemEvent e)
    {
        if (e is LogSystemActionEffectEvent effect)
        {
            var key = (effect.GlobalSequence, effect.SourceId, effect.ActionId);
            if (!_effects.TryGetValue(key, out var targets)) _effects[key] = targets = [];
            targets.Add(effect);
            return;
        }
        Flush();
        Dispatch(e);
    }
    public void Flush()
    {
        foreach (var batch in _effects.Values)
        {
            var first = batch.MinBy(e => e.TargetIndex)!;
            Dispatch(first, batch.Select(e => new ActorCastEvent.Target((uint)e.TargetId)).Distinct().ToArray());
        }
        _effects.Clear();
    }
    private void Dispatch(LogSystemEvent e, ActorCastEvent.Target[]? targets = null)
    {
        switch (e)
        {
            case LogSystemCastStartEvent cast when Find(cast.SourceId) is { } actor:
                if (!TryRotation(cast, out var rotation)) { LogError($"斗兽奇弈：技能 {cast.ActionId} 缺少锁定读条朝向，跳过此次预测。"); break; }
                var delay = Math.Max(0, cast.CastTime - (float)Math.Max(0, (_world.CurrentTime - cast.Timestamp.ToUniversalTime()).TotalSeconds));
                if (cast.SourcePosition is { } p) actor.Position = WPos.FromVec3(p);
                _engine.Cast(actor, new() { Action = new(cast.ActionId), LocXZ = WPos.FromVec3(cast.EffectPosition), Rotation = new(rotation),
                    TargetID = (uint)cast.TargetId, FinishAt = _world.FutureTime(delay + 0.3), RemainingTime = delay, NPCRemainingTime = delay + 0.3f });
                break;
            case LogSystemActionEffectEvent effect when EffectSource(effect) is { } actor:
                if (effect.SourcePosition is { } effectSource) actor.Position = WPos.FromVec3(effectSource);
                _engine.Effect(actor, new() { Action = new(effect.ActionId), TargetXZ = WPos.FromVec3(effect.EffectPosition ?? effect.TargetPosition ?? actor.Position.ToVec3()),
                    Rotation = new(effect.SourceRotation ?? actor.Rotation.Rad), MainTargetID = (uint)effect.TargetId,
                    GlobalSequence = effect.GlobalSequence, Targets = targets ?? [new((uint)effect.TargetId)] });
                break;
            case LogSystemCancelCastEvent cast when Find(cast.SourceId) is { } actor: _engine.FinishCast(actor, cast.ActionId, cancelled: true); break;
            case LogSystemInterruptCastEvent cast when Find(cast.SourceId) is { } actor: _engine.FinishCast(actor, cast.ActionId, cancelled: true); break;
            case LogSystemTetherEvent tether when Find(tether.SourceId) is { } actor:
                actor.Tether = new(tether.TetherId, (uint)tether.TargetId); _engine.Dispatch(c => c.OnTethered(actor, actor.Tether)); break;
            case LogSystemTetherCancelEvent tether when Find(tether.SourceId) is { } actor:
                var old = actor.Tether; actor.Tether = default; _engine.Dispatch(c => c.OnUntethered(actor, old)); break;
            case LogSystemTargetIconEvent icon when Find(icon.SourceId) is { } actor: _engine.Dispatch(c => c.OnEventIcon(actor, icon.MarkerId, (uint)icon.TargetId)); break;
            case LogSystemMapEffectEvent map: _engine.Dispatch(c => c.OnMapEffect((byte)map.Position, map.Param1 | ((uint)map.Param2 << 16))); break;
            case LogSystemEObjAnimationEvent anim when Find(anim.SourceId) is { } actor: _engine.Dispatch(c => c.OnActorEAnim(actor, ((uint)anim.Param1 << 16) | anim.Param2)); break;
            case LogSystemEObjSetStateEvent state when Find(state.SourceId) is { } actor:
                actor.EventState = (byte)state.State; _engine.Dispatch(c => { c.OnActorEState(actor, state.State); c.OnActorEventStateChange(actor, actor.EventState); }); break;
            case LogSystemPlayActionTimelineEvent timeline when Find(timeline.SourceId) is { } actor: _engine.Dispatch(c => c.OnActorPlayActionTimelineEvent(actor, (ushort)timeline.TimelineId)); break;
            case LogSystemDirectorUpdateEvent director: _engine.Dispatch(c => c.OnEventDirectorUpdate(director.Command, director.Param1, director.Param2, director.Param3, director.Param4)); break;
            case LogSystemStatusAddEvent status when Find(status.TargetId) is { } actor:
                var gained = new ActorStatus { ID = status.StatusId, Extra = status.Param, SourceID = (uint)status.SourceId, ExpireAt = status.Duration > 0 ? status.Timestamp.ToUniversalTime().AddSeconds(status.Duration) : DateTime.MaxValue };
                var duplicate = actor.Statuses.Any(s => s.ID == gained.ID && s.SourceID == gained.SourceID && s.Extra == gained.Extra && Math.Abs((s.ExpireAt - gained.ExpireAt).TotalSeconds) < 1);
                actor.Statuses = actor.Statuses.Where(s => s.ID != gained.ID || s.SourceID != gained.SourceID).Append(gained).ToArray();
                if (!duplicate) _engine.Dispatch(c => c.OnStatusGain(actor, ref gained)); break;
            case LogSystemStatusRemoveEvent status when Find(status.TargetId) is { } actor:
                // Same-id statuses from different sources are tracked separately, matching the scan diff.
                var lost = actor.FindStatus(status.StatusId, (uint)status.SourceId) ?? actor.FindStatus(status.StatusId)
                    ?? new ActorStatus { ID = status.StatusId, Extra = status.Param };
                actor.Statuses = actor.Statuses.Where(s => s.ID != lost.ID || s.SourceID != lost.SourceID).ToArray();
                _engine.Dispatch(c => c.OnStatusLose(actor, ref lost)); break;
            case LogSystemObjectCreateEvent: Scan(); break;
            case LogSystemObjectRemoveEvent remove when Find(remove.SourceId) is { } actor: _engine.Destroy(actor, true); break;
        }
    }
}
