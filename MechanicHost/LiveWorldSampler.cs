using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using MasterPlayer.MechanicRuntime;
using NativeActionEffectHandler = FFXIVClientStructs.FFXIV.Client.Game.Character.ActionEffectHandler;
using NativeActionManager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager;
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using NativeCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterPlayer.MechanicHost;

internal static class LiveWorldSampler
{
    private const byte SpellActionType = 1;
    private static ushort _cachedTerritory;
    private static ushort _cachedCfc;

    public static unsafe MechanicWorldFrame Capture((byte Id, Vector3? Pos)[] waymarks)
    {
        var territory = (ushort)Svc.ClientState.TerritoryType;
        var localPlayer = Svc.Objects.LocalPlayer;
        var incoming = new List<MechanicActorIncomingEffects>();
        var actors = CaptureActors(incoming);
        return new(territory, ResolveCfc(territory), Id(localPlayer), Id(Svc.Targets.Target),
            CaptureParty(localPlayer), waymarks, actors) { IncomingEffects = [.. incoming] };
    }

    internal static ulong Id(IGameObject? obj)
    {
        if (obj == null)
            return 0;
        var id = obj.EntityId is not (0 or 0xE0000000) ? (ulong)obj.EntityId : obj.GameObjectId;
        return id is 0 or 0xE0000000 || (id >> 56) == 0xFF ? 0 : id;
    }

    private static unsafe MechanicActorSnapshot[] CaptureActors(List<MechanicActorIncomingEffects> incoming)
    {
        var table = Svc.Objects;
        var actors = new List<MechanicActorSnapshot>(table.Length);
        for (var i = 0; i < table.Length; ++i)
        {
            IGameObject? obj;
            try
            {
                obj = table[i];
            }
            catch
            {
                continue;
            }
            if (obj == null || obj.Address == nint.Zero)
                continue;
            var id = Id(obj);
            if (id == 0)
                continue;

            var native = (NativeGameObject*)obj.Address;
            var chrNative = native->IsCharacter() ? (NativeCharacter*)native : null;
            var battle = obj as IBattleChara;
            var battleNative = battle != null ? (NativeBattleChara*)obj.Address : null;
            var isAlly = chrNative == null
                || NativeActionManager.ClassifyTarget(chrNative) != NativeActionManager.TargetCategory.Enemy;

            MechanicCastSnapshot? cast = null;
            var statuses = Array.Empty<MechanicStatusDto>();
            uint curHp = 0, maxHp = 0, curMp = 0, maxMp = 0;
            byte shield = 0, modelState = 0, animState1 = 0, animState2 = 0;
            uint nameId = 0, classJobId = 0;
            int level = 0;
            var inCombat = false;
            uint tetherId = 0;
            ulong tetherTargetId = 0;
            if (battle != null && battleNative != null)
            {
                curHp = battle.CurrentHp;
                maxHp = battle.MaxHp;
                curMp = battle.CurrentMp;
                maxMp = battle.MaxMp;
                shield = battle.ShieldPercentage;
                nameId = battle.NameId;
                classJobId = battle.ClassJob.RowId;
                level = battle.Level;
                inCombat = battleNative->InCombat;
                modelState = battleNative->Timeline.ModelState;
                animState1 = battleNative->Timeline.AnimationState[0];
                animState2 = battleNative->Timeline.AnimationState[1];
                ref readonly var tether = ref battleNative->Vfx.Tethers[0];
                if (tether.Id != 0)
                {
                    tetherId = tether.Id;
                    tetherTargetId = tether.TargetId;
                }
                if (battle.IsCasting && battle.CastActionId != 0
                    && float.IsFinite(battle.CurrentCastTime) && float.IsFinite(battle.TotalCastTime))
                {
                    ref readonly var castInfo = ref battleNative->CastInfo;
                    cast = new(battle.CastActionId, battle.CastActionType == SpellActionType, castInfo.TargetId,
                        float.IsFinite(battleNative->CastRotation) ? battleNative->CastRotation : 0f,
                        castInfo.TargetLocation, battle.TotalCastTime, battle.IsCastInterruptible);
                }
                var statusList = battle.StatusList;
                var statusCount = statusList.Length;
                if (statusCount > 0)
                {
                    var list = new List<MechanicStatusDto>(statusCount);
                    for (var s = 0; s < statusCount; ++s)
                    {
                        var status = statusList[s];
                        if (status is { StatusId: not 0 })
                            list.Add(new(s, status.StatusId, status.Param, status.SourceId, status.RemainingTime));
                    }
                    statuses = list.ToArray();
                }
            }

            actors.Add(new(id, obj.BaseId, i, native->LayoutId, obj.Name.TextValue, nameId,
                (byte)obj.ObjectKind, obj.SubKind, classJobId, level, obj.Position, obj.Rotation, obj.HitboxRadius,
                curHp, maxHp, shield, curMp, maxMp,
                obj.IsTargetable, isAlly, obj.IsDead, inCombat,
                obj.OwnerId, obj.TargetObjectId, native->FateId, (int)native->RenderFlags, native->EventState,
                modelState, animState1, animState2, tetherId, tetherTargetId, cast, statuses));

            // 原生逐目标 IncomingEffects（按原生槽位扫描；只上报非零槽位，差分在喂养器侧）
            if (chrNative != null)
            {
                var aeh = chrNative->GetActionEffectHandler();
                if (aeh != null)
                {
                    List<MechanicIncomingEffectSlot>? slots = null;
                    for (var s = 0; s < 32; ++s)
                    {
                        ref readonly var entry = ref aeh->IncomingEffects[s];
                        if (entry.GlobalSequence == 0)
                            continue;
                        var raw = new ulong[8];
                        var effectsCopy = entry.Effects;
                        var src = (ulong*)&effectsCopy;
                        for (var j = 0; j < 8; ++j)
                            raw[j] = src[j];
                        (slots ??= new(2)).Add(new(s, entry.GlobalSequence, entry.TargetIndex, entry.ActionType,
                            entry.ActionId, entry.Source, entry.TargetConfirmed, raw));
                    }
                    if (slots != null)
                        incoming.Add(new(id, [.. slots]));
                }
            }
        }
        return actors.ToArray();
    }

    private static MechanicPartySlotDto[] CaptureParty(IGameObject? localPlayer)
    {
        var partyInfo = new Dictionary<ulong, (ulong ContentId, string Name)>();
        try
        {
            for (var i = 0; i < Svc.Party.Length; ++i)
            {
                var member = Svc.Party[i];
                if (member != null && member.EntityId != 0)
                    partyInfo[member.EntityId] = (member.ContentId, member.Name.TextValue);
            }
        }
        catch
        {
        }

        var slots = new List<MechanicPartySlotDto>(8);
        var localId = Id(localPlayer);
        if (localId != 0)
        {
            var contentId = LocalContentId();
            slots.Add(new(0,
                partyInfo.TryGetValue(localId, out var info) && info.ContentId != 0 ? info.ContentId : contentId,
                localId, localPlayer?.Name.TextValue ?? ""));
        }

        List<IBattleChara> party;
        try
        {
            party = PromeRotation.Helpers.PartyHelper.GetParty();
        }
        catch
        {
            party = [];
        }
        for (var i = 1; i < party.Count && slots.Count < 8; ++i)
        {
            var id = Id(party[i]);
            if (id == 0)
                continue;
            slots.Add(new(slots.Count,
                partyInfo.TryGetValue(id, out var info) ? info.ContentId : 0,
                id, party[i].Name.TextValue));
        }
        return slots.ToArray();
    }

    private static ulong LocalContentId()
    {
        try
        {
            if (Svc.PlayerState.IsLoaded)
                return Svc.PlayerState.ContentId;
        }
        catch
        {
        }
        return 0;
    }

    internal static ushort ResolveCfc(ushort territory)
    {
        if (_cachedTerritory == territory)
            return _cachedCfc;
        var cfc = (ushort)0;
        try
        {
            var row = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?.GetRowOrDefault(territory);
            if (row?.ContentFinderCondition.ValueNullable is { } cfcRow)
                cfc = (ushort)cfcRow.RowId;
        }
        catch
        {
        }
        _cachedTerritory = territory;
        _cachedCfc = cfc;
        return cfc;
    }
}
