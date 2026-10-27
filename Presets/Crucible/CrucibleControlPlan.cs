namespace MasterPlayer.Mechanics.Crucible;

internal sealed record CrucibleControlPlan(ulong Target, ulong ClearTarget, ulong[] ForbiddenTargets,
    Angle? Heading, bool HoldActions, bool BlockCasts, bool CancelCast)
{
    public static CrucibleControlPlan Build(AIHints hints, Actor player, DateTime now)
    {
        var target = hints.ForcedTarget is { IsDeadOrDestroyed: false, IsTargetable: true, Type: ActorType.Enemy } forced
            && forced.InstanceID != player.InstanceID && forced.HPMP.CurHP > 0 ? forced.InstanceID : 0;
        // Existing priority-only components remain advisory. Explicit target mechanics own selection.
        var forbidden = hints.OwnsTargetSelection || target != 0 || hints.ClearTargetID != 0
            ? hints.PotentialTargets.Where(t => t.Priority == AIHints.Enemy.PriorityForbidden).Select(t => t.Actor.InstanceID).ToArray()
            : [];
        Angle? heading = hints.DesiredFacingExpire > now ? hints.DesiredFacing : null;
        var gazes = hints.ForbiddenDirections.Where(g => g.Activation <= now.AddSeconds(1.2) && g.Activation.AddSeconds(1) >= now).ToArray();
        if (gazes.Length > 0)
        {
            float Clearance(Angle angle) => gazes.Min(g => Math.Abs((angle - g.Center).Normalized().Rad) - g.HalfWidth.Rad);
            // Prefer maximum angular clearance, with the current heading breaking ties.
            var best = player.Rotation;
            var bestScore = Clearance(best);
            for (var i = 0; i < 360; ++i)
            {
                var candidate = i.Degrees();
                var score = Clearance(candidate);
                if (score > bestScore + 0.0001f) { best = candidate; bestScore = score; }
            }
            if (bestScore > 0.01f) heading = best;
        }
        var hold = heading != null || target == 0 && forbidden.Length > 0;
        return new(target, hints.ClearTargetID, forbidden, heading, hold,
            hints.MaxCastTime <= 0 || hold, hints.ForceCancelCast || heading != null || hints.MaxCastTime <= 0 && player.CastInfo != null);
    }
}
