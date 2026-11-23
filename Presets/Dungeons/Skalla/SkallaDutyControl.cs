using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class SkallaDutyControl
{
    private DateTime nextAttempt;
    internal int Attempts { get; private set; }

    internal void Clear()
    {
        nextAttempt = default;
        Attempts = 0;
    }

    internal void Update(DungeonEncounter encounter, ReplayActorState? player, bool allowed, DateTime now, Func<Vector3, bool> cast)
    {
        if (!allowed || player is not { Dead: false } || encounter.Active?.BossOid != 0x1FAC)
        {
            Clear();
            return;
        }
        if (player.Cast != null || now < nextAttempt || encounter.SkallaShatterstoneTarget is not { } target) return;
        nextAttempt = now.AddSeconds(.5);
        ++Attempts;
        cast(target.Position);
    }

    internal static unsafe bool Cast(Vector3 position)
    {
        if (PlayerPerspective.IsReplay || !Settings.MasterPlayerSettings.Instance.EnableSmartMovement
            || MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed != true) return false;
        var actions = ActionManager.Instance();
        if (actions == null || actions->GetActionStatus(ActionType.Action, 9823) != 0) return false;
        return actions->UseActionLocation(ActionType.Action, 9823, 0, &position);
    }
}
