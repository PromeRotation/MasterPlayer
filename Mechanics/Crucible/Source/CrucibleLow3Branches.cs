// Ported from BossmodRebornCN f6914f3e8. See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// Knockback 114: 35y forward, not away from the helper or a 60x60 damage zone.
sealed class CrucibleYmirTsunami(BossModule module)
    : Low3CrucibleKnockback(module, 48481u, 35, Kind.DirForward, new AOEShapeRect(60, 30))
{
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InRect(Module.Center, 19.5f, 19.5f);
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var kb in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, kb.Activation))
                hints.AddForbiddenConstraint(new LandingZone([kb], Module.Center, 19.5f, 19.5f, []), kb.Activation.AddSeconds(-0.5));
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
    private sealed class MovingEye(Actor actor)
    {
        public readonly Actor Actor = actor;
        public readonly WPos Start = actor.Position;
        public WPos Last = actor.Position;
        public WPos End;
        public DateTime Activation;
        public bool Resolved;
        public bool Confirmed;
    }
    private readonly Dictionary<ulong, MovingEye> _eyes = [];
    private static readonly AOEShapeCircle Circle = new(25);
    private static readonly AOEShapeCircle Spot = new(1);
    private bool _transplant;
    private DateTime _gazeResolvedUntil;
    private readonly HashSet<uint> _gazes = [];

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID is 19612 or 19613)
            _eyes.TryAdd(actor.InstanceID, new(actor));
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
    private WPos Destination(MovingEye eye) => eye.End + 3 * (Module.Center - eye.End).Normalized();

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var eyes = PendingEyes();
        if (eyes.Length == 0)
            return [];
        var deadline = eyes[0].Activation.AddSeconds(1);
        return eyes.Where(e => e.Activation <= deadline).Select(e => e.Actor.OID == 19612
            ? new AOEInstance(Circle, e.End, activation: e.Activation)
            : new AOEInstance(Spot, Destination(e), activation: e.Activation, color: Colors.SafeFromAOE, risky: false)).ToArray();
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            if (aoe.Risky)
                CrucibleScorpionAI.Avoid(hints, aoe);
            else
                hints.AddForbiddenConstraint(new SDInvertedCircle(aoe.Origin, 1), aoe.Activation.AddSeconds(-0.4), aoe.Activation.AddSeconds(0.6));
    }
    // Both eyes may carry the transplanted gaze; the ARR alternates which one
    // fires. Facing away from both endpoint directions satisfies either variant.
    public Components.GenericGaze.Eye[] GazeEyes()
    {
        var eyes = PendingEyes();
        if (!_transplant || eyes.Length == 0)
            return [];
        return eyes.Where(e => e.Activation <= eyes[0].Activation.AddSeconds(1)
            && e.Activation > _gazeResolvedUntil).Select(e => new Components.GenericGaze.Eye(e.End, e.Activation.AddSeconds(-0.6))).ToArray();
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
            return;
        if (spell.Action.ID == 48509)
            _transplant = true;
        if (spell.Action.ID is 48506 or 48508)
        {
            var oid = spell.Action.ID == 48506 ? 19612u : 19613u;
            var eye = _eyes.Values.Where(e => !e.Resolved && e.Actor.OID == oid)
                .MinBy(e => (e.Actor.Position - spell.LocXZ).LengthSq());
            if (eye != null && eye.Actor.Position.AlmostEqual(spell.LocXZ, 2))
            {
                eye.End = spell.LocXZ;
                eye.Activation = Module.CastFinishAt(spell);
                eye.Confirmed = true;
            }
        }
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 48506 or 48508)
        {
            var oid = spell.Action.ID == 48506 ? 19612u : 19613u;
            foreach (var eye in _eyes.Values)
                if (eye.Actor.OID == oid && eye.End.AlmostEqual(caster.Position, 2) && eye.Activation <= WorldState.FutureTime(1))
                    eye.Resolved = true;
        }
        if (spell.Action.ID == 48510 && _gazes.Add(spell.GlobalSequence))
        {
            // The gaze precedes the explosion by ~0.5s; do not keep forcing the
            // player's facing after it has resolved.
            _gazeResolvedUntil = WorldState.FutureTime(1);
        }
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == 19611)
            _eyes.Clear();
        else
            _eyes.Remove(actor.InstanceID);
    }
}

sealed class CrucibleCatoblepasGaze(BossModule module) : Components.GenericGaze(module, 48510u)
{
    public override ReadOnlySpan<Eye> ActiveEyes(int slot, Actor actor) => Module.FindComponent<CrucibleCatoblepasEyes>()?.GazeEyes() ?? [];
}
