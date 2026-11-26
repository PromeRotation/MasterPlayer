using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ECommons.DalamudServices;
using MasterPlayer.Settings;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Dodge;

public sealed partial class DodgeController
{
    private readonly IDisposable _pilgrimsFollowUpSubscription;
    private readonly Dictionary<ulong, (uint FirstAction, uint FollowAction, GeometryTemplate Shape, TransformBinding Binding,
        float Delay, SafeZoneAreaHandle FirstHandle, SafeZoneAreaHandle FollowHandle, DateTime Expires, bool Resolved,
        uint Territory, int RemainingHits, HashSet<(uint Sequence, long ZeroSequenceTime)> SeenEffects)> _pilgrimsFollowUps = [];

    private void TrackPilgrimsFollowUpCast(LogSystemCastStartEvent cast, SafeZoneAreaHandle firstHandle,
        TransformBinding binding, uint territory)
    {
        if (!(territory == 1287 && cast.ActionId is 42214 or 42216 or 42516
            || territory == 1288 && cast.ActionId is 42578 or 42748
            || territory is >= 1281 and <= 1290 && cast.ActionId == 44759) || binding is not StaticTransform pose)
            return;

        RemovePilgrimsFollowUp(cast.SourceId);
        // ARR 四组连招：右侧42214→左侧42217，左侧42216→右侧42215；包内首击方向已包含侧偏。
        var followAction = cast.ActionId switch
        {
            42214 => 42217u,
            42216 => 42215u,
            42516 => 42517u,
            42578 => 42749u,
            44759 => 44760u,
            _ => 42746u,
        };
        GeometryTemplate shape = cast.ActionId switch
        {
            42516 => new DonutTemplate(5f, 10f),
            42578 => new DonutTemplate(10f, 40f),
            42748 => new CircleTemplate(10f),
            44759 => new FanTemplate(8f, MathF.PI),
            _ => new FanTemplate(30f, MathF.PI),
        };
        var shockwave = cast.ActionId is 42214 or 42216;
        var rotation = shockwave ? pose.Rotation + MathF.PI : 0f;
        var delay = shockwave ? 2.6f : 3.1f;
        var heels = cast.ActionId == 44759;
        // 连踢的后三击由来源实时面向决定；没有实测间隔时持续避开正面，按三次效果结束。
        TransformBinding followBinding = heels
            ? new FollowEntityPositionAndRotation((uint)cast.SourceId, Vector2.Zero, MissingEntityPolicy.DisableArea)
            : new StaticTransform(pose.Position, rotation);
        _pilgrimsFollowUps[cast.SourceId] = (cast.ActionId, followAction, shape, followBinding,
            delay, firstHandle, default, DateTime.UtcNow.AddSeconds(cast.CastTime + (heels ? 15 : 6)), false, territory,
            heels ? 3 : 1, []);
    }

    private void HandlePilgrimsFollowUpEvent(LogSystemEvent ev)
    {
        if (_disposed || _pilgrimsFollowUps.Count == 0)
            return;

        if (ev is LogSystemBattleEndedEvent or LogSystemTerritoryEvent)
        {
            ClearPilgrimsFollowUps();
            return;
        }
        var source = ev switch
        {
            LogSystemActionEffectEvent effect => effect.SourceId,
            LogSystemCancelCastEvent cancel => cancel.SourceId,
            LogSystemInterruptCastEvent interrupt => interrupt.SourceId,
            LogSystemObjectRemoveEvent remove => remove.SourceId,
            _ => 0UL,
        };
        if (!_pilgrimsFollowUps.TryGetValue(source, out var sequence))
            return;

        if (DateTime.UtcNow > sequence.Expires || ev is LogSystemObjectRemoveEvent
            || ev is LogSystemCancelCastEvent cancelled && (cancelled.ActionId == 0 || cancelled.ActionId == sequence.FirstAction)
            || ev is LogSystemInterruptCastEvent interrupted && (interrupted.ActionId == 0 || interrupted.ActionId == sequence.FirstAction))
        {
            RemovePilgrimsFollowUp(source);
            return;
        }
        if (ev is not LogSystemActionEffectEvent hit)
            return;
        if (hit.ActionId == sequence.FollowAction)
        {
            // PR captures one timestamp before expanding a packet into targets.
            // Sequence zero is valid on successive heel packets, so only those
            // packets use that shared timestamp as their receipt identity.
            var receipt = (hit.GlobalSequence, hit.GlobalSequence == 0 ? hit.Timestamp.ToUniversalTime().Ticks : 0L);
            if (!sequence.Resolved || !sequence.SeenEffects.Add(receipt))
                return;
            if (--sequence.RemainingHits == 0)
                RemovePilgrimsFollowUp(source);
            else
                _pilgrimsFollowUps[source] = sequence;
            return;
        }
        if (hit.ActionId != sequence.FirstAction || sequence.Resolved)
            return;

        var settings = MasterPlayerSettings.Instance;
        if (MasterPlayerPlugin.Instance?.Scripts?.ReplayModeActive == true
            || !(settings.EnableSmartMovement || settings.EnableRadar || settings.EnableRadarOverlay)
            || MasterPlayerPlugin.Instance?.MechanicHost?.HasActiveModule == true)
        {
            RemovePilgrimsFollowUp(source);
            return;
        }

        // 首击效果可能仍在跳跃前的位置，追击须保留首次读条的锁定落点与方向。
        var template = DangerMarginInflator.Inflate(sequence.Shape, settings.DodgeDangerMargin);
        var heels = sequence.FirstAction == 44759;
        var registry = MasterPlayerPlugin.Instance!.SafeZone.Registry;
        var handle = heels
            ? registry.AddPersistentDanger(template, sequence.Binding)
            : registry.AddDelayedHit(template, sequence.Binding, sequence.Delay);
        _registeredHandles.Add(handle);
        var label = sequence.FollowAction switch
        {
            42217 => "左侧震荡波（无读条追击）",
            42215 => "右侧震荡波（无读条追击）",
            42749 => "圆环双水流（无读条月环）",
            42746 => "环圆双水流（无读条圆形）",
            44760 => "连踢（后三次无读条正面攻击）",
            _ => "地层俯冲（执行贯穿后无读条月环）",
        };
        MasterPlayerPlugin.Instance.AoeVisibility?.Track(handle, $"action/{sequence.FollowAction}", label);
        sequence.FollowHandle = handle;
        sequence.Resolved = true;
        sequence.Expires = DateTime.UtcNow.AddSeconds(heels ? 15 : sequence.Delay + 2);
        _pilgrimsFollowUps[source] = sequence;
    }

    private void UpdatePilgrimsFollowUps()
    {
        if (_pilgrimsFollowUps.Count == 0)
            return;
        var settings = MasterPlayerSettings.Instance;
        if (MasterPlayerPlugin.Instance?.Scripts?.ReplayModeActive == true
            || !(settings.EnableSmartMovement || settings.EnableRadar || settings.EnableRadarOverlay)
            || MasterPlayerPlugin.Instance?.MechanicHost?.HasActiveModule == true)
        {
            ClearPilgrimsFollowUps();
            return;
        }
        var now = DateTime.UtcNow;
        var territory = Svc.ClientState.TerritoryType;
        foreach (var source in _pilgrimsFollowUps.Where(pair => pair.Value.Expires < now || pair.Value.Territory != territory)
            .Select(pair => pair.Key).ToArray())
            RemovePilgrimsFollowUp(source);
    }

    private void ClearPilgrimsFollowUps()
    {
        foreach (var source in _pilgrimsFollowUps.Keys.ToArray())
            RemovePilgrimsFollowUp(source);
    }

    private void RemovePilgrimsFollowUp(ulong source)
    {
        if (!_pilgrimsFollowUps.Remove(source, out var sequence))
            return;
        foreach (var handle in new[] { sequence.FirstHandle, sequence.FollowHandle })
        {
            if (!handle.IsValid)
                continue;
            MasterPlayerPlugin.Instance!.SafeZone.Registry.Remove(handle);
            MasterPlayerPlugin.Instance.AoeVisibility?.Remove(handle);
            _registeredHandles.Remove(handle);
        }
    }
}
