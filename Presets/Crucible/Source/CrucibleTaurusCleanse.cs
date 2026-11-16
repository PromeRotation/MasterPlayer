using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Crucible;

// f1x2/level/planmap.lgb LVD_STAGE_SG_ER:
// south pad instance12401563 / trigger12401583 at (520,7.5), half extents2.5.
// run1813 index05 is the ONLY replay-validated charge mapping. Do not infer
// mappings for the three other visually identical pads.
sealed class CrucibleTaurusCleanse(BossModule module) : BossComponent(module)
{
    private ulong _target;
    private DateTime _expires;
    // run2093: a player status (0x1469) stopped 5421 for all six rays, and waiting out the 27s timeout pinned the
    // player on the pad through 48150/48165. Doom has always landed about 1.5s after the 48143 hit.
    private const float DoomGraceSeconds = 3f;
    private DateTime _doomDeadline;
    private bool _seenDoom;
    private static readonly WPos Pad = new(520, 7.5f);
    private readonly PadInteriorConstraint _padInterior = new(module.Arena.Bounds.Geometry(module.Center, 0).GetBounds());
    // Registered as trigger half2.5 + the solver's own Hard0.5, so the solver's
    // legal set is exactly the real 5x5 trigger. With half2.5 registered, a player
    // already standing on the trigger but within 0.5 of its edge when 48143 starts
    // (run1837-b2 doom3 at 522.31,5.25) is raster-blocked yet not geometrically in
    // danger: NoTemporalPath for 6s, then the R18 orb hits. The 1.5-deep corner /
    // R0.5 centre goal points below still pull the player deep onto the pad.
    private const float PadHalf = 2.5f, SolverHardMargin = 0.5f;
    private sealed class PadInteriorConstraint(GeometryBounds bounds) : ShapeDistance
    {
        public override float Distance(in WPos p) => PadHalf + SolverHardMargin - Math.Max(Math.Abs(p.X-Pad.X),Math.Abs(p.Z-Pad.Z));
        public override IGeometryShape Geometry(float y) => new RectangleComplementGeometry(Pad.ToVec3(y),PadHalf + SolverHardMargin,PadHalf + SolverHardMargin,bounds);
        public override string ContentKey() => $"B08PadTrigger|{bounds.MinX:R}|{bounds.MinZ:R}|{bounds.MaxX:R}|{bounds.MaxZ:R}";
    }

    private void Clear() { _target = 0; _expires = default; _doomDeadline = default; _seenDoom = false; }
    private bool Active(Actor actor)
    {
        if (Module.PrimaryActor.IsDeadOrDestroyed || actor.IsDeadOrDestroyed || WorldState.CurrentTime >= _expires
            || !_seenDoom && WorldState.CurrentTime >= _doomDeadline)
            Clear();
        return _target == actor.InstanceID && _target != 0;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster != Module.PrimaryActor || spell.Action.ID != 48143 || spell.EventHappened)
            return;
        var player = WorldState.Player;
        if (player == null || player.IsDeadOrDestroyed) return;
        _target = player.InstanceID;
        // Start on the native 4.7s warning, not on late text/stage00200010.
        // Timeout is only stale-state protection; never a successful cleanse.
        _expires = Module.CastFinishAt(spell).AddSeconds(27);
        _doomDeadline = Module.CastFinishAt(spell).AddSeconds(DoomGraceSeconds);
        _seenDoom = false;
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == 48143 && spell.Interrupted) Clear();
    }
    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (status.ID != 5421 || actor != WorldState.Player) return;
        _target = actor.InstanceID;
        _expires = status.ExpireAt.AddSeconds(0.5);
        _seenDoom = true;
    }
    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (_seenDoom && status.ID == 5421 && actor.InstanceID == _target) Clear();
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor || actor.InstanceID == _target) Clear();
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Active(actor)) hints.Add("立即进入净化地板并停留，等死亡宣告实际消失再离开！", true);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!Active(actor)) return;
        // Pre-charge whenever the physical pad overlaps the boss's real melee
        // range. If it cannot, keep normal safe melee during the warning only;
        // actual Doom still immediately starts the unchanged pad constraint.
        var bossPosition = Module.PrimaryActor.Position;
        var closestPad = new WPos(Math.Clamp(bossPosition.X, Pad.X - PadHalf, Pad.X + PadHalf),
            Math.Clamp(bossPosition.Z, Pad.Z - PadHalf, Pad.Z + PadHalf));
        if (!_seenDoom && (closestPad - bossPosition).Length()
            > Module.PrimaryActor.HitboxRadius + actor.HitboxRadius + 3f)
        {
            // Only the live orbs' existing pulse forecast may require early entry.
            // Keep the original pre-charge if any pulse reaches the real pad
            // before this cleanse deadline; no future casts or orb spawns are read.
            var orbs = Module.FindComponent<CrucibleTaurusExplosions>();
            if (orbs == null || !orbs.Forecast(_expires).Any(pulse =>
                (new WPos(Math.Clamp(pulse.Origin.X, Pad.X - PadHalf, Pad.X + PadHalf),
                    Math.Clamp(pulse.Origin.Z, Pad.Z - PadHalf, Pad.Z + PadHalf)) - pulse.Origin).Length()
                    <= pulse.Radius + 0.85f)) return;
        }
        // Hard-limit to the real 5x5 trigger (see PadInteriorConstraint); depth
        // inside it comes from the goal points, not from a raster margin.
        // Keep charge from being interrupted by normal melee follow. Other
        // hazards remain registered; replay must reject conflicting windows.
        hints.AddForbiddenConstraint(_padInterior, WorldState.CurrentTime);
        // The pad centre is 3.1y outside the B08 melee hitboxes while 48143
        // channels (six sealed attempts: 1967/1974/1978). Stay 1.25y inside
        // the real trigger on the boss-facing side so cleansing need not stop
        // melee GCDs. This is only a preferred goal: the original pad constraint,
        // danger geometry, solver margins and large-orb corner override remain.
        const float InnerOffset = 1.25f;
        var boss = Module.PrimaryActor.Position;
        var goal = new WPos(Math.Clamp(boss.X, Pad.X - InnerOffset, Pad.X + InnerOffset),
            Math.Clamp(boss.Z, Pad.Z - InnerOffset, Pad.Z + InnerOffset));
        {
            // Only already observed live orbs; R12 means the existing native four-pulse sequence
            // will next grow to R18. This is a positioning preference, never a new hard hazard.
            var large=Module.FindComponent<CrucibleTaurusExplosions>()?.ActiveAOEs(slot,actor).ToArray()
                .Where(a=>a.Shape is AOEShapeCircle c && c.Radius>=12).ToArray() ?? [];
            if(large.Length>0)
            {
                // Keep the proved inner corner one full metre from the true trigger edge.
                // The registered true pad and all original solver margins still decide legality.
                var corners=new[]{new WPos(Pad.X-1.5f,Pad.Z-1.5f),new WPos(Pad.X-1.5f,Pad.Z+1.5f),
                    new WPos(Pad.X+1.5f,Pad.Z-1.5f),new WPos(Pad.X+1.5f,Pad.Z+1.5f)};
                goal=corners.OrderByDescending(p=>large.Min(a=>(p-a.Origin).Length()-((AOEShapeCircle)a.Shape).Radius)).First();
            }
        }
        hints.GoalPoints.Add((goal, 0.5f, 100));
    }
}
