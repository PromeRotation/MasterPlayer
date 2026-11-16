using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Enuo;

internal sealed class EnuoEventAdapter(EnuoEncounter encounter, Func<LogSystemCastStartEvent, float?> rotation)
{
    public void Handle(LogSystemEvent ev, DateTime now)
    {
        switch (ev)
        {
            case LogSystemCastStartEvent cast when float.IsFinite(cast.CastTime) && cast.CastTime is >= 0 and <= 60:
                encounter.StartCast(new(cast.SourceId, cast.ActionId, cast.TargetId, cast.EffectPosition,
                    rotation(cast) ?? encounter.Actors.GetValueOrDefault(cast.SourceId)?.Rotation ?? 0,
                    now, now.AddSeconds(cast.CastTime + 0.3)), authoritative: true);
                break;
            case LogSystemActionEffectEvent e: encounter.Effect(e.SourceId, e.ActionId, e.GlobalSequence, now); break;
            case LogSystemCancelCastEvent c: encounter.Cancel(c.SourceId, c.ActionId); break;
            case LogSystemInterruptCastEvent c: encounter.Cancel(c.SourceId, c.ActionId); break;
            case LogSystemTargetIconEvent i: encounter.Icon(i.TargetId is 0 or 0xE0000000 ? i.SourceId : i.TargetId, i.MarkerId, now); break;
            case LogSystemTetherEvent t: encounter.Tether(t.SourceId, t.TargetId, t.TetherId, now); break;
            case LogSystemTetherCancelEvent t: encounter.Untether(t.SourceId, t.TetherId); break;
            case LogSystemObjectRemoveEvent r: encounter.RemoveActor(r.SourceId); break;
            case LogSystemStatusAddEvent s when s.StatusId is 4562 or 3523 or 2941 or 3943:
                encounter.Status(s.TargetId, s.StatusId, s.Param, s.SourceId,
                    float.IsFinite(s.Duration) && s.Duration > 0 ? now.AddSeconds(s.Duration) : DateTime.MaxValue);
                break;
            case LogSystemStatusRemoveEvent s: encounter.Status(s.TargetId, s.StatusId, s.Param, s.SourceId, null); break;
            case LogSystemDirectorUpdateEvent d when d.Command is 0x40000005 or 0x40000006: encounter.Reset(); break;
        }
    }
}
