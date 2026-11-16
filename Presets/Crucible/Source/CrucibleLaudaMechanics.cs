// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// R40 explosions cover the giant's arena. They are damage checks, not safe-position puzzles.
sealed class CrucibleGiantExplosionHint(BossModule module) : Components.CastHints(module, [49373u, 49374u, 49375u, 49376u], "凝胶炸裂：全屏伤害，准备减伤！");

// Installed Action 49364 uses the R2 tower omen; 49365 is the delayed
// R60 failure pulse with damage-down 4874, not another ground circle.
sealed class CrucibleGiantThunderTowers(BossModule module) : Components.GenericAOEs(module, 49364u)
{
    public readonly List<AOEInstance> Towers = [];
    private static readonly AOEShapeCircle Shape = new(2);
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(Towers);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Towers.Count > 0)
        {
            var tower = Towers.OrderBy(t => t.Activation).ThenBy(t => (t.Origin - actor.Position).LengthSq()).First();
            hints.AddForbiddenConstraint(new SDInvertedCircle(tower.Origin, 2), tower.Activation);
            var travel = MathF.Max(0,(tower.Origin-actor.Position).Length()-1)/5;
            if (WorldState.FutureTime(travel+1.4) >= tower.Activation)
                hints.GoalPoints.Add((tower.Origin,.75f,120));
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction || spell.EventHappened)
            return;
        Towers.RemoveAll(t => t.ActorID == caster.InstanceID);
        Towers.Add(new(Shape, spell.LocXZ, default, Module.CastFinishAt(spell), Colors.Safe, false, caster.InstanceID));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && spell.Interrupted)
            Towers.RemoveAll(t => t.ActorID == caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 49364 or 49365)
            Towers.RemoveAll(t => t.ActorID == caster.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor) => Towers.RemoveAll(t => t.ActorID == actor.InstanceID);
    public override void Update() => Towers.RemoveAll(t => t.Activation.AddSeconds(2) < WorldState.CurrentTime
        || WorldState.Actors.Find(t.ActorID)?.IsDeadOrDestroyed != false);
}

// ARR 22:48:06: timeline 4562 precedes the hit by 15s, 4576 by 8s.
// Four waves are 2s apart. The final helper cast alone gives only 1s of warning.
sealed class CrucibleLaudaBlades(BossModule module) : Components.GenericAOEs(module, 49483u)
{
    private const double HitWindowSeconds = .6, EndSafetySeconds = .3;
    private static readonly AOEShapeRect Shape = new(50, 2.5f);
    private readonly List<AOEInstance> _pending = [];
    private readonly Dictionary<ulong, DateTime> _resolved = [];
    private readonly List<AOEInstance> _display = [];

    private void Prune()
    {
        // Keep the published order through the original retirement window.
        // Removing one hit immediately reindexes every remaining wave in CrucibleOutput.
        _pending.RemoveAll(a => a.Activation.AddSeconds(2) < WorldState.CurrentTime
            && (!_resolved.TryGetValue(a.ActorID, out var hit)
                || hit.AddSeconds(HitWindowSeconds + EndSafetySeconds) < WorldState.CurrentTime));
        _pending.Sort((a, b) => a.Activation.CompareTo(b.Activation));
        if (_pending.Count == 0) _resolved.Clear();
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        Prune();
        _display.Clear();
        // Color remains time-graded; PR receives all windows through AddAIHints.
        var horizon = _pending.Count > 0 ? _pending[0].Activation.AddSeconds(2.2) : default;
        foreach (var pending in _pending)
        {
            if (_resolved.TryGetValue(pending.ActorID, out var hit) && hit.AddSeconds(EndSafetySeconds) < WorldState.CurrentTime)
                continue;
            var aoe = pending;
            aoe.Risky = aoe.Activation <= horizon;
            aoe.Color = aoe.Activation <= horizon.AddSeconds(-1.7) ? Colors.Danger : Colors.AOE;
            _display.Add(aoe);
        }
        return CollectionsMarshal.AsSpan(_display);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        Prune();
        foreach (var aoe in _pending)
        {
            if (_resolved.TryGetValue(aoe.ActorID, out var hit))
            {
                // Keep the full predicted hit window. A late observed effect can extend it.
                var end = (aoe.Activation > hit ? aoe.Activation : hit).AddSeconds(HitWindowSeconds);
                hints.AddForbiddenZone(aoe.Shape, aoe.Origin, aoe.Rotation,
                    aoe.Activation < hit ? aoe.Activation : hit, end);
            }
            else
                hints.AddForbiddenZone(aoe.Shape, aoe.Origin, aoe.Rotation, aoe.Activation);
        }
    }

    public override void OnActorPlayActionTimelineEvent(Actor actor, ushort id)
    {
        if (actor.OID != 19745 || id is not (4562 or 4576) || actor.IsDeadOrDestroyed)
            return;
        // The first cue is not allowed to restart a cue already in progress.
        if (id == 4562 && _pending.Any(a => a.ActorID == actor.InstanceID))
            return;
        var offset = actor.Position - Module.Center;
        var direction = Math.Abs(offset.X) / 15f >= Math.Abs(offset.Z) / 25f
            ? new WDir(-Math.Sign(offset.X), 0) : new WDir(0, -Math.Sign(offset.Z));
        var activation = WorldState.FutureTime(id == 4562 ? 15 : 8);
        foreach (var pending in _pending)
            if (pending.ActorID == actor.InstanceID && pending.Activation < activation)
                activation = pending.Activation;
        Replace(actor.InstanceID, actor.Position, Angle.FromDirection(direction), activation);
    }

    private void Replace(ulong actorID, WPos origin, Angle rotation, DateTime activation)
    {
        _resolved.Remove(actorID);
        _pending.RemoveAll(a => a.ActorID == actorID || a.Origin.AlmostEqual(origin, 0.25f));
        _pending.Add(new(Shape, origin, rotation, activation, actorID: actorID));
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && !spell.EventHappened && Module.CastFinishAt(spell) > WorldState.CurrentTime)
            Replace(caster.InstanceID, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && spell.Interrupted)
            _pending.RemoveAll(a => a.ActorID == caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            var casterKnown = _pending.Any(a => a.ActorID == caster.InstanceID);
            foreach (var aoe in _pending)
                if (aoe.ActorID == caster.InstanceID || !casterKnown
                    && aoe.Origin.AlmostEqual(caster.Position, 0.25f) && aoe.Activation <= WorldState.FutureTime(.5))
                    _resolved.TryAdd(aoe.ActorID, WorldState.CurrentTime);
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        _resolved.Remove(actor.InstanceID);
        _pending.RemoveAll(a => a.ActorID == actor.InstanceID);
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// 49473 is a visible preparation cue.  Only the actual helper cast declares
// the radial knockback source and deadline; a normal cast end does not resolve it.
sealed class CrucibleLaudaPhysicalAxe(BossModule module) : Components.GenericKnockback(module, 49476u)
{
    private DateTime _prepareBy, _activation, _expires;
    private ulong _caster;
    private WPos _origin;
    private WPos? _goal;
    private bool _moving;
    private LandingZone? _landing;
    private readonly Knockback[] _sources = new Knockback[1];

    private bool Refresh()
    {
        if (_caster == 0)
            return false;
        if (_expires < WorldState.CurrentTime || WorldState.Actors.Find(_caster) is not { IsDeadOrDestroyed: false } source)
        {
            Clear();
            return false;
        }
        if (!source.Position.AlmostEqual(_origin, .01f))
        {
            _origin = source.Position;
            _goal = null;
            _landing = null;
        }
        return true;
    }

    private void Clear()
    {
        _caster = 0;
        _prepareBy = _activation = _expires = default;
        _goal = null;
        _landing = null;
        _moving = false;
    }

    private sealed class LandingZone(WPos origin) : ShapeDistance
    {
        private FrozenConstraintGeometry? _geometry;
        public override float Distance(in WPos p) => (p - origin).LengthSq() > .01f
            && CrucibleLaudaFloor.SafeTravel(p, (p - origin).Normalized() * 20) ? 1 : -1;
        public override MasterPlayer.Spatial.Geometry.IGeometryShape Geometry(float y)
        {
            if (_geometry != null && _geometry.Position.Y == y) return _geometry;
            return _geometry = FrozenConstraintFactory.Create(p => Distance(p), CrucibleLaudaFloor.Center, y, radius: 26);
        }
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        if (!Refresh() || _activation == default)
            return [];
        _sources[0] = new(_origin, 20, _activation, actorID: _caster);
        return _sources;
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !CrucibleLaudaFloor.Contains(pos);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!Refresh() || actor.IsDeadOrDestroyed)
            return;
        if (_goal == null || !_moving)
        {
            var points = new[] { Module.Center + new WDir(0, 2.5f), Module.Center - new WDir(0, 2.5f) }
                .Where(p => CrucibleLaudaFloor.SafeTravel(p, (p - _origin).Normalized() * 20))
                .OrderBy(p => (p - actor.Position).LengthSq()).ToArray();
            if (points.Length == 0)
                return;
            _goal = points[0];
        }
        var goal = _goal.Value;
        _moving |= WorldState.FutureTime((actor.Position - goal).Length() / 6 + .8) >= _prepareBy;
        if (!_moving)
            return;
        hints.GoalPoints.Add((goal, .5f, 220));
        hints.AddForbiddenConstraint(_landing ??= new(_origin), _prepareBy);
        // Radial flight magnifies a small sideways start error.  Use the existing
        // ordinary walking validator and the production controller's precise
        // endpoint, retaining all floor, danger and input-acquisition checks.
        var visible = Module.Components.OfType<Components.GenericAOEs>()
            .SelectMany(c => c.ActiveAOEs(slot, actor).ToArray()).ToArray();
        var route = Module.FindComponent<CrucibleLaudaFireKnife>()?.TryValidatedEscape(actor.Position, goal, visible, hints, .1);
        if (route != null)
        {
            hints.DirectRoute = route;
            hints.DirectRouteDeadline = _prepareBy;
        }
        hints.MaxCastTime = 0;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is not (49473 or 49476) || spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        var helper = spell.Action.ID == WatchedAction;
        if (!helper)
            Clear();
        _caster = caster.InstanceID;
        _origin = caster.Position;
        _prepareBy = Module.CastFinishAt(spell, -.5f);
        _activation = helper ? Module.CastFinishAt(spell) : default;
        _expires = Module.CastFinishAt(spell, helper ? 1 : 3);
        _goal = null;
        _landing = null;
        _moving |= helper;
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster.InstanceID == _caster && spell.Action.ID is 49473 or 49476 && spell.Interrupted)
            Clear();
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster.InstanceID == _caster && spell.Action.ID == WatchedAction)
        {
            Clear();
            ++NumCasts;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.InstanceID == _caster)
            Clear();
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
    public override void Update() => Refresh();
}

// Both recorded casts use the same origin/rotation. Row 234 pushes the north
// half north and row 235 pushes the south half south: split away from the cast's
// center line, not a fixed source-right knockback for every player.
sealed class CrucibleLaudaTorrent(BossModule module) : Components.GenericKnockback(module, 49453u)
{
    private DateTime _activation;
    private ulong _caster;
    private Angle _rotation;
    private readonly Knockback[] _source = new Knockback[1];
    private LandingZone? _landing;

    private WDir Displacement(WPos position)
    {
        var normal = _rotation.ToDirection().OrthoR();
        return normal * ((position - Module.Center).Dot(normal) >= 0 ? 20 : -20);
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        if (_activation == default || _activation.AddSeconds(1) < WorldState.CurrentTime)
            return [];
        _source[0] = new(Module.Center, 20, _activation, direction: Angle.FromDirection(Displacement(actor.Position)), kind: Kind.DirForward);
        return _source;
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !CrucibleLaudaFloor.Contains(pos);

    private sealed class LandingZone(Angle rotation) : ShapeDistance
    {
        private FrozenConstraintGeometry? _geometry;
        public override float Distance(in WPos p)
        {
            var normal = rotation.ToDirection().OrthoR();
            var displacement = normal * ((p - CrucibleLaudaFloor.Center).Dot(normal) >= 0 ? 20 : -20);
            return CrucibleLaudaFloor.SafeTravel(p, displacement) ? 1 : -1;
        }
        public override MasterPlayer.Spatial.Geometry.IGeometryShape Geometry(float y)
        {
            if (_geometry != null && _geometry.Position.Y == y) return _geometry;
            return _geometry = FrozenConstraintFactory.Create(p => Distance(p), CrucibleLaudaFloor.Center, y, radius: 26);
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (ActiveKnockbacks(slot, actor).Length != 0 && !IsImmune(slot, _activation))
        {
            hints.AddForbiddenConstraint(_landing ??= new(_rotation), _activation.AddSeconds(-0.5));
            var normal = _rotation.ToDirection().OrthoR();
            var points = new[] { Module.Center + normal * 2.5f, Module.Center - normal * 2.5f }
                .Where(p => CrucibleLaudaFloor.SafeTravel(p, Displacement(p)))
                .Where(p => Module.FindComponent<CrucibleLaudaBlades>() is not { } blades
                    || !blades.ActiveAOEs(slot, actor).ToArray().Any(a => a.Activation >= WorldState.CurrentTime
                        && a.Activation <= _activation && a.Check(p)))
                .OrderBy(p => (p - actor.Position).LengthSq()).ToArray();
            if (points.Length != 0)
                hints.GoalPoints.Add((points[0], 0.5f, 150));
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && !spell.EventHappened && Module.CastFinishAt(spell) > WorldState.CurrentTime)
        {
            _activation = Module.CastFinishAt(spell);
            _rotation = spell.Rotation;
            _landing = null;
            _caster = caster.InstanceID;
        }
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
        if (actor.InstanceID == _caster)
            _activation = default;
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster.InstanceID == _caster && spell.Action.ID == WatchedAction && spell.Interrupted)
            _activation = default;
    }
}

// Thunder follows its cast target all the way through the hit (Icon471). The
// marked player cannot sidestep their own line; do not create a self-chasing zone.
sealed class CrucibleLaudaThunder(BossModule module) : Components.BaitAwayCast(module, 49470u, new AOEShapeRect(50, 3), endsOnCastEvent: true, tankbuster: true)
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

// Three Tether1 sources aim at the player, then lock into 49464/50848 ground lines.
// Use baits while aiming and the actual helper cast once locked.
sealed class CrucibleLaudaBaits(BossModule module) : Components.GenericBaitAway(module)
{
    private WPos? _baitPoint;
    private DateTime _lockAt, _prepareUntil;
    private bool _moving, _locked;
    private WPos? _lastWalkingGoal, _observedWalkingPosition, _innerPlacement, _postPlacementBait;
    private DateTime _observedWalkingAt, _walkingUntil;
    public bool Preparing => !_locked && (_prepareUntil > WorldState.CurrentTime || CurrentBaits.Count > 0);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (!Preparing)
        {
            // After native aim cancellation, use only the helper geometry
            // already visible now. Every nonzero walk is prechecked with the
            // regular .5/.1 preparation and checked again by the controller.
            var knife=Module.FindComponent<CrucibleLaudaFireKnife>();
            var visible=Module.FindComponent<XBMB45AOE>()?.ActiveAOEs(slot,actor).ToArray() ?? [];
            if(_locked && _walkingUntil>WorldState.CurrentTime && knife is {CircleEscapeActive:false} && visible.Any(a=>a.Shape is AOEShapeRect
                && a.Activation>WorldState.CurrentTime && a.Activation<WorldState.FutureTime(2.5)))
            {
                var pending=visible.Where(a=>a.Activation.AddSeconds(.7)>WorldState.CurrentTime && a.Activation<WorldState.FutureTime(2.5)).ToArray();
                WPos? best=null;var distance=float.MaxValue;
                for(var x=504f;x<=536;x+=.5f)
                    for(var z=-446f;z<=-394;z+=.5f)
                    {
                        var p=new WPos(x,z);var next=(p-actor.Position).LengthSq();
                        if(next>=distance || next<.01f || !CrucibleLaudaFloor.Contains(p)
                            ||!CrucibleLaudaFloor.Contains(p+new WDir(.5f,0))||!CrucibleLaudaFloor.Contains(p-new WDir(.5f,0))
                            ||!CrucibleLaudaFloor.Contains(p+new WDir(0,.5f))||!CrucibleLaudaFloor.Contains(p-new WDir(0,.5f))
                            ||pending.Any(a=>(a.ShapeDistance??a.Shape.Distance(a.Origin,a.Rotation)).Distance(p)<1))continue;
                        best=p;distance=next;
                    }
                if(best is {} goal)
                {
                    hints.GoalPoints.Add((goal,.5f,150));
                    var moving=_observedWalkingPosition is {} last && _observedWalkingAt<WorldState.CurrentTime
                        && _observedWalkingAt.AddSeconds(.3)>WorldState.CurrentTime && (actor.Position-last).LengthSq()>.01f;
                    if(_observedWalkingAt<WorldState.CurrentTime){_observedWalkingAt=WorldState.CurrentTime;_observedWalkingPosition=actor.Position;}
                    var route=knife.TryValidatedEscape(actor.Position,goal,visible,hints,moving?0:.1);
                    if(route!=null)_lastWalkingGoal=goal;
                    else if(_lastWalkingGoal is {} prior)route=knife.TryValidatedEscape(actor.Position,prior,visible,hints,moving?0:.1);
                    if(route!=null){hints.DirectRoute=route;hints.DirectRouteDeadline=pending.Min(a=>a.Activation);}
                }
            }
            return;
        }
        if(CurrentBaits.Count>0 && !IsBaitTarget(actor))return;
        // Choose once per wave. Only the two axial tips avoid the side statues.
        _baitPoint ??= Module.Center + new WDir(0, actor.Position.Z < Module.Center.Z ? -22.5f : 22.5f);
        var placementKnife=Module.FindComponent<CrucibleLaudaFireKnife>();
        var placementAt=placementKnife?.PendingPlacementDeadline ?? default;
        if(_moving && placementKnife!=null && placementAt>WorldState.CurrentTime)
        {
            var inner=_innerPlacement ?? Module.Center+new WDir(0,_baitPoint.Value.Z<Module.Center.Z?-21.5f:21.5f);
            // Current visible geometry decides when the inset becomes reachable.
            // A validated walk may approach directly from any safe source point;
            // requiring an intermediate goal tolerance can waste the short window.
            var visible=Module.FindComponent<XBMB45AOE>()?.ActiveAOEs(slot,actor).ToArray() ?? [];
            var route=placementKnife.TryValidatedEscape(actor.Position,inner,visible,hints,.1);
            if(route!=null)
            {
                var length=0f;var from=actor.Position;
                foreach(var p in route){length+=(p-from).Length();from=p;}
                if(WorldState.FutureTime(.1+length/6)<placementAt)
                {
                    _innerPlacement=inner;_baitPoint=inner;
                    hints.DirectRoute=route;hints.DirectRouteDeadline=placementAt;
                }
            }
        }
        if(_moving && placementKnife is {CircleEscapeActive:true,LockedPlacement:{} placed} && _lockAt>WorldState.CurrentTime)
        {
            // The helper's native cast has already fixed the knife position.
            // Move a short, validated distance off that point, then stand still
            // before the existing early sword-aim deadline.
            var inset=_postPlacementBait ?? placed+(Module.Center-placed).Normalized()*.8f;
            var visible=Module.FindComponent<XBMB45AOE>()?.ActiveAOEs(slot,actor).ToArray() ?? [];
            var route=placementKnife.TryValidatedEscape(actor.Position,inset,visible,hints,.1);
            if(route!=null)
            {
                var length=0f;var from=actor.Position;
                foreach(var p in route){length+=(p-from).Length();from=p;}
                if(WorldState.FutureTime(.1+length/6)<_lockAt)
                {
                    _postPlacementBait=inset;_baitPoint=inset;
                    hints.DirectRoute=route;hints.DirectRouteDeadline=_lockAt;
                }
            }
        }
        var point = _baitPoint.Value;
        // Keep attacking until the remaining walk plus navigation/latency margin
        // requires leaving. Once we depart, hold the chosen tip until helpers lock.
        var placementDeadline=Module.FindComponent<CrucibleLaudaFireKnife>()?.PendingPlacementDeadline ?? default;
        var deadline=placementDeadline > WorldState.CurrentTime && placementDeadline < _lockAt ? placementDeadline : _lockAt;
        _moving |= WorldState.FutureTime((actor.Position - point).Length() / 6 + 1) >= deadline;
        if (!_moving)
            return;
        hints.GoalPoints.Add((point, 0.2f, 100));
        hints.AddForbiddenConstraint(new SDInvertedCircle(point, 0.9f), deadline);
        hints.MaxCastTime = 0;
    }

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (_locked || tether.ID != 1 || source.OID is not (19742 or 19747) || WorldState.Actors.Find(tether.Target) is not { } target)
            return;
        if (CurrentBaits.Count == 0)
        {
            if (_prepareUntil <= WorldState.CurrentTime)
            {
                _baitPoint = null;
                _moving = false;
            }
            _lockAt = WorldState.FutureTime(4.5); // aim locks at +5.1s; reserve latency
            _locked = false;
        }
        CurrentBaits.RemoveAll(b => b.Source == source);
        CurrentBaits.Add(new(source, target, new AOEShapeRect(60, 3), WorldState.FutureTime(7.1)));
    }

    public override void OnUntethered(Actor source, in ActorTetherInfo tether)
    {
        if(!CurrentBaits.Any(b=>b.Source==source))return;
        CurrentBaits.RemoveAll(b=>b.Source==source);
        // Native cancellation announces the end of the aim before helper cast-start.
        if(CurrentBaits.Count==0)
        {
            _walkingUntil=WorldState.FutureTime(3);
            _locked=true;
            _prepareUntil=default;
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 49462 && !spell.EventHappened)
        {
            _baitPoint = null;
            _lastWalkingGoal=null;
            _innerPlacement=null;
            _postPlacementBait=null;
            _observedWalkingPosition=null;
            _observedWalkingAt=default;
            _walkingUntil=default;
            _moving = false;
            _locked = false;
            CurrentBaits.Clear();
            _prepareUntil = WorldState.FutureTime(16);
            // ARR: summon -> tethers ~9.6s -> aimed helpers ~5.1s later.
            _lockAt = WorldState.FutureTime(14);
        }
        else if (spell.Action.ID == 50846 && !spell.EventHappened && Module.CastFinishAt(spell) > WorldState.CurrentTime)
        {
            // The boss's aiming cast gives the actual deadline; its later
            // 50848 damage cast is already fixed and must be dodged.
            _lockAt = Module.CastFinishAt(spell, -0.5f);
            _prepareUntil = Module.CastFinishAt(spell, 3);
            _locked = false;
        }
        else if (spell.Action.ID is 49464 or 50848)
        {
            // The wave is now fixed. Stale tether/source snapshots must not
            // retain the axial goal while the short damage casts are ticking.
            _locked = true;
            _prepareUntil = default;
            CurrentBaits.Clear();
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 49462 or 50846 && spell.Interrupted)
        {
            _locked = true;
            _prepareUntil = default;
            CurrentBaits.Clear();
        }
    }

    public override void Update() => CurrentBaits.RemoveAll(b => b.Source.IsDeadOrDestroyed || b.Target.IsDeadOrDestroyed || b.Activation.AddSeconds(1) < WorldState.CurrentTime);

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (Preparing)
            hints.Add(_moving ? "三剑：到上下中央凸台保持引导，锁线读条结束后躲开，避免打中石像！" : "三剑：可继续攻击，锁线读条结束前到上下中央凸台！", false);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        base.DrawArenaForeground(pcSlot, pc);
        if (Preparing && _baitPoint is { } point)
            new AOEShapeCircle(0.9f).Outline(Arena, point, default, Colors.Safe);
    }
}

// run2313：冲击波把人击退到场外火地板（火伤 3065，每 3s 约 1900），寻路以场外为起点解不出来
// （NoReachableSafeCell），人在火里站了 26s 被烧死。人不在任何平台上时直接走回最近的平台点。
sealed class CrucibleLaudaFloorRescue(BossModule module) : BossComponent(module)
{
    // run2317：只给 DirectRoute 不够。寻路地图就是平台形状，人在场外时起点不在地图里（NoReachableSafeCell），
    // 直走路线也过不了校验，人在火里没动。照 B26 石像鬼电网的做法：临时把寻路地图放大到包含火地板，
    // 边距归零，按目标点规划。
    private static readonly ArenaBoundsRect FireFloor = new(45, 45);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (CrucibleLaudaFloor.Contains(actor.Position))
        {
            // 常驻组件顺带处理宠物站位（见 CrucibleLaudaPetCleave），省得另外登记组件。
            CrucibleLaudaPetCleave.Apply(Module, actor, hints);
            return;
        }
        var point = CrucibleLaudaFloor.Nearest(actor.Position);
        hints.PathfindMapBounds = FireFloor;
        hints.ArenaSafetyMargin = 0;
        hints.GoalDirectedTemporalPlanning = true;
        hints.GoalPoints.Add((point, 0.5f, 300));
        hints.DirectRoute = [point];
        hints.DirectRouteDeadline = WorldState.FutureTime((point - actor.Position).Length() / 6 + 0.5);
        hints.MaxCastTime = 0;
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (!CrucibleLaudaFloor.Contains(actor.Position))
            hints.Add("在场外火地板上：立刻回平台！");
    }
}

// run2276–2324（15 遍）：劳妲平A C6AC 是正面顺劈。仇恨在人物时，站在劳妲正面（相对面向 <90°）的宠物被同一下平A带到
// 76 次（角度 0–30° 47 次、30–60° 18 次、60–90° 10 次，距离中位 3.8m），每遍 1.2–4.9k；站在侧后（≥90°）的 128/165 次没挨。
// 2324-1：满血陷阱草 14.9–33.1s 被顺劈 6 下共 3957（4361→404），36.6s 残血收回，之后只剩 220 血的灯心蜻蛉。
// 劳妲全程仇恨在人物（PTL HoldCover），宠物挨的这些平A不替人物分担任何伤害，纯亏。
// 做法：仇恨在人物、劳妲没在读条、冲波诱导没在进行时，正面 <90°、离劳妲本体 ≤6y 的宠物移到劳妲身后。
// 待实测：PetMoveTo 移动后宠物是否照常攻击（冲波诱导已在用同一字段）。
static class CrucibleLaudaPetCleave
{
    private const float FrontCos = 0f;      // <90° 算正面
    private const float Reach = 6f;         // 劳妲 hitbox 外 6y 内
    private const float Behind = 2f;        // 站到 hitbox 外 2y

    public static void Apply(BossModule module, Actor actor, AIHints hints)
    {
        var boss = module.PrimaryActor;
        if (hints.PetMoveTo != null || boss.IsDeadOrDestroyed || boss.CastInfo != null || boss.TargetID != actor.InstanceID)
            return;
        if (module.FindComponent<CrucibleLaudaShockwaveBait>() is { Active: true })
            return;
        var facing = boss.Rotation.ToDirection();
        foreach (var pet in CrucibleNightmare.Pets(module.WorldState, actor))
        {
            var off = pet.Position - boss.Position;
            var dist = off.Length();
            if (dist < 0.01f || dist > boss.HitboxRadius + Reach || off.Dot(facing) / dist <= FrontCos)
                continue;
            var rear = boss.Position - facing * (boss.HitboxRadius + Behind);
            if (CrucibleLaudaFloor.Contains(rear))
                hints.PetMoveTo = rear;
            return;
        }
    }
}

sealed class CrucibleLaudaLaterHint(BossModule module) : Components.CastHints(module, [49477u], "灼焰战斧：持续全屏伤害，准备减伤与治疗！");

sealed class CrucibleLaudaAddRaidwide(BossModule module) : Components.CastHints(module, [50541u], "地狱之痛：全屏伤害，准备减伤！");

// 49484 消失魔刃·冲波（2.7s 读条）：读条结束玩家吃 止步＋ 5555（7s 定身），
// 劳妲 ~2s 后瞬移到玩家读条结束时的位置（271），再 ~0.8s 49487 打玩家
// （2000~2600 + 石化 2s + 5341 12s → 49489 冲击波 40y 击退，见 CrucibleLaudaShockwave）。
// run2209/2215/2249 每次都在场中挨这一下，之后 魔刃结界→火焰魔刃 出小怪。
// kano：剑范围引导到场边没打到就没有小怪。所以读条期间把人拉到离宠物最远、
// 读条内走得到的场边小平台，宠物拉到对面；落刀后正常回去（CrucibleLaudaShockwave 接管）。
sealed class CrucibleLaudaShockwaveBait(BossModule module) : BossComponent(module)
{
    private static readonly WPos[] Spots =
    [
        new(520, -443), new(520, -397), new(533, -427.5f), new(533, -417.5f), new(507, -422.5f), new(507, -412.5f)
    ];
    private DateTime _castEnd, _hitBy;
    private WPos? _spot;
    public bool Active => _hitBy != default && WorldState.CurrentTime <= _hitBy;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != 49484 || spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        _castEnd = Module.CastFinishAt(spell);
        _hitBy = _castEnd.AddSeconds(4);
        _spot = null;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 49487)
        {
            _hitBy = default;
            _spot = null;
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_hitBy == default || WorldState.CurrentTime > _hitBy)
            return;
        var pets = CrucibleNightmare.Pets(WorldState, actor);
        if (WorldState.CurrentTime < _castEnd)
        {
            // 读条剩余时间内走得到（6y/s，留 0.3s）的点里，离宠物/劳妲最远的；都走不到就选最近的。
            var left = (float)(_castEnd - WorldState.CurrentTime).TotalSeconds - 0.3f;
            float Away(WPos p) => pets.Length == 0 ? 0 : pets.Min(q => (q.Position - p).Length());
            var reachable = Spots.Where(p => (p - actor.Position).Length() <= 6 * left).ToArray();
            var spot = reachable.Length > 0
                ? reachable.OrderByDescending(p => Away(p) + (_spot == p ? 3 : 0)).First()
                : Spots.OrderBy(p => (p - actor.Position).LengthSq()).First();
            _spot = spot;
            hints.GoalPoints.Add((spot, 0.5f, 160));
            if ((spot - actor.Position).Length() > 1)
                hints.MaxCastTime = 0;
        }
        if (_spot is { } bait && pets.Length > 0)
        {
            // 宠物站对侧，离落刀点 ≥14y，留在主平台上。
            var away = CrucibleLaudaFloor.Center - (bait - CrucibleLaudaFloor.Center).Normalized() * 10;
            if (hints.PetMoveTo == null && CrucibleLaudaFloor.Contains(away))
                hints.PetMoveTo = away;
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_hitBy != default && WorldState.CurrentTime <= _hitBy && _spot != null)
            hints.Add("消失魔刃·冲波：去场边放剑，宠物站对面！");
    }
}
