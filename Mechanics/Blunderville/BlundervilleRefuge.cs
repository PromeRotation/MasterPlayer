using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// ARR 2026-10-12 15:10:49 Final: when the timed search found no route the
// runtime released control and the player stood inside the next forecast
// circle or wave. Step aside to the closest reachable spot that stays clear.
internal static class BlundervilleRefuge
{
    internal const double ThreatHorizon = 1.5, HoldAfterArrival = 2, Speed = 6;
    // 7m: a player at the edge of an R5 circle needs >5.6m to clear it.
    internal const float Clearance = .6f, MaximumDistance = 7;

    public static bool Threatened(IReadOnlyList<BlundervilleHazard> hazards, Vector3 position, DateTime from, DateTime until, float clearance = Clearance)
        => hazards.Any(h => h.Until >= from && h.From <= until && h.Geometry.Contains(position, clearance));

    public static Vector3? Find(IGeometryShape floor, IReadOnlyList<BlundervilleHazard> hazards, Vector3 player, DateTime now)
    {
        if (!Threatened(hazards, player, now, now.AddSeconds(ThreatHorizon))) return null;
        Vector3? best = null; var bestScore = float.PositiveInfinity;
        for (var radius = .75f; radius <= MaximumDistance + 1e-3f; radius += .75f)
            for (var step = 0; step < 16; ++step)
            {
                var angle = step * MathF.Tau / 16;
                var candidate = player + new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * radius;
                if (!OnFloor(floor, candidate) || !ArenaBoundaryTraversal.ContainsTube(floor, player, candidate, .5f)) continue;
                var arrival = now.AddSeconds(radius / Speed + .25);
                if (Threatened(hazards, candidate, arrival, arrival.AddSeconds(HoldAfterArrival))) continue;
                // The step itself must not cross an active window on the way.
                var crossed = false;
                for (var sample = 1; sample < 4 && !crossed; ++sample)
                {
                    var point = Vector3.Lerp(player, candidate, sample / 4f);
                    var at = now.AddSeconds(radius * sample / 4 / Speed);
                    crossed = Threatened(hazards, point, at, at.AddSeconds(.35), .4f);
                }
                if (crossed) continue;
                // Prefer the shortest step, then progress down the course (-Z).
                var score = radius + (candidate.Z - player.Z) * .15f;
                if (score < bestScore) { best = candidate with { Y = player.Y }; bestScore = score; }
            }
        return best;
    }

    // Applied only after the timed solver failed (released or no route).
    public static BlundervilleRaceDecision Apply(BlundervilleRaceDecision decision, BlundervilleRaceState state,
        IReadOnlyList<BlundervilleHazard> hazards, Vector3 player, DateTime now, bool solverFailed)
    {
        if (state.Course != BlundervilleCourse.Final || !solverFailed || decision.Hold || !state.CanMove(now)) return decision;
        return Find(BlundervilleRoutes.Boundary(state.Course), hazards, player, now) is { } refuge
            ? decision with { Target = refuge, Jump = false, Hint = "决赛：先避开即将判定的机关" }
            : decision;
    }

    private static bool OnFloor(IGeometryShape floor, Vector3 p)
        => floor.Contains(p) && floor.Contains(p + new Vector3(.5f, 0, 0)) && floor.Contains(p - new Vector3(.5f, 0, 0))
            && floor.Contains(p + new Vector3(0, 0, .5f)) && floor.Contains(p - new Vector3(0, 0, .5f));
}
