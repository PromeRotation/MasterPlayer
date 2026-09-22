using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using PromeRotation.Helpers;
using PromeRotation.LogSystem;
using PromeRotation.Managers.PartyPriority;
using PromeRotation.Timeline.Core;
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterPlayer.Replay;

internal sealed class ReplayWorldCapture
{
    private readonly Dictionary<ulong, uint> _animations = [];
    private ushort? _territory;

    public void Observe(LogSystemEvent ev)
    {
        switch (ev)
        {
            case LogSystemEObjAnimationEvent a: _animations[a.SourceId] = ((uint)a.Param1 << 16) | a.Param2; break;
            case LogSystemObjectRemoveEvent a: _animations.Remove(a.SourceId); break;
            case LogSystemTerritoryEvent t: _animations.Clear(); _territory = (ushort)t.TerritoryId; break;
        }
    }

    internal static ulong Id(IGameObject? obj)
        => obj == null ? 0 : obj.EntityId is not (0 or 0xE0000000) ? obj.EntityId : obj.GameObjectId;

    internal static ReplayPartyMember[] CaptureParty()
        => Enum.GetValues<PartyPrioritySlot>().Select(slot => (slot, member: PartyHelper.GetSortedPartyMember(slot)))
            .Where(p => p.member != null).Select(p => new ReplayPartyMember((int)p.slot, p.slot.ToString(), Id(p.member))).ToArray();

    public unsafe ReplayWorldFrame Capture(DateTime now)
    {
        var territory = (ushort)Svc.ClientState.TerritoryType;
        if (_territory is { } previous && previous != territory) _animations.Clear();
        _territory = territory;
        var actors = new List<ReplayActorState>();
        foreach (var obj in Svc.Objects)
        {
            if (obj.Address == 0 || Id(obj) is 0 or 0xE0000000) continue;
            var native = (NativeGameObject*)obj.Address;
            var id = Id(obj);
            var actor = new ReplayActorState
            {
                Id = id, EntityId = obj.EntityId, GameObjectId = obj.GameObjectId, Oid = obj.BaseId,
                Name = obj.Name.TextValue, Kind = (byte)obj.ObjectKind, SubKind = obj.SubKind, OwnerId = obj.OwnerId,
                Position = obj.Position, Rotation = obj.Rotation, HitboxRadius = obj.HitboxRadius,
                Dead = obj.IsDead, Targetable = obj.IsTargetable, TargetId = obj.TargetObjectId,
                EventState = native->EventState, EventAnimation = _animations.TryGetValue(id, out var animation) ? animation : null
            };
            if (obj is IBattleChara battle)
            {
                var battleNative = (NativeBattleChara*)obj.Address;
                var tethers = new List<ReplayTether>();
                foreach (var tether in battleNative->Vfx.Tethers)
                    if (tether.Id != 0) tethers.Add(new(tether.Id, tether.TargetId, tether.Progress));
                ReplayCast? cast = null;
                if (battle.IsCasting && battle.CastActionId != 0 && float.IsFinite(battle.CurrentCastTime) && float.IsFinite(battle.TotalCastTime))
                {
                    var info = &battleNative->CastInfo;
                    var p = info->TargetLocation;
                    cast = new(battle.CastActionId, battle.CastTargetObjectId, new(p.X, p.Y, p.Z),
                        float.IsFinite(battleNative->CastRotation) ? battleNative->CastRotation : null,
                        now.AddSeconds(-battle.CurrentCastTime), now.AddSeconds(Math.Max(0, battle.TotalCastTime - battle.CurrentCastTime) + 0.3),
                        battle.IsCastInterruptible, info->ResponseActionId == battle.CastActionId && info->ResponseGlobalSequence != 0);
                }
                actor = actor with
                {
                    NameId = battle.NameId, InCombat = (battle.StatusFlags & StatusFlags.InCombat) != 0,
                    Hostile = (battle.StatusFlags & StatusFlags.Hostile) != 0,
                    Level = battle.Level, ClassJob = battle.ClassJob.RowId,
                    Hp = battle.CurrentHp, MaxHp = battle.MaxHp, Mp = battle.CurrentMp, MaxMp = battle.MaxMp, Shield = battle.ShieldPercentage,
                    ModelState = battleNative->Timeline.ModelState, AnimationState1 = battleNative->Timeline.AnimationState[0],
                    AnimationState2 = battleNative->Timeline.AnimationState[1], Cast = cast, Tethers = tethers.ToArray(),
                    Statuses = battle.StatusList.Where(s => s.StatusId != 0).Select(s => new ReplayStatus(s.StatusId, s.Param, s.SourceId,
                        ReplayStatus.Expiration(now, s.RemainingTime))).ToArray()
                };
            }
            actors.Add(actor);
        }
        var seen = actors.Select(a => a.Id).ToHashSet();
        foreach (var id in _animations.Keys.Where(id => !seen.Contains(id)).ToArray()) _animations.Remove(id);
        return new()
        {
            Full = true, Territory = territory, LocalPlayerId = Id(Svc.Objects.LocalPlayer), TargetId = Id(Svc.Targets.Target),
            LocalTimelineRole = TimelineRoleManager.CurrentRole.ToString(), Party = CaptureParty(), Actors = actors.ToArray()
        };
    }
}
