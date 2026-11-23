using System.Numerics;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class DungeonObjectInteraction
{
    private ulong targetId;
    private DateTime nextAttempt;
    private DungeonActor? interactTarget;
    internal bool Attempted { get; private set; }
    internal bool Holding { get; private set; }

    internal void Clear()
    {
        targetId = 0;
        nextAttempt = default;
        interactTarget = null;
        Attempted = Holding = false;
    }

    internal void Prepare(DungeonActor? target, Vector3? player, bool allowed)
    {
        if (!allowed || player == null || target is not { Dead: false, Targetable: true }
            || !Supported(target.Oid) || target.EventState == 7)
        {
            Clear();
            return;
        }
        if (targetId != target.Id)
        {
            Clear();
            targetId = target.Id;
        }
        interactTarget = target;
        Holding = Vector2.DistanceSquared(new(player.Value.X, player.Value.Z),
            new(target.Position.X, target.Position.Z)) <= MathF.Pow(2 + target.HitboxRadius, 2);
    }

    internal bool Update(DateTime now, bool casting, Func<ulong, bool> interact)
    {
        if (!Holding || interactTarget == null || casting || now < nextAttempt) return false;
        nextAttempt = now.AddSeconds(1.1);
        if (!interact(interactTarget.Id)) return false;
        Attempted = true;
        return true;
    }

    internal static unsafe bool Interact(ulong id)
    {
        if (PlayerPerspective.IsReplay || !Settings.MasterPlayerSettings.Instance.EnableSmartMovement
            || MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed != true) return false;
        var player = PlayerPerspective.LivePlayer;
        var target = Svc.Objects.FirstOrDefault(a => a.EntityId == id || a.GameObjectId == id);
        if (player == null || player.IsDead || player.Address == 0 || target == null || target.IsDead
            || !target.IsTargetable || target.Address == 0 || !Supported(target.BaseId)
            || ((NativeGameObject*)target.Address)->EventState == 7) return false;
        var system = TargetSystem.Instance();
        var events = EventFramework.Instance();
        if (system == null || events == null
            || !events->CheckInteractRange((NativeGameObject*)player.Address, (NativeGameObject*)target.Address, 1, false)) return false;
        system->InteractWithObject((NativeGameObject*)target.Address, false);
        return true;
    }

    private static bool Supported(uint oid) => AurumValeCatalog.IsFruit(oid) || FractalCatalog.IsInteraction(oid) || CosmosCatalog.IsSeed(oid);
}
