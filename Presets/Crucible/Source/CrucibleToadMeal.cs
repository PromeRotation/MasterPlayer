namespace MasterPlayer.Mechanics.Crucible;

// Beast Road III B36: after the drake dies, provoke the toad and lead it past living butterflies.
// This supplies a safe-planner goal only. Disappearance never grants meal/kill credit.
sealed class CrucibleToadMeal(BossModule module) : BossComponent(module)
{
    private bool _drakeDead;
    private ulong _butterfly;

    private static bool Drake(Actor a) => a.OID == 19696 && a.NameID == 14651;
    private static bool Living(Actor a) => !a.IsDeadOrDestroyed && a.HPMP.CurHP > 0;

    public override void OnActorDeath(Actor actor)
    {
        if (Drake(actor) && (actor.IsDead || actor.HPMP.CurHP == 0)) _drakeDead = true;
    }

    public override void Update()
    {
        var drake = WorldState.Actors.FirstOrDefault(Drake);
        if (drake != null)
            _drakeDead = drake.IsDead || drake.HPMP.CurHP == 0;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!_drakeDead || hints.GoalPoints.Any(p => p.Priority >= 100)) return;
        var frog = WorldState.Actors.FirstOrDefault(a => a.OID == 19700 && a.NameID == 14655 && Living(a));
        if (frog == null || frog.TargetID != actor.InstanceID) return;
        var butterflies = WorldState.Actors.Where(a => a.OID == 19701 && a.NameID == 14656 && Living(a)).ToArray();
        var next = butterflies.FirstOrDefault(a => a.InstanceID == _butterfly)
            ?? butterflies.OrderBy(a => (a.Position - actor.Position).LengthSq()).ThenBy(a => a.InstanceID).FirstOrDefault();
        if (next == null) { _butterfly = 0; return; }
        _butterfly = next.InstanceID;
        // Butterflies spawn at (502/538, +/-13), near the 40x30 arena corners.
        // Stand just beyond the chosen butterfly, strictly inside the floor; never request a point 12y outside it.
        var offset = next.Position - Module.Center;
        if (offset.LengthSq() < .01f) return;
        var raw = next.Position + offset.Normalized();
        var goal = new WPos(Math.Clamp(raw.X, 501f, 539f), Math.Clamp(raw.Z, -14f, 14f));
        if (Module.Arena.InBounds(goal)) hints.GoalPoints.Add((goal, .3f, 110));
    }
}
