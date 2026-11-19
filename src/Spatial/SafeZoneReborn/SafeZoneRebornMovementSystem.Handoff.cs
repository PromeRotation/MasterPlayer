using System.Numerics;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

namespace MasterPlayer.Spatial.SafeZoneReborn;

internal sealed partial class SafeZoneRebornMovementSystem
{
    private readonly HashSet<SafeZoneAreaHandle> handoffDangers = [];
    internal Func<bool, bool>? RequestExclusiveMovement { get; set; }
    public bool MechanicMovementActive { get; private set; }

    private bool PrepareMovementHandoff(SafeZoneSnapshot snapshot, PlayerState player, DateTime now, bool routeRequested)
    {
        var horizon = now.AddSeconds(_options.PlanningHorizon);
        var endedBefore = now.AddSeconds(-_options.DangerEndSafetyTime);
        handoffDangers.RemoveWhere(handle => !snapshot.DangerAreas.Any(danger => danger.Handle == handle &&
            danger.DangerWindows.Any(window => window.DangerFrom <= horizon &&
                (window.DangerUntil > endedBefore || window.DangerUntil == null && danger.Geometry.Contains(player.Position)))));
        foreach (var danger in snapshot.DangerAreas)
        {
            if (danger.Geometry.Contains(player.Position) && danger.DangerWindows.Any(window =>
                window.DangerFrom <= horizon && (window.DangerUntil == null || window.DangerUntil > endedBefore)))
                handoffDangers.Add(danger.Handle);
        }

        if (!routeRequested && !_hasControl && handoffDangers.Count == 0 && !HasMechanicGoal(snapshot, now))
            return true;

        // Set the live flag before synchronously asking FSD to revoke its outputs.
        // A denied handoff never authorizes a GreenMove write; retry on the next update.
        MechanicMovementActive = true;
        return RequestExclusiveMovement?.Invoke(true) ?? true;
    }

    private static bool HasMechanicGoal(SafeZoneSnapshot snapshot, DateTime now) =>
        snapshot.GoalAreas.Any(goal => goal.GoalWindows.Any(window => window.Priority > 0 && window.IsActiveAt(now)));

    private void CompleteMovementHandoff(SafeZoneSnapshot snapshot, DateTime now)
    {
        if (!_hasControl && handoffDangers.Count == 0 && !HasMechanicGoal(snapshot, now))
            ReleaseMovementHandoff();
    }

    private void ReleaseMovementHandoff()
    {
        handoffDangers.Clear();
        if (!MechanicMovementActive) return;
        MechanicMovementActive = false;
        RequestExclusiveMovement?.Invoke(false);
    }

    private void Dispatch(SafeZoneSolution? solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now)
    {
        if (_externalMovementEnabled() || _externalHasControl())
        {
            _phase = DispatchPhase.YieldingToHost;
            ReleaseControl();
            ReleaseMovementHandoff();
            return;
        }
        if (!PrepareMovementHandoff(snapshot, player, now, solution is { IsSuccess: true, ExecutablePath.Count: >= 2 }))
        {
            ReleaseControl();
            return;
        }
        try { DispatchCore(solution, player, snapshot, now); }
        finally { CompleteMovementHandoff(snapshot, now); }
    }
}
