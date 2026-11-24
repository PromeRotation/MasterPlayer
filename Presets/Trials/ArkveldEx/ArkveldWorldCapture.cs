// 护锁刃龙上位狩猎战（Arkveld Extreme）· 个人走位。原单文件脚本 ArkveldExtreme.cs 按段拆分。
/*
BSD 3-Clause License

Copyright (c) 2022-2024, Andrew Gilewsky

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its
   contributors may be used to endorse or promote products derived from
   this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using System.Numerics;

namespace MasterPlayer.Mechanics.Arkveld
{
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using PromeRotation.Helpers;
using PromeRotation.LogSystem;
using PromeRotation.Managers.PartyPriority;
using PromeRotation.Timeline.Core;
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

using MasterPlayer.Replay;

internal sealed class ReplayWorldCapture
{
    private readonly Dictionary<ulong, uint> _animations = [];
    private ushort? _territory;
    private static DateTime Expiration(DateTime now, float remaining)
        => !float.IsFinite(remaining) || remaining == 0 ? DateTime.MaxValue
            : now.AddSeconds(Math.Min(MathF.Abs(remaining), 100000f));

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

    public unsafe ReplayWorldFrame Capture(DateTime now, ulong currentPlayerId)
    {
        var territory = (ushort)Svc.ClientState.TerritoryType;
        if (_territory is { } previous && previous != territory) _animations.Clear();
        _territory = territory;
        var actors = new List<ReplayActorState>();
        foreach (var obj in Svc.Objects)
        {
            if (obj.Address == 0 || Id(obj) is 0 or 0xE0000000) continue;
            var id = Id(obj);
            actors.Add(CaptureActor(obj, now, _animations.TryGetValue(id, out var animation) ? animation : null));
        }
        var seen = actors.Select(a => a.Id).ToHashSet();
        foreach (var id in _animations.Keys.Where(id => !seen.Contains(id)).ToArray()) _animations.Remove(id);
        return new()
        {
            Full = true, Territory = territory, LocalPlayerId = currentPlayerId, TargetId = Id(Svc.Targets.Target),
            LocalTimelineRole = TimelineRoleManager.CurrentRole.ToString(), Party = CaptureParty(), Actors = actors.ToArray()
        };
    }

    internal static unsafe ReplayActorState CaptureActor(IGameObject obj, DateTime now, uint? animation = null)
    {
        var native = (NativeGameObject*)obj.Address;
        var id = Id(obj);
        var actor = new ReplayActorState
        {
            Id = id, EntityId = obj.EntityId, GameObjectId = obj.GameObjectId, Oid = obj.BaseId,
            Name = obj.Name.TextValue, Kind = (byte)obj.ObjectKind, SubKind = obj.SubKind, OwnerId = obj.OwnerId,
            Position = obj.Position, Rotation = obj.Rotation, HitboxRadius = obj.HitboxRadius,
            Dead = obj.IsDead, Targetable = obj.IsTargetable, TargetId = obj.TargetObjectId,
            EventState = native != null ? native->EventState : (byte)0, EventAnimation = animation
        };
        if (obj is IBattleChara battle)
        {
            var battleNative = (NativeBattleChara*)obj.Address;
            var tethers = new List<ReplayTether>();
            foreach (var tether in battleNative != null ? battleNative->Vfx.Tethers : [])
                if (tether.Id != 0) tethers.Add(new(tether.Id, tether.TargetId, tether.Progress));
            ReplayCast? cast = null;
            if (battleNative != null && battle.IsCasting && battle.CastActionId != 0 && float.IsFinite(battle.CurrentCastTime) && float.IsFinite(battle.TotalCastTime))
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
                ModelState = battleNative != null ? battleNative->Timeline.ModelState : (byte)0, AnimationState1 = battleNative != null ? battleNative->Timeline.AnimationState[0] : (byte)0,
                AnimationState2 = battleNative != null ? battleNative->Timeline.AnimationState[1] : (byte)0, Cast = cast, Tethers = tethers.ToArray(),
                Statuses = battle.StatusList.Where(s => s.StatusId != 0).Select(s => new ReplayStatus(s.StatusId, s.Param, s.SourceId,
                    Expiration(now, s.RemainingTime))).ToArray()
            };
        }
        return actor;
    }
}

}
