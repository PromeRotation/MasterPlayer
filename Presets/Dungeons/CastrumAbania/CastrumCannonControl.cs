using System.Numerics;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using MasterPlayer.Replay;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class CastrumCannonControl
{
    private DateTime nextInteraction;
    private DateTime nextPulse;
    internal int InteractionAttempts { get; private set; }
    internal int PulseAttempts { get; private set; }

    internal void Clear()
    {
        nextInteraction = nextPulse = default;
        InteractionAttempts = PulseAttempts = 0;
    }

    internal void Update(CastrumAbaniaEncounter encounter, ReplayActorState player, bool allowed, DateTime now,
        Func<ulong, bool> interact, Func<Vector3, bool> pulse)
    {
        if (!allowed || player.Dead || !encounter.HaywireActive)
        {
            Clear();
            return;
        }
        var cannon = encounter.Actors.Values.Where(a => a.Oid == CastrumAbaniaCatalog.Cannon && !a.Dead)
            .MinBy(a => Vector3.DistanceSquared(a.Position, player.Position));
        var boss = encounter.Actors.Values.FirstOrDefault(a => a.Oid == CastrumAbaniaCatalog.MagnaRoader && !a.Dead);
        if (cannon == null || boss == null || Vector3.DistanceSquared(cannon.Position, player.Position) >= 12.25f || player.Cast != null) return;
        // 占用炮台后对象可能不可选中。先按游戏动作可用性发炮，再尝试进入仍可交互的炮台。
        if (now >= nextPulse)
        {
            nextPulse = now.AddSeconds(.5);
            ++PulseAttempts;
            if (pulse(boss.Position)) return;
        }
        if (cannon.Targetable && now >= nextInteraction)
        {
            nextInteraction = now.AddSeconds(1.1);
            ++InteractionAttempts;
            interact(cannon.Id);
        }
    }

    internal static unsafe bool Interact(ulong id)
    {
        if (!ExecutionAllowed()) return false;
        var player = PlayerPerspective.LivePlayer;
        var target = Svc.Objects.FirstOrDefault(a => a.EntityId == id || a.GameObjectId == id);
        if (player == null || player.IsDead || player.Address == 0 || target == null || target.Address == 0
            || target.BaseId != CastrumAbaniaCatalog.Cannon || target.IsDead || !target.IsTargetable) return false;
        var events = EventFramework.Instance();
        var system = TargetSystem.Instance();
        if (events == null || system == null
            || !events->CheckInteractRange((NativeGameObject*)player.Address, (NativeGameObject*)target.Address, 1, false)) return false;
        system->InteractWithObject((NativeGameObject*)target.Address, false);
        return true;
    }

    internal static unsafe bool Pulse(Vector3 position)
    {
        if (!ExecutionAllowed()) return false;
        var actions = ActionManager.Instance();
        if (actions == null || actions->GetActionStatus(ActionType.Action, 7962) != 0) return false;
        return actions->UseActionLocation(ActionType.Action, 7962, 0, &position);
    }

    private static bool ExecutionAllowed()
        => !PlayerPerspective.IsReplay && Settings.MasterPlayerSettings.Instance.EnableSmartMovement
            && MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed == true;
}
