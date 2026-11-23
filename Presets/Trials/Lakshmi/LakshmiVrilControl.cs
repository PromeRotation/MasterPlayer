using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class LakshmiVrilControl
{
    private DateTime nextAttempt;
    private bool pending;
    private ulong pendingPlayer;
    internal int Attempts { get; private set; }

    internal void Clear()
    {
        nextAttempt = default;
        pending = false;
        pendingPlayer = 0;
        Attempts = 0;
    }

    internal void Update(bool requested, bool allowed, ReplayActorState? player, ulong owner,
        IReadOnlyList<ReplayDutyAction>? actions, DateTime now, Func<ulong, bool> cast)
    {
        if (!allowed || player is not { Dead: false })
        {
            Clear();
            return;
        }
        if (player.Id != owner || actions?.Any(a => a.Slot == 0 && a.ActionId == 9345 && a.CurrentCharges > 0) != true)
        {
            pending = false;
            return;
        }
        if (pendingPlayer != player.Id) pending = false;
        pendingPlayer = player.Id;
        pending |= requested;
        if (!pending || player.Cast != null || now < nextAttempt) return;
        // A one-shot request waits for execution gates instead of disappearing during animation lock.
        if (!cast(player.Id)) return;
        nextAttempt = now.AddSeconds(.5);
        ++Attempts;
        pending = false;
    }
}
