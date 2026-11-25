using PromeRotation.LogSystem;
using MasterPlayer.Scripting;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class DungeonEventAdapter(DungeonEncounter encounter, Action scan,
    Func<LogSystemCastStartEvent, float?> rotation, Action<string> logError)
{
    public void Handle(LogSystemEvent ev, DateTime now)
    {
        if (encounter.IsWickedThunder)
        {
            encounter.WickedThunderInputScan = true;
            try { scan(); }
            finally { encounter.WickedThunderInputScan = false; }
            encounter.WickedThunderEvent(ev, now);
            return;
        }
        if (encounter.IsThordan)
        {
            encounter.ThordanInputScan = true;
            try { scan(); }
            finally { encounter.ThordanInputScan = false; }
            encounter.ThordanEvent(ev, now);
            return;
        }
        if (encounter.IsKefka)
        {
            encounter.KefkaInputScan = true;
            try { scan(); }
            finally { encounter.KefkaInputScan = false; }
            encounter.KefkaEvent(ev, now);
            return;
        }
        if (encounter.IsQuantum)
        {
            encounter.QuantumInputScan = true;
            try { scan(); }
            finally { encounter.QuantumInputScan = false; }
            encounter.QuantumEvent(ev, now);
            return;
        }
        void ScanInput()
        {
            var previous = encounter.EurekaInputScan;
            encounter.EurekaInputScan = previous || encounter.IsEurekaOrthos;
            try { scan(); }
            finally { encounter.EurekaInputScan = previous; }
        }
        if (encounter.Active == null || ev is LogSystemEObjAnimationEvent) ScanInput();
        encounter.Tick(now);
        switch (ev)
        {
            case NativeActionEffectEvent effect:
                if (encounter.UsesEurekaNativeEffect(effect.ActionId))
                {
                    if (effect.ActionType is { } actionType && actionType != 1) break;
                    ScanInput();
                    encounter.Effect(effect.SourceId, effect.ActionId, effect.GlobalSequence, now, effect.MainTargetId, effect.TargetPosition);
                    break;
                }
                // Event prediction locks the main target's current world position.
                // Native packets drain before the periodic script scan on the Framework thread.
                if (encounter.Active != null && (effect.ActionId == NativeTreasureCatalog.VoidzoneAction(encounter.Active.BossOid)
                    || encounter.Active.BossOid == NativeTreasureCatalog.SECRET_PORXIE && effect.ActionId == 21672
                    || encounter.Active.BossOid == NativeTreasureCatalog.DAEN_TYPHON && effect.ActionId == 21781
                    || (encounter.UsesTacticalNativeEffect(effect.ActionId) || (encounter.UsesSlickshellNativeEffect(effect.ActionId) || (encounter.UsesBarP2NativeEffect(effect.ActionId) || (encounter.UsesCarnivaleNativeEffect(effect.ActionId) || (encounter.UsesHeavenOnHighNativeEffect(effect.ActionId) || (encounter.UsesGodmotherNativeEffect(effect.ActionId) || (encounter.UsesGudannaNativeEffect(effect.ActionId) || encounter.UsesPalaceEarlyPairNativeEffect(effect.ActionId))))))))))
                    ScanInput();
                encounter.TreasureEffect(effect.SourceId, effect.ActionId, effect.GlobalSequence, effect.MainTargetId, now, effect.TargetPosition);
                if (encounter.UsesFateNativeEffect(effect.ActionId) || (encounter.UsesTacticalNativeEffect(effect.ActionId) || (encounter.UsesSlickshellNativeEffect(effect.ActionId) || (encounter.UsesBarP2NativeEffect(effect.ActionId) || (encounter.UsesCarnivaleNativeEffect(effect.ActionId) || (encounter.UsesHeavenOnHighNativeEffect(effect.ActionId) || (encounter.UsesGodmotherNativeEffect(effect.ActionId) || (encounter.UsesGudannaNativeEffect(effect.ActionId) || encounter.UsesPalaceEarlyPairNativeEffect(effect.ActionId)))))))))
                    encounter.Effect(effect.SourceId, effect.ActionId, effect.GlobalSequence, now, effect.MainTargetId,
                        effect.TargetPosition, effect.Timestamp.ToUniversalTime(), hasTargets: effect.Targets.Length > 0);
                break;
            case LegacyMapEffectEvent legacy: encounter.LegacyMapEffect(legacy.Sequence, legacy.Param, legacy.Data); break;
            case LogSystemCastStartEvent cast:
                if (!encounter.Actors.ContainsKey(cast.SourceId)) ScanInput();
                if (!float.IsFinite(cast.CastTime) || cast.CastTime < 0 || cast.CastTime > 3600)
                {
                    logError($"技能 {cast.ActionId}: 读条时长无效，跳过预测。");
                    break;
                }
                var started = cast.Timestamp.ToUniversalTime();
                encounter.StartCast(cast.SourceId, new(cast.ActionId, cast.EffectPosition, rotation(cast),
                    started, started.AddSeconds(cast.CastTime + 0.3), cast.TargetId, cast.SourcePosition), now);
                break;
            // Eureka's source feed ends observed casts through CastInfo changes.
            // A queued cancellation must not consume component history ahead of that input.
            case LogSystemCancelCastEvent when encounter.IsEurekaOrthos: ScanInput(); break;
            case LogSystemInterruptCastEvent when encounter.IsEurekaOrthos: ScanInput(); break;
            case LogSystemCancelCastEvent cancel: encounter.Finish(cancel.SourceId, cancel.ActionId, cancel.Timestamp.ToUniversalTime()); break;
            case LogSystemInterruptCastEvent interrupt: encounter.Finish(interrupt.SourceId, interrupt.ActionId, interrupt.Timestamp.ToUniversalTime()); break;
            case LogSystemActionEffectEvent effect:
                if (encounter.UsesEurekaNativeEffect(effect.ActionId)) break;
                if (encounter.UsesFateNativeEffect(effect.ActionId) || (encounter.UsesTacticalNativeEffect(effect.ActionId) || (encounter.UsesSlickshellNativeEffect(effect.ActionId) || (encounter.UsesBarP2NativeEffect(effect.ActionId) || (encounter.UsesCarnivaleNativeEffect(effect.ActionId) || (encounter.UsesHeavenOnHighNativeEffect(effect.ActionId) || (encounter.UsesGodmotherNativeEffect(effect.ActionId) || (encounter.UsesGudannaNativeEffect(effect.ActionId) || encounter.UsesPalaceEarlyPairNativeEffect(effect.ActionId))))))))) break;
                if (encounter.IsBarPassage || encounter.IsHeroesPretenders || !encounter.Actors.ContainsKey(effect.SourceId)) ScanInput();
                encounter.Effect(effect.SourceId, effect.ActionId, effect.GlobalSequence, now, effect.TargetId, effect.EffectPosition, effect.Timestamp.ToUniversalTime(),
                    effect.SourceRotation, effect.TargetIndex >= 0, effect.SourcePosition);
                break;
            case LogSystemTargetIconEvent icon:
                var iconTarget = icon.TargetId is 0 or 0xE0000000 ? icon.SourceId : icon.TargetId;
                if (encounter.IsEurekaOrthos || !encounter.Actors.ContainsKey(iconTarget)) ScanInput();
                if (icon.SourceId is not (0 or 0xE0000000) && !encounter.Actors.ContainsKey(icon.SourceId)) ScanInput();
                encounter.Icon(iconTarget, icon.MarkerId, icon.Timestamp.ToUniversalTime(), icon.SourceId);
                break;
            case LogSystemPlayActionTimelineEvent timeline:
                if (encounter.IsEurekaOrthos || !encounter.Actors.ContainsKey(timeline.SourceId)) ScanInput();
                encounter.Timeline(timeline.SourceId, timeline.TimelineId, timeline.Timestamp.ToUniversalTime());
                break;
            case LogSystemNpcYellEvent yell:
                ScanInput();
                encounter.NpcYell(yell.SourceId, yell.NpcYellId, yell.Timestamp.ToUniversalTime());
                break;
            case LogSystemObjectCreateEvent create when !encounter.UsesDeepDungeonCastIdentity && !encounter.IsBarPassage && !encounter.IsCarnivale && !encounter.IsAlphaAlligator && !encounter.IsTimeBetween && !encounter.IsHeroesPretenders && !encounter.IsIseultalon && !encounter.IsDikaiosyne:
                encounter.ObjectCreated(create.SourceId, create.Timestamp.ToUniversalTime(), create.DataId, create.SourcePosition, create.SourceRotation);
                ScanInput();
                break;
            case LogSystemObjectRemoveEvent remove when !encounter.UsesDeepDungeonCastIdentity && !encounter.IsBarPassage && !encounter.IsCarnivale && !encounter.IsAlphaAlligator && !encounter.IsTimeBetween && !encounter.IsHeroesPretenders && !encounter.IsIseultalon && !encounter.IsDikaiosyne: encounter.RemoveActor(remove.SourceId, remove.Timestamp.ToUniversalTime()); break;
            case LogSystemMapEffectEvent map: encounter.MapEffect(map.Position, ((uint)map.Param2 << 16) | map.Param1, map.Timestamp.ToUniversalTime()); break;
            case LogSystemTetherEvent tether:
                if (encounter.IsEurekaOrthos || !encounter.Actors.ContainsKey(tether.SourceId) || !encounter.Actors.ContainsKey(tether.TargetId)) ScanInput();
                encounter.Tether(tether.TargetId, tether.TetherId, true, tether.Timestamp.ToUniversalTime(), tether.SourceId); break;
            case LogSystemTetherCancelEvent tether: encounter.Tether(tether.TargetId, tether.TetherId, false, tether.Timestamp.ToUniversalTime(), tether.SourceId); break;
            case LogSystemEObjSetStateEvent state: encounter.EventState(state.SourceId, state.State); break;
            case LogSystemDirectorUpdateEvent director: encounter.DirectorUpdate(director.Command, director.Param1, director.Param2, director.Param3, director.Param4); break;
            case LogSystemStatusAddEvent status when status.StatusId is HalataliCatalog.BURNING_WARD or 280 or QarnCatalog.DOOM or 270 or DzemaelCatalog.INVINCIBILITY or 302 or 303 or 3779 or 3463 or 374 or 370 or 273 or 569 or 420 or 3324 or 325 or 3012 or 911 or 2518 or 723 or 152 or 680 or 439 or 3513 or 1055 or 1064 or 775 or 562 or 1132 or 3421 or 5174 or 3629 or 1259 or 1253 or 1448 or 618 or 1849 or 2193 or 910 or 2574 or 18 or 2504 or 2672 or 1422 or 2936 or 3694 or 3909 or 2812 or 2813 or 2814 or 2965 or 2966 or 1769 or 1958 or 1959 or 1960 or 1961 or 1257 or 2552 or 2973 or 3364 or 3511 or 3802 or 4144 or 1909 or 3949 or 2104 or 4542 or 4543 or 4544 or 4545 or 4550 or 4594 or 2536 or 5191 or 477 or 2124 or 160 or 1209 or 2663 or 3054 or 1722 or 2345 or 1290 or 667 or 1538 or 1539 or 960 or 2377 or 2273 or 3523 or 4150 or 3974 or 3814 or 3815 or 4130 or 4176 or 202 or 333 or 334 or 349 or 292 or 348 or 369 or 379 or 126 or 377 or 1268 or 1269 or 904 or 905 or 671 or 3997 or 2161 or 2162 or 2163 or 2164 or 4551 or 4552 or 14:
                if (status.StatusId is 1538 or 1539 || status.StatusId == 3997 && !encounter.Actors.ContainsKey(status.TargetId)) ScanInput();
                var duration = status.Duration;
                if ((!float.IsFinite(duration) || duration <= 0) && status.DurationMilliseconds > 0)
                    duration = status.DurationMilliseconds / 1000f;
                DateTime? expires = float.IsFinite(duration) && duration is > 0 and < 86400
                    ? status.Timestamp.ToUniversalTime().AddSeconds(duration) : null;
                encounter.StatusChanged(status.TargetId, status.StatusId, true, status.Timestamp.ToUniversalTime(), status.Param, expires, status.SourceId); break;
            case LogSystemStatusRemoveEvent status when status.StatusId is HalataliCatalog.BURNING_WARD or 280 or QarnCatalog.DOOM or 270 or DzemaelCatalog.INVINCIBILITY or 302 or 303 or 3779 or 3463 or 374 or 370 or 273 or 569 or 420 or 3324 or 325 or 3012 or 911 or 2518 or 723 or 152 or 680 or 439 or 3513 or 1055 or 1064 or 775 or 562 or 1132 or 3421 or 5174 or 3629 or 1259 or 1253 or 1448 or 618 or 1849 or 2193 or 910 or 2574 or 18 or 2504 or 2672 or 1422 or 2936 or 3694 or 3909 or 2812 or 2813 or 2814 or 2965 or 2966 or 1769 or 1958 or 1959 or 1960 or 1961 or 1257 or 2552 or 2973 or 3364 or 3511 or 3802 or 4144 or 1909 or 3949 or 2104 or 4542 or 4543 or 4544 or 4545 or 4550 or 4594 or 2536 or 5191 or 477 or 2124 or 160 or 1209 or 2663 or 3054 or 1722 or 2345 or 1290 or 667 or 1538 or 1539 or 960 or 2377 or 2273 or 3523 or 4150 or 3974 or 3814 or 3815 or 4130 or 4176 or 202 or 333 or 334 or 349 or 292 or 348 or 369 or 379 or 126 or 377 or 1268 or 1269 or 904 or 905 or 671 or 3997 or 2161 or 2162 or 2163 or 2164 or 4551 or 4552 or 14:
                if (status.StatusId == 3997 && !encounter.Actors.ContainsKey(status.TargetId)) ScanInput();
                encounter.StatusChanged(status.TargetId, status.StatusId, false, status.Timestamp.ToUniversalTime(), sourceId: status.SourceId); break;
            case LogSystemEObjAnimationEvent animation: encounter.Animation(animation.SourceId, ((uint)animation.Param1 << 16) | animation.Param2,
                animation.Timestamp.ToUniversalTime()); break;
        }
    }
}
