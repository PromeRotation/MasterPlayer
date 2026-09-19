using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// 48607 uses both knockback rows 234/235: the two sides of the central line
// are pushed outwards by 20y. Both endpoints must be on the irregular floor.
sealed class CrucibleGatlerTorrent(BossModule module) : Components.GenericKnockback(module, 48607u)
{
    private DateTime _activation;
    private ulong _caster;
    private Angle _rotation;
    private readonly Knockback[] _source = new Knockback[1];

    private WDir Displacement(WPos p)
    {
        var normal = _rotation.ToDirection().OrthoR();
        return normal * ((p - Module.Center).Dot(normal) >= 0 ? 20 : -20);
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        if (_activation == default || _activation.AddSeconds(1) < WorldState.CurrentTime)
            return [];
        _source[0] = new(Module.Center, 20, _activation, direction: Angle.FromDirection(Displacement(actor.Position)), kind: Kind.DirForward);
        return _source;
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !CrucibleLaudaFloor.Contains(pos);
    private sealed class LandingZone(CrucibleGatlerTorrent component) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            return CrucibleLaudaFloor.SafeTravel(p, component.Displacement(p)) ? 1 : -1;
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (ActiveKnockbacks(slot, actor).Length != 0 && !IsImmune(slot, _activation))
            hints.AddForbiddenConstraint(new LandingZone(this), _activation.AddSeconds(-0.5));
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && !spell.EventHappened && Module.CastFinishAt(spell) > WorldState.CurrentTime)
        {
            _activation = Module.CastFinishAt(spell);
            _rotation = spell.Rotation;
            _caster = caster.InstanceID;
        }
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && spell.Interrupted)
            _activation = default;
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            _activation = default;
            ++NumCasts;
        }
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.InstanceID == _caster || actor == Module.PrimaryActor)
            _activation = default;
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// This is one boss-to-player tether, not Lauda's three summoned swords.
// Keep the ray parallel to the long edges to miss the statues at X500/540.
sealed class CrucibleGatlerBait(BossModule module) : Components.GenericBaitAway(module)
{
    private WPos? _point;
    private DateTime _lockAt;
    private bool _moving;

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (source.OID != 19626 || tether.ID != 1 || WorldState.Actors.Find(tether.Target) is not { } target)
            return;
        if (CurrentBaits.Any(b => b.Source == source && b.Target == target))
            return;
        Clear();
        _point = new(Module.Center.X, Module.Center.Z + (target.Position.Z >= source.Position.Z ? 22.5f : -22.5f));
        _lockAt = WorldState.FutureTime(4.6);
        CurrentBaits.Add(new(source, target, new AOEShapeRect(60, 3), _lockAt));
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        if (spell.Action.ID == 48612)
            _lockAt = Module.CastFinishAt(spell, -0.4);
        else if (spell.Action.ID == 48614)
            Clear(); // Helper start locks the ground line: stop holding and evade it.
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48612 && spell.Interrupted)
            Clear();
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 48613 or 48614)
            Clear();
    }
    public override void Update()
    {
        if (_lockAt.AddSeconds(4) < WorldState.CurrentTime || CurrentBaits.Any(b => b.Source.IsDeadOrDestroyed || b.Target.IsDeadOrDestroyed))
            Clear();
    }
    private void Clear()
    {
        CurrentBaits.Clear();
        _point = null;
        _moving = false;
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (_point is not { } p || !IsBaitTarget(actor))
            return;
        _moving |= WorldState.FutureTime((p - actor.Position).Length() / 6 + 1) >= _lockAt;
        if (_moving)
        {
            hints.GoalPoints.Add((p, 0.3f, 100));
            hints.MaxCastTime = 0;
        }
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (IsBaitTarget(actor))
            hints.Add("支配魔刃：沿长边方向引导，避开两侧石像；锁线后躲开！", false);
        else
            base.AddHints(slot, actor, hints);
    }
    public override void DrawArenaForeground(int slot, Actor actor)
    {
        base.DrawArenaForeground(slot, actor);
        if (_point is { } p && IsBaitTarget(actor))
            Arena.ZoneCircleOutline(p, 0.8f, Colors.Safe);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (CurrentBaits.Any(b => b.Source == actor || b.Target == actor))
            Clear();
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// Icon471 follows the marked target until the hit. Do not chase one's own line.
sealed class CrucibleGatlerThunder(BossModule module) : Components.BaitAwayCast(module, 48620u, new AOEShapeRect(50, 3), endsOnCastEvent: true, tankbuster: true)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction || spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        CurrentBaits.RemoveAll(b => b.Source == caster);
        base.OnCastStarted(caster, spell);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && spell.Interrupted)
            CurrentBaits.RemoveAll(b => b.Source == caster);
    }
    public override void Update() => CurrentBaits.RemoveAll(b => b.Source.IsDeadOrDestroyed || b.Target.IsDeadOrDestroyed || b.Activation.AddSeconds(2) < WorldState.CurrentTime);
}

// 669 fixes an R6 drop after ~5.4s, then a distance-falloff explosion.
// The R80 sheet range is NOT an arena-wide forbidden zone.
sealed class CrucibleGatlerFireKnife(BossModule module) : BossComponent(module)
{
    private ulong _target, _caster;
    private WPos? _drop, _explosion;
    private DateTime _dropUntil, _explodeAt;

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID != 669)
            return;
        _target = actor.InstanceID;
        WPos[] tips = [new(520, -442.5f), new(520, -397.5f), new(507.5f, -422.5f), new(533, -417.5f)];
        _drop = tips.MinBy(p => (p - actor.Position).LengthSq());
        _dropUntil = WorldState.FutureTime(8);
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        if (spell.Action.ID is 48618 or 48619)
        {
            _drop = null;
            _explosion = spell.LocXZ;
            _caster = caster.InstanceID;
            _explodeAt = Module.CastFinishAt(spell, spell.Action.ID == 48618 ? 9 : 0);
        }
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48619)
            _explosion = null;
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 48615 or 48618 or 48619 && spell.Interrupted)
        {
            _drop = null;
            _explosion = null;
        }
    }
    public override void Update()
    {
        if (_dropUntil < WorldState.CurrentTime)
            _drop = null;
        if (_explodeAt.AddSeconds(1) < WorldState.CurrentTime)
            _explosion = null;
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_drop is { } drop && _target == actor.InstanceID)
        {
            hints.GoalPoints.Add((drop, 0.3f, 100));
            hints.MaxCastTime = 0;
        }
        if (_explosion is { } explosion)
            hints.GoalZones.Add(p => Math.Min(12, (p - explosion).Length() / 4));
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_drop != null && _target == actor.InstanceID)
            hints.Add("火焰魔刃：放到边缘安全凸台，落点固定后远离！", false);
        if (_explosion != null)
            hints.Add("魔刃爆焰：远离飞刀，距离越远伤害越低！", false);
    }
    public override void DrawArenaForeground(int slot, Actor actor)
    {
        if (_drop is { } drop && _target == actor.InstanceID)
            Arena.ZoneCircleOutline(drop, 0.8f, Colors.Safe);
        if (_explosion is { } explosion)
            Arena.ZoneCircleOutline(explosion, 6, Colors.Danger);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.InstanceID == _target || actor == Module.PrimaryActor)
            _drop = null;
        if (actor.InstanceID == _caster || actor == Module.PrimaryActor)
            _explosion = null;
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}
