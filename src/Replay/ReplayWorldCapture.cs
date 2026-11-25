using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using ECommons.DalamudServices;
using PromeRotation.Helpers;
using PromeRotation.LogSystem;
using PromeRotation.Managers.PartyPriority;
using PromeRotation.Timeline.Core;
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using NativeTreasure = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure;
using NativeGroupManager = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager;
using NativeActionManager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager;
using NativeCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace MasterPlayer.Replay;

internal sealed class ReplayWorldCapture
{
    private static (uint Territory, nint Director, byte Dungeon, byte Floor)? deepDungeonFloor;
    private static bool? deepDungeonTrapsHidden;
    private readonly Dictionary<ulong, uint> _animations = [];
    private ushort? _territory;

    public void Observe(LogSystemEvent ev)
    {
        switch (ev)
        {
            case LogSystemEObjAnimationEvent a: _animations[a.SourceId] = ((uint)a.Param1 << 16) | a.Param2; break;
            case LogSystemObjectRemoveEvent a: _animations.Remove(a.SourceId); break;
            case LogSystemTerritoryEvent t:
                _animations.Clear();
                _territory = (ushort)t.TerritoryId;
                deepDungeonFloor = null;
                deepDungeonTrapsHidden = null;
                break;
        }
    }

    internal static ulong Id(IGameObject? obj)
        => obj == null ? 0 : obj.EntityId is not (0 or 0xE0000000) ? obj.EntityId : obj.GameObjectId;

    internal static unsafe ReplayPartyMember[] CaptureParty()
    {
        var result = new List<ReplayPartyMember>();
        var seen = new HashSet<ulong>();
        foreach (var slot in Enum.GetValues<PartyPrioritySlot>())
        {
            var id = Id(PartyHelper.GetSortedPartyMember(slot));
            if (id is not (0 or 0xE0000000) && seen.Add(id)) result.Add(new((int)slot, slot.ToString(), id));
        }
        var manager = (NativeGroupManager*)(Svc.Party?.GroupManagerAddress ?? 0);
        if (manager == null) return result.ToArray();
        var group = DutyReplayState.Active ? &manager->ReplayGroup : &manager->MainGroup;
        // PR resolves actors for its priority slots. A roster member may still exist after its actor disappears.
        var occupied = result.Select(p => p.Slot).ToHashSet();
        for (var index = 0; index < Math.Min(8, (int)group->MemberCount); ++index)
        {
            var id = group->PartyMembers[index].EntityId;
            if (id is 0 or 0xE0000000 || !seen.Add(id)) continue;
            var slot = Enumerable.Range(0, 8).FirstOrDefault(s => !occupied.Contains(s), -1);
            if (slot < 0) break;
            occupied.Add(slot);
            result.Add(new(slot, ((PartyPrioritySlot)slot).ToString(), id));
        }
        // Match the source's regular 3x8 alliance layout. The 6x4 layout uses different slot semantics.
        if (!group->IsAlliance || group->IsSmallGroupAlliance) return result.ToArray();
        for (var index = 0; index < 16; ++index)
        {
            ref var member = ref group->AllianceMembers[index];
            var id = member.EntityId;
            if ((member.Flags & 1) != 0 && id is not (0 or 0xE0000000) && seen.Add(id))
                result.Add(new(8 + index, "Alliance", id));
        }
        return result.ToArray();
    }

    public unsafe ReplayWorldFrame Capture(DateTime now, ulong currentPlayerId)
    {
        var territory = (ushort)Svc.ClientState.TerritoryType;
        if (_territory is { } previous && previous != territory) _animations.Clear();
        _territory = territory;
        var actors = new List<ReplayActorState>();
        var enmity = CapturePlayerEnmity();
        foreach (var obj in Svc.Objects)
        {
            if (obj.Address == 0 || Id(obj) is 0 or 0xE0000000) continue;
            var id = Id(obj);
            actors.Add(CaptureActor(obj, now, _animations.TryGetValue(id, out var animation) ? animation : null, enmity));
        }
        var seen = actors.Select(a => a.Id).ToHashSet();
        foreach (var id in _animations.Keys.Where(id => !seen.Contains(id)).ToArray()) _animations.Remove(id);
        return new()
        {
            Full = true, Territory = territory, LocalPlayerId = currentPlayerId, TargetId = Id(Svc.Targets.Target),
            SourcePlayerId = CaptureSourcePlayerId(),
            LegacyMovement = Svc.GameConfig.UiControl.TryGetUInt("MoveMode", out var mode) && mode == 1,
            CameraAzimuth = CaptureCameraAzimuth(),
            AllowedFateId = CaptureAllowedFate(actors.FirstOrDefault(a => a.Id == currentPlayerId)?.Level),
            DutyActionPlayerId = currentPlayerId, DutyActions = CaptureDutyActions(currentPlayerId),
            DeepDungeon = CaptureDeepDungeon(),
            LocalTimelineRole = TimelineRoleManager.CurrentRole.ToString(), Party = CaptureParty(), Actors = actors.ToArray()
        };
    }

    internal static unsafe ulong? CaptureSourcePlayerId()
    {
        if (DutyReplayState.Active)
        {
            foreach (var obj in Svc.Objects)
                if (obj.Address != 0 && ((NativeGameObject*)obj.Address)->ObjectIndex == 0)
                    return Id(obj);
            return 0;
        }
        var ui = FFXIVClientStructs.FFXIV.Client.Game.UI.UIState.Instance();
        return ui != null ? ui->PlayerState.EntityId : null;
    }

    internal static unsafe uint CaptureAllowedFate(byte? playerLevel)
    {
        var manager = FFXIVClientStructs.FFXIV.Client.Game.Fate.FateManager.Instance();
        if (manager == null || manager->CurrentFate == null) return 0;
        var id = manager->CurrentFate->FateId;
        var fate = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Fate>()?.GetRowOrDefault(id);
        if (fate == null) return 0;
        if (fate.Value.EurekaFate != 1)
            return playerLevel <= fate.Value.ClassJobLevelMax ? id : 0u;
        var ui = FFXIVClientStructs.FFXIV.Client.Game.UI.UIState.Instance();
        return ui != null && ui->PlayerState.GetContentValue(2) <= fate.Value.ClassJobLevelMax ? id : 0u;
    }

    internal static unsafe float? CaptureCameraAzimuth()
    {
        var manager = CameraManager.Instance();
        var camera = manager != null ? manager->GetActiveCamera() : null;
        return camera != null && float.IsFinite(camera->DirH) ? camera->DirH + MathF.PI : null;
    }

    internal static unsafe HashSet<uint> CapturePlayerEnmity()
    {
        var result = new HashSet<uint>();
        var ui = FFXIVClientStructs.FFXIV.Client.Game.UI.UIState.Instance();
        if (ui == null) return result;
        var count = Math.Clamp(ui->Hater.HaterCount, 0, ui->Hater.Haters.Length);
        for (var i = 0; i < count; ++i) result.Add(ui->Hater.Haters[i].EntityId);
        return result;
    }

    internal static unsafe ReplayDeepDungeonState? CaptureDeepDungeon()
    {
        var framework = FFXIVClientStructs.FFXIV.Client.Game.Event.EventFramework.Instance();
        var dungeon = framework != null ? framework->GetInstanceContentDeepDungeon() : null;
        if (dungeon == null)
        {
            deepDungeonFloor = null;
            deepDungeonTrapsHidden = null;
            return null;
        }
        var identity = (Svc.ClientState.TerritoryType, (nint)dungeon, dungeon->DeepDungeonId, dungeon->Floor);
        if (deepDungeonFloor != identity)
        {
            deepDungeonFloor = identity;
            deepDungeonTrapsHidden = null;
        }
        var partyRooms = new Dictionary<ulong, int>();
        foreach (var member in dungeon->Party)
            if (member.EntityId is not (0 or 0xE0000000))
                partyRooms[member.EntityId] = Math.Max(0, (int)member.RoomIndex);
        ReplayDeepDungeonItem[]? items = null;
        if (Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.DeepDungeon>().GetRowOrDefault(dungeon->DeepDungeonId) is { } definition)
        {
            items = new ReplayDeepDungeonItem[dungeon->Items.Length];
            for (var slot = 0; slot < items.Length; ++slot)
            {
                var item = dungeon->Items[slot];
                items[slot] = new(definition.PomanderSlot[slot].RowId, item.Count, item.Flags);
            }
        }
        return new()
        {
            DungeonId = dungeon->DeepDungeonId,
            Floor = dungeon->Floor,
            // LayoutInitializationType identifies the map variant; ActiveLayoutIndex does not.
            Tileset = dungeon->LayoutInitializationType switch { 1 => 0, 2 => 1, 5 => 2, _ => byte.MaxValue },
            PassageProgress = dungeon->PassageProgress,
            WeaponLevel = dungeon->WeaponLevel,
            ArmorLevel = dungeon->ArmorLevel,
            TrapsHidden = deepDungeonTrapsHidden,
            Items = items,
            Rooms = dungeon->MapData.ToArray().Select(flags => (ushort)flags).ToArray(),
            PartyRooms = partyRooms,
        };
    }

    internal static void ObserveDeepDungeonSystemLog(uint message)
    {
        if (message is not (7248 or 7255 or 7256)) return;
        // Establish this director/floor identity before applying a log that can arrive
        // before the first object sample. Otherwise initialization would erase Safety.
        CaptureDeepDungeon();
        if (message == 7248) deepDungeonTrapsHidden = true;
        else if (message is 7255 or 7256) deepDungeonTrapsHidden = false;
    }

    internal static unsafe ReplayDutyAction[]? CaptureDutyActions(ulong playerId)
    {
        // Client charges belong only to the real local player, never an ARR viewing subject.
        if (PlayerPerspective.IsReplay || playerId == 0 || Id(Svc.Objects.LocalPlayer) != playerId) return null;
        var manager = DutyActionManager.GetInstanceIfReady();
        if (manager == null) return null;
        if (!manager->ActionsPresent) return [];
        var slots = Math.Min((int)manager->NumValidSlots, 2);
        var result = new ReplayDutyAction[slots];
        for (ushort slot = 0; slot < slots; ++slot)
            result[slot] = new(slot, DutyActionManager.GetDutyActionId(slot), manager->CurCharges[slot], manager->MaxCharges[slot]);
        return result;
    }

    internal static unsafe ReplayActorState CaptureActor(IGameObject obj, DateTime now, uint? animation = null,
        IReadOnlySet<uint>? playerEnmity = null)
    {
        var native = (NativeGameObject*)obj.Address;
        var id = Id(obj);
        var actor = new ReplayActorState
        {
            Id = id, EntityId = obj.EntityId, GameObjectId = obj.GameObjectId, Oid = obj.BaseId,
            Name = obj.Name.TextValue, Kind = (byte)obj.ObjectKind, SubKind = obj.SubKind, OwnerId = obj.OwnerId,
            ObjectIndex = native != null ? native->ObjectIndex : null,
            FateId = native != null ? native->FateId : null,
            IsEnemy = native != null
                ? native->IsCharacter() && NativeActionManager.ClassifyTarget((NativeCharacter*)native) == NativeActionManager.TargetCategory.Enemy
                : null,
            Position = obj.Position, Rotation = obj.Rotation, HitboxRadius = obj.HitboxRadius,
            Dead = obj.IsDead, Targetable = obj.IsTargetable, TargetId = obj.TargetObjectId,
            AggroPlayer = playerEnmity?.Contains(obj.EntityId) == true,
            EventState = native != null ? native->EventState : (byte)0, EventAnimation = animation,
            OpenedTreasure = native != null && obj.ObjectKind == ObjectKind.Treasure
                && (((NativeTreasure*)native)->Flags & NativeTreasure.TreasureFlags.Opened) != 0,
            RenderFlags = native != null ? (int)native->RenderFlags : null
        };
        if (obj is IBattleChara battle)
        {
            var battleNative = (NativeBattleChara*)obj.Address;
            var tethers = new List<ReplayTether>();
            var tetherSlot = 0;
            foreach (var tether in battleNative != null ? battleNative->Vfx.Tethers : [])
            {
                if (tether.Id != 0) tethers.Add(new(tether.Id, tether.TargetId, tether.Progress) { SlotIndex = tetherSlot });
                ++tetherSlot;
            }
            ReplayCast? cast = null;
            if (battleNative != null && battle.IsCasting && battle.CastActionId != 0 && float.IsFinite(battle.CurrentCastTime) && float.IsFinite(battle.TotalCastTime))
            {
                var info = &battleNative->CastInfo;
                var p = info->TargetLocation;
                cast = new(battle.CastActionId, battle.CastTargetObjectId, new(p.X, p.Y, p.Z),
                    float.IsFinite(battleNative->CastRotation) ? battleNative->CastRotation : null,
                    now.AddSeconds(-battle.CurrentCastTime), now.AddSeconds(Math.Max(0, battle.TotalCastTime - battle.CurrentCastTime) + 0.3),
                    battle.IsCastInterruptible, info->ResponseActionId == battle.CastActionId && info->ResponseGlobalSequence != 0)
                {
                    TotalTime = battle.TotalCastTime,
                    IsSpell = battle.CastActionType == 1
                };
            }
            actor = actor with
            {
                NameId = battle.NameId, InCombat = (battle.StatusFlags & StatusFlags.InCombat) != 0,
                Hostile = (battle.StatusFlags & StatusFlags.Hostile) != 0,
                Level = battle.Level, ClassJob = battle.ClassJob.RowId,
                Hp = battle.CurrentHp, MaxHp = battle.MaxHp, Mp = battle.CurrentMp, MaxMp = battle.MaxMp, Shield = battle.ShieldPercentage,
                ModelState = battleNative != null ? battleNative->Timeline.ModelState : (byte)0, AnimationState1 = battleNative != null ? battleNative->Timeline.AnimationState[0] : (byte)0,
                AnimationState2 = battleNative != null ? battleNative->Timeline.AnimationState[1] : (byte)0, Cast = cast, Tethers = tethers.ToArray(),
                IncomingEffects = CaptureIncomingEffects(battleNative),
                Statuses = CaptureStatuses(battle.StatusList, now)
            };
        }
        return actor;
    }

    internal static ReplayStatus[] CaptureStatuses(IEnumerable<IStatus> statuses, DateTime now)
    {
        var result = new List<ReplayStatus>();
        var slot = 0;
        foreach (var status in statuses)
        {
            var id = status.StatusId;
            if (id != 0)
            {
                var remaining = status.RemainingTime;
                result.Add(new(id, status.Param, status.SourceId, ReplayStatus.Expiration(now, remaining))
                {
                    SlotIndex = slot,
                    ZeroRemainingTime = remaining == 0 ? true : null
                });
            }
            ++slot;
        }
        return result.ToArray();
    }

    internal static unsafe ReplayIncomingEffect[] CaptureIncomingEffects(NativeBattleChara* character)
    {
        var handler = character != null ? character->GetActionEffectHandler() : null;
        if (handler == null) return [];
        var actorId = (ulong)character->GetGameObjectId();
        List<ReplayIncomingEffect>? incoming = null;
        for (var slot = 0; slot < 32; ++slot)
        {
            ref readonly var entry = ref handler->IncomingEffects[slot];
            if (entry.GlobalSequence == 0) continue;
            var effects = entry.Effects;
            var raw = (ulong*)&effects;
            var knockback = false;
            var hpDelta = 0;
            for (var i = 0; i < 8; ++i)
            {
                knockback |= ReplayIncomingEffect.IsKnockback(raw[i]);
                hpDelta += ReplayIncomingEffect.PendingHpDelta(raw[i], actorId, entry.Source, entry.Target,
                    entry.SourceConfirmed, entry.TargetConfirmed);
            }
            (incoming ??= []).Add(new(slot, entry.GlobalSequence, (byte)entry.TargetIndex, entry.TargetConfirmed, knockback)
                { HpDelta = hpDelta });
        }
        return incoming?.ToArray() ?? [];
    }
}
