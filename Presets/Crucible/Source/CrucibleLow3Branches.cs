using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// Knockback 114: 35y forward, not away from the helper or a 60x60 damage zone.
sealed class CrucibleYmirTsunami(BossModule module)
    : Low3CrucibleKnockback(module, 48481u, 35, Kind.DirForward, new AOEShapeRect(60, 30))
{
    private Knockback[] _pending = [];
    private DateTime _expires;
    private bool _effectSeen;
    private WPos _lastPosition;
    private DateTime _lastPositionAt;

    private void Clear() { _pending = []; _expires = default; _effectSeen = false; }
    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        if (actor.IsDeadOrDestroyed || _expires <= WorldState.CurrentTime
            || _pending.Length > 0 && IsImmune(slot, _pending[0].Activation)) Clear();
        // Ordinary walking inside the launch strip must not consume this lease.
        // Native tsunami displacement is about 40m/s; walking/sprint are below 12.
        if (_effectSeen && _pending.Length > 0 && WorldState.CurrentTime > _lastPositionAt)
        {
            var elapsed = (WorldState.CurrentTime - _lastPositionAt).TotalSeconds;
            var forward = (actor.Position - _lastPosition).Dot(_pending[0].Direction.ToDirection());
            if (forward > 0.3f && forward / elapsed > 12) Clear();
            _lastPosition = actor.Position;
            _lastPositionAt = WorldState.CurrentTime;
        }
        return _pending;
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction || spell.EventHappened || spell.FinishAt <= WorldState.CurrentTime) return;
        _pending = [new(spell.LocXZ,35,Module.CastFinishAt(spell),new AOEShapeRect(60,30),spell.Rotation,Kind.DirForward,actorID:caster.InstanceID)];
        _expires = Module.CastFinishAt(spell).AddSeconds(3);
        _effectSeen = false;
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && spell.Interrupted && _pending.Length > 0 && _pending[0].ActorID == caster.InstanceID) Clear();
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != WatchedAction || _pending.Length == 0 || _pending[0].ActorID != caster.InstanceID) return;
        _effectSeen = true;
        _lastPosition = WorldState.Player?.Position ?? default;
        _lastPositionAt = WorldState.CurrentTime;
        _expires = WorldState.FutureTime(2);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == WorldState.Player || actor == Module.PrimaryActor || _pending.Length > 0 && actor.InstanceID == _pending[0].ActorID) Clear();
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InRect(Module.Center, 19.5f, 19.5f);
    // run2007 b2/b3: the opaque landing constraint sent the player to the back fence at
    // the 7.7s cast start, ~4s ahead of need. Launch rows need forward <= 19.5-35 from
    // the centre; withhold the constraint until the walk there (5y/s, +1y) needs the time left.
    private const float LaunchWalkSpeed = 5f, LaunchSlack = 1f;
    private const double LaunchLead = 0.5, LaunchReserve = 1.5;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var kb in ActiveKnockbacks(slot, actor))
        {
            var forward = (actor.Position - Module.Center).Dot(kb.Direction.ToDirection());
            var walk = MathF.Max(0, forward - (19.5f - 35) + LaunchSlack) / LaunchWalkSpeed;
            // Already on a launch row: bind at once so nothing walks back into the centre.
            if (!_effectSeen && walk > 0 && kb.Activation > WorldState.FutureTime(walk + LaunchLead + LaunchReserve))
                continue;
            hints.AddForbiddenConstraint(new LandingZone([kb], Module.Center, 19.5f, 19.5f, []), kb.Activation.AddSeconds(-LaunchLead));
        }
    }
}

// The charge packet's target is an ally, not the destination. Wait for the actual
// displacement, then show the rear half before the 0.7s helper cast arrives.
sealed class CrucibleZuRear(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCone Shape = new(15, 90f.Degrees());
    private WPos _start;
    private Angle _direction;
    private DateTime _landingDeadline;
    private DateTime _rearChargeUntil;
    private AOEInstance? _prediction;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID == 48499 ? new(Shape, true) : null;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48494 && !spell.EventHappened)
            _rearChargeUntil = Module.CastFinishAt(spell, 3);
        base.OnCastStarted(caster, spell);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48494 && spell.Interrupted)
            _rearChargeUntil = default;
        base.OnCastFinished(caster, spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48495 && _rearChargeUntil > WorldState.CurrentTime)
        {
            _rearChargeUntil = default; // same charge can also follow the front variant
            _start = caster.Position;
            _direction = spell.Rotation + 180f.Degrees();
            _landingDeadline = WorldState.FutureTime(3);
        }
        else if (spell.Action.ID == 48499)
        {
            _prediction = null;
            _landingDeadline = default;
            _rearChargeUntil = default;
        }
        base.OnEventCast(caster, spell);
    }
    public override void Update()
    {
        base.Update();
        if (_landingDeadline > WorldState.CurrentTime && _prediction == null)
        {
            var boss = Module.Enemies(19606).FirstOrDefault(a => !a.IsDeadOrDestroyed);
            if (boss != null && !boss.Position.AlmostEqual(_start, 3))
                _prediction = new(Shape, boss.Position, _direction, _landingDeadline);
        }
        if (_prediction is { } p && p.Activation.AddSeconds(1) < WorldState.CurrentTime)
            _prediction = null;
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var actual = base.ActiveAOEs(slot, actor);
        return actual.Length > 0 ? actual : _prediction is { } p ? new[] { p } : [];
    }
    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == 19606)
        {
            _prediction = null;
            _landingDeadline = default;
            _rearChargeUntil = default;
        }
        base.OnActorDeath(actor);
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

// Eyes travel at 2y/s toward eight R21 perimeter positions. One replayed eye
// detours slightly mid-path: snap the projected endpoint to those observed
// 45-degree positions, then reconcile the actual cast. Movement provides ~19s
// of warning for a 0.2s cast. Stationary eyes are not armed.
// The donut's inner radius is absent from Action/Omen: show an interior destination
// close to that eye, rather than draw a guessed damage ring.
sealed class CrucibleCatoblepasEyes(BossModule module) : Components.GenericAOEs(module)
{
    private sealed class MovingEye(Actor actor, uint oid = 0)
    {
        public readonly Actor Actor = actor;
        public readonly uint OID = oid == 0 ? actor.OID : oid;
        public readonly WPos Start = actor.Position;
        public WPos Last = actor.Position;
        public WPos End;
        public DateTime Activation;
        public bool Resolved;
        public bool Confirmed;
        public ulong CastActorID;
        public bool HasGaze;
        public bool GazeResolved;
    }
    private readonly Dictionary<ulong, MovingEye> _eyes = [];
    private static readonly AOEShapeCircle Circle = new(25);
    private const float SpotRadius = 0.5f;
    private const float InwardOffset = 2.5f;
    private static readonly AOEShapeCircle Spot = new(SpotRadius);

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID is 19612 or 19613)
            _eyes.TryAdd(actor.InstanceID, new(actor));
    }

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        // ARR 2026-09-11: tether 195 marks exactly one eye from each pair.
        // The visual link can disappear before the gaze, so latch until impact.
        if (tether.ID == 195 && source.OID is 19612 or 19613 && WorldState.Actors.Find(tether.Target)?.OID == 19611)
        {
            OnActorCreated(source);
            _eyes[source.InstanceID].HasGaze = true;
        }
    }
    public override void Update()
    {
        foreach (var eye in _eyes.Values)
        {
            var current = eye.Actor.Position;
            if (eye.Resolved || eye.Confirmed || eye.Actor.IsDeadOrDestroyed || Math.Abs((eye.Start - Module.Center).Length() - 21) >= 1
                || current.AlmostEqual(eye.Last, 0.05f) || current.AlmostEqual(eye.Start, 1.25f))
                continue;
            eye.Last = current;
            var direction = (current - eye.Start).Normalized();
            var offset = current - Module.Center;
            var projection = offset.Dot(direction);
            var discriminant = projection * projection + 21 * 21 - offset.LengthSq();
            if (discriminant < 0)
                continue;
            var remaining = -projection + MathF.Sqrt(discriminant);
            if (remaining < 0)
                continue;
            var projected = current + remaining * direction;
            var angle = Angle.FromDirection(projected - Module.Center);
            var snapped = (MathF.Round(angle.Deg / 45) * 45).Degrees();
            eye.End = Module.Center + 21 * snapped.ToDirection();
            eye.Activation = WorldState.FutureTime((eye.End - current).Length() / 2 + 0.5);
        }
    }
    private MovingEye[] PendingEyes() => _eyes.Values.Where(e => !e.Resolved && !e.Actor.IsDeadOrDestroyed
        && e.Activation != default && e.Activation.AddSeconds(1) >= WorldState.CurrentTime).OrderBy(e => e.Activation).ToArray();
    private WPos Destination(MovingEye eye) => eye.End + InwardOffset * (Module.Center - eye.End).Normalized();

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var eyes = PendingEyes();
        if (eyes.Length == 0)
            return [];
        var deadline = eyes[0].Activation.AddSeconds(1);
        return eyes.Where(e => e.Activation <= deadline).Select(e => e.OID == 19612
            ? new AOEInstance(Circle, e.End, activation: e.Activation)
            : new AOEInstance(Spot, Destination(e), activation: e.Activation, color: Colors.SafeFromAOE, risky: false)).ToArray();
    }
    // The pocket binds from 1s before the burst. Walk at 5y/s (as the gargoyle tether
    // stretch) and keep 1.5s for dispatch, solver delay and the crossing's detours.
    private const float PocketWalkSpeed = 5f;
    private const double PocketLead = 1, PocketReserve = 1.5;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            if (aoe.Risky)
                CrucibleScorpionAI.Avoid(hints, aoe);
            else
            {
                // run2005: the pocket was selected ~19s before the 0.2s burst and the
                // player stood there without a GCD. Keep hitting until the walk needs
                // what is left; an opaque constraint/positive goal disables the
                // uptime escape delay, so withhold both until then.
                var inPocket = actor.Position.InCircle(aoe.Origin, SpotRadius);
                var walk = (actor.Position - aoe.Origin).Length() / PocketWalkSpeed;
                if (!inPocket && aoe.Activation > WorldState.FutureTime(walk + PocketLead + PocketReserve))
                    continue;
                hints.AddForbiddenConstraint(new SDInvertedCircle(aoe.Origin, SpotRadius), aoe.Activation.AddSeconds(-PocketLead), aoe.Activation.AddSeconds(0.6));
                hints.GoalPoints.Add((MeleeWaypoint(actor, aoe) ?? aoe.Origin, SpotRadius, 100));
                if (!inPocket)
                    hints.MaxCastTime = 0;
            }
    }

    // Waves alternate sides 8s apart, so the next pocket is due as soon as the last
    // one resolves (run2005: 31y crossing passing 4–5y off the hitbox, 15s with no GCD).
    // While the time allows, cross via the boss's melee ring at walking speed 6y/s.
    private const float MeleeReach = 2.5f, CrossSpeed = 6f, MaxDetour = 6f;
    private const double CrossReserve = 0.5;
    private WPos? MeleeWaypoint(Actor actor, AOEInstance aoe)
    {
        var boss = Module.PrimaryActor;
        if (boss.IsDeadOrDestroyed)
            return null;
        var reach = boss.HitboxRadius + MeleeReach;
        if (actor.Position.InCircle(boss.Position, reach + .5f))
            return null;
        var direct = (aoe.Origin - actor.Position).Length();
        if (direct < 1)
            return null;
        // Nearest point on the melee ring to the straight crossing.
        var along = (aoe.Origin - actor.Position) / direct;
        var t = Math.Clamp((boss.Position - actor.Position).Dot(along), 0, direct);
        var closest = actor.Position + along * t;
        var offset = closest - boss.Position;
        var via = boss.Position + (offset.LengthSq() > .01f ? offset.Normalized() : along.OrthoL()) * reach;
        var detour = (via - actor.Position).Length() + (aoe.Origin - via).Length();
        if (detour - direct > MaxDetour || !Module.Arena.InBounds(via)
            || aoe.Activation <= WorldState.FutureTime(detour / CrossSpeed + PocketLead + CrossReserve)
            || ActiveAOEs(0, actor).ToArray().Any(o => o.Risky && o.Shape.Check(via, o.Origin, o.Rotation)
                && o.Activation <= WorldState.FutureTime((via - actor.Position).Length() / CrossSpeed + 1)))
            return null;
        return via;
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        foreach (var aoe in ActiveAOEs(slot, actor))
            if (!aoe.Risky)
                hints.Add("环形爆炸：进入绿色安全点，避免爆炸附带石化", !actor.Position.InCircle(aoe.Origin, SpotRadius));
    }

    public override void DrawArenaForeground(int slot, Actor actor)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            if (!aoe.Risky)
            {
                Arena.ZoneCircleOutline(aoe.Origin, SpotRadius, Colors.Safe);
                Arena.AddLine(actor.Position, aoe.Origin, Colors.Safe);
            }
    }

    public Components.GenericGaze.Eye[] GazeEyes()
    {
        var eyes = PendingEyes();
        if (eyes.Length == 0)
            return [];
        return eyes.Where(e => e.HasGaze && !e.GazeResolved && e.Activation <= eyes[0].Activation.AddSeconds(1))
            .Select(e => new Components.GenericGaze.Eye(e.End, e.Activation.AddSeconds(-0.6), range: 100, actorID: e.Actor.InstanceID)).ToArray();
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
            return;
        if (spell.Action.ID is 48506 or 48508)
        {
            var oid = spell.Action.ID == 48506 ? 19612u : 19613u;
            var eye = _eyes.Values.Where(e => !e.Resolved && !e.Actor.IsDeadOrDestroyed && e.OID == oid)
                .MinBy(e => (e.Actor.Position - spell.LocXZ).LengthSq());
            if (eye == null || !eye.Actor.Position.AlmostEqual(spell.LocXZ, 2))
            {
                // Late activation may miss the moving actors entirely. The
                // helper still provides an authoritative locked position/time.
                eye = new(caster, oid);
                _eyes[caster.InstanceID] = eye;
            }
            eye.End = spell.LocXZ;
            eye.Activation = Module.CastFinishAt(spell);
            eye.Confirmed = true;
            eye.CastActorID = caster.InstanceID;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
            return; // Keep the normal cast through its effect packet.
        if (spell.Action.ID == 48509 && spell.NPCRemainingTime > 0.5f)
            foreach (var eye in _eyes.Values)
                eye.HasGaze = false;
        // These casts last only 0.2s; the usual 0.5s NPC threshold misses them.
        else if (spell.Action.ID is 48506 or 48508 && spell.Interrupted)
            foreach (var eye in _eyes.Values)
                if (eye.CastActorID == caster.InstanceID)
                    eye.Resolved = true;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 48506 or 48508)
        {
            var oid = spell.Action.ID == 48506 ? 19612u : 19613u;
            foreach (var eye in _eyes.Values)
                if (eye.OID == oid && (eye.CastActorID == caster.InstanceID
                    || eye.End.AlmostEqual(caster.Position, 2) && eye.Activation <= WorldState.FutureTime(1)))
                    eye.Resolved = true;
        }
        if (spell.Action.ID == 48510)
        {
            // Release facing after this eye's gaze, while retaining its explosion
            // and every later wave's independent gaze marker.
            foreach (var eye in _eyes.Values)
                if (eye.HasGaze && eye.End.AlmostEqual(caster.Position, 2) && eye.Activation <= WorldState.FutureTime(1))
                    eye.GazeResolved = true;
        }
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == 19611)
            _eyes.Clear();
        else
        {
            _eyes.Remove(actor.InstanceID);
            foreach (var eye in _eyes.Values)
                if (eye.CastActorID == actor.InstanceID)
                    eye.Resolved = true;
        }
    }
}

sealed class CrucibleCatoblepasGaze(BossModule module) : Components.GenericGaze(module, 48510u)
{
    public override ReadOnlySpan<Eye> ActiveEyes(int slot, Actor actor) => Module.FindComponent<CrucibleCatoblepasEyes>()?.GazeEyes() ?? [];

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        foreach (var eye in ActiveEyes(slot, actor))
            hints.Add("恶魔义眼：背对被连线眼球的落点，同时进入环形爆炸安全点", HitByEye(ref actor, eye));
    }
}
