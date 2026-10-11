using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

namespace MasterPlayer.Spatial.SafeZoneReborn;

internal sealed partial class SafeZoneRebornMovementSystem
{
    private SafeZoneSolution? _temporalContinuation;
    private IReadOnlyList<GoalAreaSnapshot> _temporalGoals = [];

    private void RememberTemporalContinuation(SafeZoneSolution solution, SafeZoneSnapshot snapshot)
    {
        _temporalContinuation = _setting.RuntimePreferStraightPaths && solution.ContinuationPath.Count > 1
            && snapshot.GoalAreas.Any(g => g.Geometry.Contains(solution.ContinuationPath[^1].Position)
                && g.GoalWindows.Any(w => w.IsActiveAt(snapshot.CreatedAt))) ? solution : null;
        _temporalGoals = _temporalContinuation == null ? [] : snapshot.GoalAreas;
    }

    // The worker already searched beyond the wait. Resume that suffix on the
    // main thread after checking current geometry, instead of spending another
    // worker round trip inside a short-lived safe pocket.
    private bool TryContinueTemporalPhase(PlayerState player, SafeZoneSnapshot snapshot, DateTime now)
    {
        if (_temporalContinuation is not { } previous) return false;
        if (!_hasControl || !_setting.RuntimePreferStraightPaths || now < previous.SolveTime
            || now - previous.SolveTime > TimeSpan.FromSeconds(_options.PlanningHorizon)
            || !SameTemporalGoals(snapshot.GoalAreas, now))
        { _temporalContinuation = null; return false; }

        var end = previous.ExecutablePath[^1];
        if (GeometryHelper.DistanceXZ(player.Position, end.Position) > MinArrivalTolerance)
        {
            if (_greenMove?.IsRunning == true && ReferenceEquals(_dispatchedSolution, previous)
                && IsTemporalPhaseSafe(previous, player, snapshot, now))
            { _phase = DispatchPhase.Moving; return previous.SolvedAt != previous.SolveTime; }
            _temporalContinuation = null;
            return false;
        }
        if (!end.IsWaitPoint || previous.ContinuationPath.Count < 2)
        { _temporalContinuation = null; return false; }
        if (now < end.PlannedDepartureTime)
        {
            if (!IsTemporalSegmentSafe(snapshot, player.Position, player.Position, now, end.PlannedDepartureTime))
            { _temporalContinuation = null; return false; }
            return false;
        }

        var remaining = previous.ContinuationPath;
        var phase = new List<TimedWaypoint> { TimedWaypoint.Pass(player.Position, now) };
        var next = 1;
        var arrival = now.AddSeconds(.1);
        for (; next < remaining.Count; ++next)
        {
            var point = remaining[next];
            arrival = arrival.AddSeconds(GeometryHelper.DistanceXZ(phase[^1].Position, point.Position)
                / MathF.Max(.1f, player.CurrentSpeed));
            var wait = point.IsWaitPoint && point.PlannedDepartureTime > arrival;
            phase.Add(point with { IsWaitPoint = wait });
            if (wait) break;
        }
        // Remove only collinear interior grid points. Every retained segment is
        // rechecked against current obstacles, floor and danger times below.
        for (var i = phase.Count - 2; i > 0; --i)
        {
            var a = phase[i].Position - phase[i - 1].Position;
            var b = phase[i + 1].Position - phase[i].Position;
            a.Y = b.Y = 0;
            if (!phase[i].IsWaitPoint && a.LengthSquared() > .001f && b.LengthSquared() > .001f
                && Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b)) > .99999f) phase.RemoveAt(i);
        }
        SafeZoneSolution Build(IReadOnlyList<TimedWaypoint> path) => new(SolutionStatus.Success, snapshot.Version, previous.SolveTime, now,
            path[^1].Position, path)
        {
            UsesTemporalPlanning = true, MaxArrivalTolerance = previous.MaxArrivalTolerance,
            ContinuationPath = next < remaining.Count ? remaining.Skip(next).ToArray() : [],
        };
        var candidate = Build(phase);
        for (var anchor = 0; anchor + 2 < phase.Count; ++anchor)
            for (var endIndex = phase.Count - 1; endIndex > anchor + 1; --endIndex)
            {
                var shortcut = phase.Take(anchor + 1).Concat(phase.Skip(endIndex)).ToList();
                var trial = Build(shortcut);
                if (!IsTemporalPhaseSafe(trial, player, snapshot, now)
                    || !IsTemporalPhaseSafe(trial, player, snapshot, now, startDelay: .1)) continue;
                phase = shortcut; candidate = trial; break;
            }
        if (!IsTemporalPhaseSafe(candidate, player, snapshot, now)
            || !IsTemporalPhaseSafe(candidate, player, snapshot, now, startDelay: .1))
        { _temporalContinuation = null; return false; }
        _greenMove!.MoveTo(BuildGreenMovePoints(candidate, snapshot, now));
        _dispatchedSolution = _latestSolution = _temporalContinuation = candidate;
        _dispatchCount++; _lastDispatchUtc = now;
        _phase = DispatchPhase.Moving;
        return true;
    }

    internal void ClearTemporalContinuation() { _temporalContinuation = null; _temporalGoals = []; }

    private bool SameTemporalGoals(IReadOnlyList<GoalAreaSnapshot> goals, DateTime now)
    {
        if (goals.Count != _temporalGoals.Count) return false;
        for (var i = 0; i < goals.Count; ++i)
        {
            var a = goals[i]; var b = _temporalGoals[i];
            if (a.Handle != b.Handle || !a.GoalWindows.SequenceEqual(b.GoalWindows)
                || !a.GoalWindows.Any(w => w.IsActiveAt(now))) return false;
            if (ReferenceEquals(a.Geometry, b.Geometry)) continue;
            if (a.Geometry is not CircleGeometry ac || b.Geometry is not CircleGeometry bc
                || ac.Position != bc.Position || ac.Radius != bc.Radius) return false;
        }
        return true;
    }
}
