using System;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

namespace MasterPlayer.Spatial.SafeZoneReborn;

internal sealed partial class SafeZoneRebornMovementSystem
{
    // In addition to DangerStartSafetyTime, leave room for the next frame and movement startup.
    private const double DepartureReserveSeconds = 0.15;

    private bool ShouldWaitForEscape(SafeZoneSolution solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now)
    {
        if (_setting is null || _setting.AttackUptimeExpiresAt <= now || _hasControl)
            return false;
        return CanDelayEscape(solution, player, snapshot, now, DepartureReserveSeconds);
    }

    private bool CanDelayEscape(SafeZoneSolution solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now, double delay)
    {
        if (!solution.IsSuccess || solution.ExecutablePath.Count < 2 || solution.UsesTemporalPlanning
            || solution.ReducedDangerStartSafetyTime.HasValue || player.CurrentSpeed <= 0.1f
            || !double.IsFinite(delay) || delay <= 0 || IsPointUnsafeNow(snapshot, player.Position, now))
            return false;

        // Negative-priority goals are optional attack preferences. Mechanic destinations
        // and opaque constraints may encode knockbacks, binds, or other preparation deadlines.
        foreach (var goal in snapshot.GoalAreas)
            foreach (var window in goal.GoalWindows)
                if (window.Priority >= 0 && window.IsActiveAt(now)) return false;

        var mustEscape = false;
        DateTime? firstActivation = null;
        foreach (var danger in snapshot.DangerAreas)
        {
            foreach (var window in danger.DangerWindows)
            {
                if (window.DangerFrom == DateTime.MaxValue ||
                    window.DangerUntil is { } end && (end - now).TotalSeconds < -_options.DangerEndSafetyTime)
                    continue;
                if (danger.Geometry is not (CircleGeometry or DonutGeometry or FanGeometry or RectangleGeometry))
                    return false;
                if (window.DangerFrom > now)
                {
                    // Staggered waves can reveal further lanes while waiting. A route
                    // through today's empty lane is not evidence that it will stay open.
                    if (firstActivation is { } first && Math.Abs((window.DangerFrom - first).TotalSeconds) > 0.1)
                        return false;
                    firstActivation ??= window.DangerFrom;
                }
                if (window.DangerFrom > now && danger.Geometry.Contains(player.Position))
                    mustEscape = true;
            }
        }
        if (!mustEscape) return false;

        var departure = now.AddSeconds(delay);
        // Recheck the actual remaining polyline at the proposed departure time. This
        // includes intermediate hazards and their end margins, not just the final point
        // or the first circle's exit. No path search runs on the framework thread.
        return IsTemporalSegmentSafe(snapshot, player.Position, player.Position, now, departure)
            && IsTemporalPhaseSafe(solution, player, snapshot, departure);
    }
}
