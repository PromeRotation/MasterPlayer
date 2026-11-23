using FFXIVClientStructs.FFXIV.Client.Game;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class CosmosSeedDrop
{
    private DateTime? safeSince;
    private DateTime nextAttempt;
    internal void Clear() { safeSince = null; nextAttempt = default; }
    internal bool Update(DateTime now, bool ready, bool allowed, Func<bool> drop)
    {
        if (!allowed || !ready) { Clear(); return false; }
        safeSince ??= now;
        if (now < safeSince.Value.AddSeconds(.25) || now < nextAttempt) return false;
        nextAttempt = now.AddSeconds(1.1);
        return drop();
    }
    internal static bool Drop()
    {
        if (PlayerPerspective.IsReplay || !Settings.MasterPlayerSettings.Instance.EnableSmartMovement
            || MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed != true || PlayerPerspective.LivePlayer is not { IsDead: false }) return false;
        return StatusManager.ExecuteStatusOff(404);
    }
}
