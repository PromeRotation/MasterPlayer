// Ported from BossmodRebornCN f6914f3e8. See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// Normal second board, ARR 2026-09-10 21:50:57. The helper CAST rotation is
// already turned toward the attacking half; its effect rotation is the boss's
// facing instead. Do not apply another left/right offset to the actual casts.
sealed class CrucibleManticoreHalfArena(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCone Hammer = new(30f, 90f.Degrees());
    private static readonly AOEShapeCone ClawTail = new(40f, 90f.Degrees());
    private readonly List<uint> _arms = [];
    private readonly HashSet<ulong> _previewCasters = [];
    private readonly Dictionary<uint, AOEInstance> _predicted = [];
    private readonly HashSet<uint> _resolved = [];
    private readonly List<AOEInstance> _displayed = [];
    private bool _combo;
    private bool _armOrderKnown;
    private WPos _finalPosition;
    private Angle _finalFacing;
    private DateTime _firstActivation;
    private DateTime _comboActivation;

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        48123 or 48125 or 48132 or 48134 => new(Hammer, true),
        48140 or 48142 or 50411 or 50413 => new(ClawTail, true),
        _ => null
    };

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        foreach (var aoe in base.ActiveAOEs(slot, actor))
            _displayed.Add(aoe);
        foreach (var (action, aoe) in _predicted.ToArray())
        {
            if (aoe.Activation.AddSeconds(2d) < WorldState.CurrentTime)
            {
                _predicted.Remove(action);
                _resolved.Add(action);
            }
            else
                _displayed.Add(aoe);
        }
        _displayed.Sort((a, b) => a.Activation.CompareTo(b.Activation));
        var aoes = CollectionsMarshal.AsSpan(_displayed);
        for (var i = 0; i < aoes.Length; ++i)
        {
            // Opposite halves resolve ~2.1s apart. Keep both previews but never
            // prohibit both halves before the first one has cleared.
            aoes[i].Risky = aoes[i].Activation <= aoes[0].Activation.AddSeconds(0.5d);
            aoes[i].Color = aoes[i].Risky ? Colors.Danger : Colors.AOE;
        }
        return aoes;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;

        if (spell.Action.ID == 48127 && caster == Module.PrimaryActor)
        {
            var activation = Module.CastFinishAt(spell);
            if (_combo && Math.Abs((activation - _comboActivation).TotalSeconds) < 0.75d)
                return; // A cast resync must not discard an already observed arm order.
            ResetCombo();
            _combo = true;
            _comboActivation = activation;
            // Status notifications can arrive before the cast notification. At
            // the real start only the first arm is raised; the second rises 5s later.
            // A late load with both buffs cannot reconstruct their order safely.
            foreach (var status in caster.Statuses)
                if (status.ID is 2056 or 2193)
                    _arms.Add(status.ID);
            _armOrderKnown = _arms.Count <= 1;
        }
        else if (_combo && spell.Action.ID == 48128)
        {
            // Four path previews precede the actual charges by ~7.1s. The final
            // leg reveals the hammer's future position/facing ~10.9s before it hits.
            if (_previewCasters.Add(caster.InstanceID) && _previewCasters.Count == 4)
            {
                var direction = spell.LocXZ - caster.Position;
                if (direction.LengthSq() < 0.01f)
                    return; // No trustworthy final-leg facing; actual hammer casts still work.
                _finalPosition = spell.LocXZ;
                _finalFacing = Angle.FromDirection(direction);
                _firstActivation = Module.CastFinishAt(spell, 9.4d);
                PredictHammers();
            }
            else if (_previewCasters.Count > 4)
            {
                // An unrecorded path length must not retain the fourth-leg guess.
                _firstActivation = default;
                _predicted.Clear();
            }
        }
        else if (_combo && spell.Action.ID == 48130 && _firstActivation != default && spell.LocXZ.AlmostEqual(_finalPosition, 0.5f))
        {
            // Reconcile the final real dash; don't keep the preview's estimated time.
            var direction = spell.LocXZ - caster.Position;
            if (direction.LengthSq() >= 0.01f)
                _finalFacing = Angle.FromDirection(direction);
            _firstActivation = Module.CastFinishAt(spell, 2.3d);
            PredictHammers();
        }

        if (spell.Action.ID is 48132 or 48134)
        {
            _predicted.Remove(spell.Action.ID);
            _resolved.Add(spell.Action.ID); // Actual cast owns this half from now on.
        }
        base.OnCastStarted(caster, spell);
    }

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (_combo && _armOrderKnown && actor == Module.PrimaryActor && status.ID is 2056 or 2193 && !_arms.Contains(status.ID))
        {
            _arms.Add(status.ID);
            PredictHammers();
        }
    }

    private void PredictHammers()
    {
        if (!_armOrderKnown || _arms.Count != 2 || _firstActivation == default)
            return;
        for (var i = 0; i < _arms.Count; ++i)
        {
            // Opening casts prove the hidden statuses: 2056 turns -90 degrees,
            // 2193 turns +90. After the final westward dash these become south/north.
            var action = _arms[i] == 2056 ? 48132u : 48134u;
            if (!_resolved.Contains(action))
                _predicted[action] = new(Hammer, _finalPosition,
                    _finalFacing + (_arms[i] == 2056 ? -90f : 90f).Degrees(), _firstActivation.AddSeconds(2.1d * i));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48127 && spell.Interrupted)
            ResetCombo();
        base.OnCastFinished(caster, spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 48132 or 48134)
        {
            _predicted.Remove(spell.Action.ID);
            _resolved.Add(spell.Action.ID);
        }
        base.OnEventCast(caster, spell);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            ResetCombo();
        base.OnActorDestroyed(actor);
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    private void ResetCombo()
    {
        _combo = false;
        _armOrderKnown = false;
        _arms.Clear();
        _previewCasters.Clear();
        _predicted.Clear();
        _resolved.Clear();
        _firstActivation = default;
        _comboActivation = default;
    }

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
        // Stand near the dividing line before the first hammer. The other half
        // follows only 2.1s later; a far-edge safe point is not a usable staging point.
        if (_displayed.Count >= 2 && _displayed[0].Shape == Hammer && !_displayed[1].Risky)
        {
            var first = _displayed[0];
            hints.AddForbiddenConstraint(new HammerStaging(first.Origin, first.Rotation), first.Activation.AddSeconds(-1), first.Activation.AddSeconds(0.6));
        }
    }

    private sealed class HammerStaging(WPos origin, Angle rotation) : ShapeDistance
    {
        public override float Distance(in WPos p) => 3f - Math.Abs((p - origin).Dot(rotation.ToDirection()));
    }
}

// The four harmless 48128 previews reveal the damaging 48130 routes ~8.6s
// before impact. Type 8 encodes the END in LocXZ and has no useful cast rotation.
sealed class CrucibleManticoreCharges(BossModule module) : Components.GenericAOEs(module, 48130u)
{
    private readonly List<AOEInstance> _legs = [];
    private readonly HashSet<ulong> _previews = [];
    private readonly HashSet<uint> _events = [];
    private readonly HashSet<ulong> _resolvedCasters = [];
    private DateTime _combo;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        if (spell.Action.ID == 48127)
        {
            var activation = Module.CastFinishAt(spell);
            if (Math.Abs((activation - _combo).TotalSeconds) < 0.75d)
                return;
            _combo = activation;
            _legs.Clear(); _previews.Clear(); _events.Clear(); _resolvedCasters.Clear();
        }
        if (spell.Action.ID is not (48128 or 48130))
            return;
        if (spell.Action.ID == 48130 && _resolvedCasters.Contains(caster.InstanceID))
            return;
        if (spell.Action.ID == 48128 && !_previews.Add(caster.InstanceID))
            return;
        var delta = spell.LocXZ - caster.Position;
        if (delta.LengthSq() < 0.01f)
            return;
        var rotation = Angle.FromDirection(delta);
        var aoe = new AOEInstance(new AOEShapeRect(delta.Length(), 4), caster.Position, rotation,
            Module.CastFinishAt(spell, spell.Action.ID == 48128 ? 7.1d : 0), actorID: caster.InstanceID);
        if (spell.Action.ID == 48130)
            _legs.RemoveAll(a => a.Origin.AlmostEqual(aoe.Origin, 0.5f) && Math.Abs((a.Rotation - rotation).Normalized().Deg) < 2);
        _legs.Add(aoe);
        _legs.Sort((a, b) => a.Activation.CompareTo(b.Activation));
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _legs.RemoveAll(a => a.Activation.AddSeconds(1) < WorldState.CurrentTime);
        return CollectionsMarshal.AsSpan(_legs);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction && _events.Add(spell.GlobalSequence))
        {
            _resolvedCasters.Add(caster.InstanceID);
            _legs.RemoveAll(a => a.Origin.AlmostEqual(caster.Position, 0.5f) && Math.Abs((a.Activation - WorldState.CurrentTime).TotalSeconds) < 1);
            ++NumCasts;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Interrupted)
        {
            if (spell.Action.ID == 48127)
                _legs.Clear();
            else if (spell.Action.ID == 48130)
                _legs.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            _legs.Clear();
        else
            _legs.RemoveAll(a => a.ActorID == actor.InstanceID);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var aoes = ActiveAOEs(slot, actor);
        // Look ahead one leg, without treating the entire crossing pattern as permanent.
        for (var i = 0; i < aoes.Length; ++i)
            CrucibleScorpionAI.Avoid(hints, aoes[i]);
    }
}
