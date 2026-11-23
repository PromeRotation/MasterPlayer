using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using MasterPlayer.Replay;
using MasterPlayer.Settings;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using NativeTreasure = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed class PalaceFloorInteraction
{
    private DateTime nextAttempt;

    internal void Update(DateTime now, ReplayActorState player, ReplayActorState? target, IReadOnlyList<PalaceArea> areas)
    {
        if (target == null || player.Cast != null || now < nextAttempt
            || areas.Any(a => !a.Safe && !a.PreviewOnly && !a.Obstacle && a.Geometry.Contains(player.Position))) return;
        nextAttempt = now.AddSeconds(1.1);
        Interact(target.Id);
    }

    private static unsafe void Interact(ulong id)
    {
        if (PlayerPerspective.IsReplay || !MasterPlayerSettings.Instance.EnableSmartMovement
            || MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed != true
            || Svc.ClientState.TerritoryType is < 603 or > 607) return;
        var player = PlayerPerspective.LivePlayer;
        var target = Svc.Objects.FirstOrDefault(a => a.EntityId == id || a.GameObjectId == id);
        if (player == null || player.IsDead || player.Address == 0 || player.IsCasting
            || target == null || !target.IsTargetable || target.IsDead || target.Address == 0
            || !PalaceFloorNavigation.IsCoffer(target.BaseId)) return;
        var native = (NativeGameObject*)target.Address;
        if (target.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Treasure
            && (((NativeTreasure*)native)->Flags & NativeTreasure.TreasureFlags.Opened) != 0) return;
        var system = TargetSystem.Instance();
        var events = EventFramework.Instance();
        if (system == null || events == null
            || !events->CheckInteractRange((NativeGameObject*)player.Address, native, 1, false)) return;
        system->InteractWithObject(native, false);
    }
}
