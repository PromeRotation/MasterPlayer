// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// R40 explosions cover the giant's arena. They are damage checks, not safe-position puzzles.
sealed class CrucibleGiantExplosionHint(BossModule module) : Components.CastHints(module, [49373u, 49374u, 49375u, 49376u], "凝胶炸裂：全屏伤害，准备减伤！");

// ARR 22:48:06: timeline 4562 precedes the hit by 15s, 4576 by 8s.
// Four waves are 2s apart. The final helper cast alone gives only 1s of warning.
sealed class CrucibleLaudaBlades(BossModule module) : Components.GenericAOEs(module, 49483u)
{
    private static readonly AOEShapeRect Shape = new(50, 2.5f);
    private readonly List<AOEInstance> _pending = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _pending.RemoveAll(a => a.Activation.AddSeconds(2) < WorldState.CurrentTime);
        _pending.Sort((a, b) => a.Activation.CompareTo(b.Activation));
        // Color remains time-graded; PR receives all windows through AddAIHints.
        var horizon = _pending.Count > 0 ? _pending[0].Activation.AddSeconds(2.2) : default;
        var entries = CollectionsMarshal.AsSpan(_pending);
        for (var i = 0; i < entries.Length; ++i)
        {
            entries[i].Risky = entries[i].Activation <= horizon;
            entries[i].Color = entries[i].Activation <= horizon.AddSeconds(-1.7) ? Colors.Danger : Colors.AOE;
        }
        return entries;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            hints.AddForbiddenZone(aoe.Shape, aoe.Origin, aoe.Rotation, aoe.Activation);
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
            ++NumCasts;
            _pending.RemoveAll(a => a.ActorID == caster.InstanceID || a.Origin.AlmostEqual(caster.Position, 0.25f));
        }
    }

    public override void OnActorDestroyed(Actor actor) => _pending.RemoveAll(a => a.ActorID == actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
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
            return _geometry = FrozenConstraintGeometry.Create(p => Distance(p), CrucibleLaudaFloor.Center, y, radius: 26);
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (ActiveKnockbacks(slot, actor).Length != 0 && !IsImmune(slot, _activation))
        {
            hints.AddForbiddenConstraint(_landing ??= new(_rotation), _activation.AddSeconds(-0.5));
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
    public bool Preparing => !_locked && (_prepareUntil > WorldState.CurrentTime || CurrentBaits.Count > 0);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (!Preparing || (CurrentBaits.Count > 0 && !IsBaitTarget(actor)))
            return;
        // Choose once per wave. Only the two axial tips avoid the side statues.
        _baitPoint ??= Module.Center + new WDir(0, actor.Position.Z < Module.Center.Z ? -22.5f : 22.5f);
        var point = _baitPoint.Value;
        // Keep attacking until the remaining walk plus navigation/latency margin
        // requires leaving. Once we depart, hold the chosen tip until helpers lock.
        _moving |= WorldState.FutureTime((actor.Position - point).Length() / 6 + 1) >= _lockAt;
        if (!_moving)
            return;
        hints.GoalPoints.Add((point, 0.2f, 100));
        hints.AddForbiddenConstraint(new SDInvertedCircle(point, 0.9f), _lockAt);
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

    public override void OnUntethered(Actor source, in ActorTetherInfo tether) => CurrentBaits.RemoveAll(b => b.Source == source);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 49462 && !spell.EventHappened)
        {
            _baitPoint = null;
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

sealed class CrucibleLaudaLaterHint(BossModule module) : Components.CastHints(module, [49477u], "灼焰战斧：持续全屏伤害，准备减伤与治疗！");

sealed class CrucibleLaudaAddRaidwide(BossModule module) : Components.CastHints(module, [50541u], "地狱之痛：全屏伤害，准备减伤！");
