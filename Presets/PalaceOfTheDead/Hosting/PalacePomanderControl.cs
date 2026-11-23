using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using MasterPlayer.MechanicHost;
using MasterPlayer.Replay;
using MasterPlayer.Settings;
using PromeRotation.Helpers;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed class PalacePomanderControl
{
    private DateTime nextAttempt;

    internal unsafe void Update(DateTime now, (int Slot, uint Id)? request)
    {
        if (request is not { } wanted || now < nextAttempt || PlayerPerspective.IsReplay
            || !MasterPlayerSettings.Instance.EnableSmartMovement
            || MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed != true
            || Svc.ClientState.TerritoryType is < 603 or > 607) return;
        var player = PlayerPerspective.LivePlayer;
        if (player == null || player.IsDead || player.IsCasting || wanted.Slot is < 0 or >= 16
            || MechanicFacingControl.HoldActions || ActionHelper.GetAnimationLock() > 0) return;
        var framework = EventFramework.Instance();
        var dungeon = framework != null ? framework->GetInstanceContentDeepDungeon() : null;
        if (dungeon == null || dungeon->DeepDungeonId != 1 || dungeon->Floor is < 151 or > 199
            || dungeon->Floor % 10 == 0
            || dungeon->Party.ToArray().Count(p => p.EntityId is not (0 or 0xE0000000)) > 1) return;
        var item = dungeon->Items[wanted.Slot];
        var definition = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.DeepDungeon>().GetRowOrDefault(1);
        if (definition == null || definition.Value.PomanderSlot[wanted.Slot].RowId != wanted.Id
            || item.Count != 3 || !item.IsUsable) return;
        // UsePomander accepts the mapped inventory slot, not DeepDungeonItem's ID.
        nextAttempt = now.AddSeconds(1.1);
        dungeon->UsePomander((uint)wanted.Slot);
    }

    internal void Reset() => nextAttempt = default;
}
