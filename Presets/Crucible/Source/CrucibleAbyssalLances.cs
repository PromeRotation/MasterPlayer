namespace MasterPlayer.Mechanics.Crucible;

// B02 Abyssal Lance (46876): four lance actors (OID 19337) appear and, ~3s later, cast 40y lines aimed
// at the player's position. In 17 ARRs of 2026-10-30 the lines hit 5.6–6.4s after the lances appeared
// and 3.0s after the cast packet, except at 13:40 where the packets came ~1.1s late (hit 1.9s after the
// cast, 5.8s after the lances appeared) and the planner waited past the real hit. Plan against the
// earlier of the cast finish and the appearance time plus the shortest observed gap.
sealed class CrucibleAbyssalLances(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    public const uint Lance = 46876;
    private const uint LanceOID = 19337;
    private const double AppearToHit = 5.6;
    private readonly Dictionary<ulong, DateTime> _appeared = [];

    protected override AOEConfig? ConfigFor(uint actionID) => actionID == Lance ? new(CrucibleSpells.Table[Lance].Shape, true) : null;

    public override void Update()
    {
        base.Update();
        var now = WorldState.CurrentTime;
        foreach (var actor in WorldState.Actors)
            if (actor.OID == LanceOID)
                _appeared.TryAdd(actor.InstanceID, now);
        foreach (var id in _appeared.Keys.Where(id => WorldState.Actors.Find(id) == null).ToArray())
            _appeared.Remove(id);
    }

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
        {
            if (!aoe.Risky)
                continue;
            var activation = aoe.Activation;
            if (_appeared.TryGetValue(aoe.ActorID, out var appeared) && appeared.AddSeconds(AppearToHit) < activation)
                activation = appeared.AddSeconds(AppearToHit);
            hints.AddForbiddenZone(aoe.ShapeDistance ?? aoe.Shape.Distance(aoe.Origin, aoe.Rotation), activation);
        }
    }
}
