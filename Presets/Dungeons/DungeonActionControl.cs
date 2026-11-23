using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class DungeonActionControl
{
    private DateTime nextAttempt;
    internal void Clear() => nextAttempt = default;

    internal void Update(IReadOnlyList<DungeonActionRequest> requests, bool allowed, ReplayActorState? player, DateTime now,
        Func<DungeonActionRequest, bool> execute)
    {
        if (!allowed || player is not { Dead: false }) { Clear(); return; }
        if (now < nextAttempt) return;
        foreach (var request in requests)
        {
            if (request.Until <= now || !execute(request)) continue;
            nextAttempt = now.AddSeconds(.1);
            break;
        }
    }
}
