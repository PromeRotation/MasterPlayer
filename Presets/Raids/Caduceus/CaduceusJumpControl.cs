using FFXIVClientStructs.FFXIV.Client.Game;
using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class CaduceusJumpControl
{
    private DateTime nextAttempt;

    internal void Clear() => nextAttempt = default;

    internal void Update(bool requested, bool allowed, ReplayActorState? player, DateTime now, Func<bool> jump)
    {
        if (!allowed || player is not { Dead: false })
        {
            Clear();
            return;
        }
        if (!requested || now < nextAttempt) return;
        nextAttempt = now.AddSeconds(.1);
        jump();
    }

    internal static unsafe bool Jump()
    {
        if (PlayerPerspective.IsReplay || !Settings.MasterPlayerSettings.Instance.EnableSmartMovement
            || MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed != true) return false;
        var actions = ActionManager.Instance();
        // Installed client GeneralAction[2] is Jump. PR PAction maps ordinary Action IDs instead.
        return actions != null && actions->UseAction(ActionType.GeneralAction, 2);
    }
}
