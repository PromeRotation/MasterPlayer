// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

sealed class CrucibleChimeraTether(BossModule module) : BossComponent(module)
{
    private Actor? _target;
    private Actor? _source;
    private WPos _edge;
    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (source.NameID != 14663 || tether.ID != 57 || WorldState.Actors.Find(tether.Target) is not { } target)
            return;
        if (_source == source && _target == target)
            return;
        _source = source;
        _target = target;
        var offset = target.Position - Module.Center;
        if (offset.LengthSq() < 1)
            offset = source.Position - Module.Center;
        if (offset.LengthSq() < 1)
            offset = new WDir(0, -1);
        _edge = Module.Center + offset.Normalized() * 18.5f;
    }
    public override void OnUntethered(Actor source, in ActorTetherInfo tether)
    { if (source == _source && tether.ID == 57) { _target = null; _source = null; } }
    public override void Update()
    {
        if (_target is { IsDeadOrDestroyed: true } || _source is { IsDeadOrDestroyed: true })
        { _target = null; _source = null; }
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    { if (_target == actor) hints.Add("连线：贴场边引导，等待冰圈扩大后躲开！", true); }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (_target is { IsDeadOrDestroyed: false } target)
        { Arena.AddLine(target.Position, _edge, target == pc ? Colors.SafeFromAOE : Colors.Object); Arena.Actor(target, target == pc ? Colors.SafeFromAOE : Colors.Object); }
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_target != actor || actor.IsDeadOrDestroyed) return;
        hints.GoalPoints.Add((_edge, 0.2f, 100));
        hints.MaxCastTime = 0;
    }
}

// 49320 is only the initial R6 hit. EObj 2015490 is the persistent ice:
// ARR: spawn -> ~5.8s -> EAnim 00100020 -> ~16s -> EState 4.
// b4171dmgf1_c1.avfx scales the expanding emitter 1 -> 3 over 360 frames
// (12s). Use R6 -> R18, with an extra navigation margin for the visual edge.
sealed class CrucibleChimeraExpandingIce(BossModule module) : Components.GenericAOEs(module)
{
    private const uint IceOID = 2015490;
    private sealed class Ice(Actor actor, DateTime growth)
    {
        public readonly Actor Actor = actor;
        public DateTime Growth = growth;
        public bool AnimationSeen;
    }
    private sealed record Drop(ulong Caster, WPos Origin, DateTime Activation, bool Hit = false, bool Visual = false);
    private readonly Dictionary<ulong, Ice> _ice = [];
    private readonly HashSet<ulong> _existing = module.Enemies(IceOID).Select(a => a.InstanceID).ToHashSet();
    private readonly List<Drop> _drops = [];
    private readonly List<AOEInstance> _aoes = [];
    private Actor? _marked;
    private DateTime _markExpires;

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == 669)
        {
            _marked = WorldState.Actors.Find(targetID) ?? actor;
            _markExpires = WorldState.FutureTime(9.7);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        if (spell.Action.ID == 49319 && _marked is { IsDeadOrDestroyed: false } target)
        {
            // Visual-cast fallback; the helper corrects this position/timing.
            if (!_drops.Any(d => d.Origin.InCircle(target.Position, 1)))
                _drops.Add(new(caster.InstanceID, target.Position, Module.CastFinishAt(spell, 0.5f), Visual: true));
        }
        else if (spell.Action.ID == 49320)
        {
            _drops.RemoveAll(d => d.Caster == caster.InstanceID || d.Visual && !d.Hit && d.Origin.InCircle(spell.LocXZ, 1)
                && Math.Abs((d.Activation - Module.CastFinishAt(spell)).TotalSeconds) < 1);
            _drops.Add(new(caster.InstanceID, spell.LocXZ, Module.CastFinishAt(spell)));
            _marked = null;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 49319 or 49320 && spell.Interrupted)
        {
            _drops.RemoveAll(d => d.Caster == caster.InstanceID && !d.Hit);
            _marked = null;
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != 49320)
            return;
        var i = _drops.FindIndex(d => d.Caster == caster.InstanceID);
        if (i >= 0)
            _drops[i] = _drops[i] with { Hit = true, Activation = WorldState.CurrentTime };
        // Keep the initial footprint until its ground object arrives (~0.4s).
        // Never clear another cast or an existing expanding pool here.
        _marked = null;
        ++NumCasts;
    }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID != IceOID || actor.IsDeadOrDestroyed || actor.EventState is 4 or 7 || _ice.ContainsKey(actor.InstanceID))
            return;
        // Reloading cannot recover an object's animation age: cover its full
        // possible size rather than restarting an old pool at R6.
        _ice.Add(actor.InstanceID, new(actor, WorldState.FutureTime(_existing.Contains(actor.InstanceID) ? -12 : 5.8)));
        _drops.RemoveAll(d => d.Origin.InCircle(actor.Position, 1));
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (state == 0x00100020 && _ice.TryGetValue(actor.InstanceID, out var ice) && !ice.AnimationSeen)
        {
            ice.AnimationSeen = true;
            // Do not shrink an already-expanded pool after a reload.
            if (!_existing.Contains(actor.InstanceID))
                ice.Growth = WorldState.CurrentTime;
        }
    }
    public override void OnActorEState(Actor actor, ushort state)
    {
        if (state is 4 or 7)
            _ice.Remove(actor.InstanceID);
    }
    public override void OnActorEventStateChange(Actor actor, byte state) => OnActorEState(actor, state);
    public override void OnActorDestroyed(Actor actor)
    {
        _ice.Remove(actor.InstanceID);
        _drops.RemoveAll(d => d.Caster == actor.InstanceID && !d.Hit);
        if (_marked == actor) _marked = null;
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void Update()
    {
        _drops.RemoveAll(d => d.Activation.AddSeconds(1.5) < WorldState.CurrentTime);
        foreach (var (id, ice) in _ice.ToArray())
            if (ice.Actor.IsDeadOrDestroyed || ice.Actor.EventState is 4 or 7)
                _ice.Remove(id);
        if (_marked is { IsDeadOrDestroyed: true } || _markExpires < WorldState.CurrentTime)
            _marked = null;
    }

    private static float Radius(Ice ice, DateTime time) => 6 + Math.Clamp((float)(time - ice.Growth).TotalSeconds, 0, 12);
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        if (_marked is { IsDeadOrDestroyed: false } target && _drops.Count == 0)
            _aoes.Add(new(new AOEShapeCircle(6), target.Position, activation: _markExpires, risky: target != actor));
        foreach (var drop in _drops)
            _aoes.Add(new(new AOEShapeCircle(6), drop.Origin, activation: drop.Activation, actorID: drop.Caster));
        foreach (var ice in _ice.Values)
            _aoes.Add(new(new AOEShapeCircle(Radius(ice, WorldState.CurrentTime)), ice.Actor.Position, actorID: ice.Actor.InstanceID));
        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_marked is { IsDeadOrDestroyed: false } target && target != actor && _drops.Count == 0)
            hints.AddForbiddenZone(new AOEShapeCircle(6), target.Position, default, _markExpires);
        foreach (var drop in _drops)
            hints.AddForbiddenZone(new AOEShapeCircle(6), drop.Origin, default, drop.Activation);
        foreach (var ice in _ice.Values)
        {
            // Only reserve the next second of growth. Far-future rings make the
            // pathfinder reject every intermediate position and run straight to R18.
            hints.AddForbiddenZone(new AOEShapeCircle(Radius(ice, WorldState.FutureTime(1)) + 0.5f), ice.Actor.Position);
        }
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var ice in _ice.Values)
            if (Radius(ice, WorldState.CurrentTime) < 18)
                new AOEShapeCircle(18).Outline(Arena, ice.Actor.Position, default, Colors.Danger);
    }
}
