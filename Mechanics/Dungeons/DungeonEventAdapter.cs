using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class DungeonEventAdapter(DungeonEncounter encounter, Action scan,
    Func<LogSystemCastStartEvent, float?> rotation, Action<string> logError)
{
    public void Handle(LogSystemEvent ev, DateTime now)
    {
        if (encounter.Active == null || ev is LogSystemEObjAnimationEvent) scan();
        encounter.Tick(now);
        switch (ev)
        {
            case LogSystemCastStartEvent cast:
                if (!encounter.Actors.ContainsKey(cast.SourceId)) scan();
                if (!float.IsFinite(cast.CastTime) || cast.CastTime < 0 || cast.CastTime > 3600)
                {
                    logError($"技能 {cast.ActionId}: 读条时长无效，跳过预测。");
                    break;
                }
                var started = cast.Timestamp.ToUniversalTime();
                encounter.StartCast(cast.SourceId, new(cast.ActionId, cast.EffectPosition, rotation(cast),
                    started, started.AddSeconds(cast.CastTime + 0.3), cast.TargetId), now);
                break;
            case LogSystemCancelCastEvent cancel: encounter.Finish(cancel.SourceId, cancel.ActionId, cancel.Timestamp.ToUniversalTime()); break;
            case LogSystemInterruptCastEvent interrupt: encounter.Finish(interrupt.SourceId, interrupt.ActionId, interrupt.Timestamp.ToUniversalTime()); break;
            case LogSystemActionEffectEvent effect: encounter.Effect(effect.SourceId, effect.ActionId, effect.GlobalSequence, now); break;
            case LogSystemObjectRemoveEvent remove: encounter.RemoveActor(remove.SourceId); break;
            case LogSystemEObjAnimationEvent animation: encounter.Animation(animation.SourceId, ((uint)animation.Param1 << 16) | animation.Param2); break;
        }
    }
}
