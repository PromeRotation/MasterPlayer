namespace BossMod.Endwalker.DeepDungeon.EurekaOrthos.DD30TiamatsClone;

public enum OID : uint
{
    Boss = 0x3D9A, // R19.0
    DarkWanderer = 0x3D9B, // R2.0
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 32702, // Boss->player, no cast, single-target
    HeadAttack = 31842, // Helper->player, no cast, single-target

    CreatureOfDarkness = 31841, // Boss->self, 3.0s cast, single-target, summon Heads E<->W heading S
    DarkMegaflareVisual = 31849, // Boss->self, 3.0s cast, single-target
    DarkMegaflare = 31850, // Helper->location, 3.0s cast, range 6 circle
    DarkWyrmtailVisual = 31843, // Boss->self, 5.0s cast, single-target
    DarkWyrmtail = 31844, // Helper->self, 6.0s cast, range 40 width 16 rect, summon Heads Heading E/W from Middle Lane
    DarkWyrmwingVisual = 31845, // Boss->self, 5.0s cast, single-target
    DarkWyrmwing = 31846, // Helper->self, 6.0s cast, range 40 width 16 rect, summon Heads Heading E/W from E/W Walls
    WheiMornFirst = 31847, // Boss->location, 5.0s cast, range 6 circle
    WheiMornRest = 31848 // Boss->location, no cast, range 6 circle
}

public enum IconID : uint
{
    WheiMorn = 197 // player
}

// Self-targeted native casts carry no usable ground location. Ground casts do.
// The native cast flag drops ~0.3s before ActionEffect; retain by source until then.
class TiamatAOEs(BossModule module, uint aid, AOEShape shape, bool selfTargeted)
    : Components.SimpleAOEs(module, aid, shape)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction) return;
        Remove(caster.InstanceID);
        var origin = selfTargeted ? caster.Position : spell.LocXZ;
        Casters.Add(new(Shape, origin, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID));
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && spell.RemainingTime > 0.1f) Remove(caster.InstanceID);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == WatchedAction) Remove(caster.InstanceID);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in Casters)
            hints.TimedForbiddenZones.Add((aoe.Shape.Distance(aoe.Origin, aoe.Rotation), aoe.Activation,
                (aoe.Activation > WorldState.CurrentTime ? aoe.Activation : WorldState.CurrentTime).AddSeconds(0.6), aoe.ActorID, false));
    }
    public override void OnActorDestroyed(Actor actor) => Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => Remove(actor.InstanceID);
    public override void Update() => Casters.RemoveAll(a => WorldState.CurrentTime > a.Activation.AddSeconds(2));
    private void Remove(ulong id) => Casters.RemoveAll(a => a.ActorID == id);
}
class DarkMegaflare(BossModule module) : TiamatAOEs(module, (uint)AID.DarkMegaflare, new AOEShapeCircle(6f), false);
class DarkWyrmwing(BossModule module) : TiamatAOEs(module, (uint)AID.DarkWyrmwing, new AOEShapeRect(40f, 8f), true);
class DarkWyrmtail(BossModule module) : TiamatAOEs(module, (uint)AID.DarkWyrmtail, new AOEShapeRect(40f, 8f), true);

class WheiMorn(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [];
    private int _remaining;
    private WPos _previous;
    private DateTime _next;
    private ulong _source;
    private WPos? _destination;
    private static readonly AOEShapeCircle Circle = new(6);
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        if (_remaining > 0)
        {
            var offset = actor.Position - _previous;
            var distance = offset.Length();
            var origin = _remaining == 5 || distance <= 6 ? (_remaining == 5 ? _previous : actor.Position) : _previous + 6 * offset / distance;
            _aoes.Add(new(Circle, origin, activation: _next, actorID: _source));
        }
        return CollectionsMarshal.AsSpan(_aoes);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_remaining > 0)
        {
            hints.TimedForbiddenZones.Add((new SDCircle(_previous, _remaining == 5 ? 6 : 12.5f), _next, _next.AddSeconds(.8), _source, true));
            var required = 6 * (_remaining == 5 ? 4 : _remaining) + 9f;
            if (_destination == null)
            {
                WPos? best = null;
                var bestScore = float.MinValue;
                for (var x = -17f; x <= 17; x += 2)
                    for (var z = -17f; z <= 17; z += 2)
                    {
                        var candidate = Module.Center + new WDir(x, z);
                        var offset = candidate - actor.Position;
                        var clearance = (candidate - _previous).Length();
                        if (clearance < 14) continue;
                        var score = Math.Min(clearance, required) * 4 - offset.Length();
                        if (score <= bestScore) continue;
                        bestScore = score;
                        best = candidate;
                    }
                _destination = best;
            }
            if (_destination is { } destination && (_remaining < 5 || WorldState.CurrentTime >= _next.AddSeconds(-2.5)))
                hints.AddForbiddenZone(new SDInvertedCircle(destination, 1), source: _source);
        }
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.WheiMornFirst) return;
        _source = caster.InstanceID;
        _remaining = 5;
        _destination = null;
        _previous = spell.LocXZ;
        _next = Module.CastFinishAt(spell);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.WheiMornFirst && spell.RemainingTime > 0.1f) _remaining = 0;
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is not ((uint)AID.WheiMornFirst) and not ((uint)AID.WheiMornRest)) return;
        if (caster.InstanceID != _source || _remaining == 0) return;
        _previous = spell.TargetXZ;
        --_remaining;
        _next = WorldState.FutureTime(2);
    }
    public override void Update()
    {
        if (_remaining > 0 && (WorldState.CurrentTime > _next.AddSeconds(0.8) || Module.PrimaryActor.IsDead)) _remaining = 0;
    }
    public override void OnActorDestroyed(Actor actor) { if (actor.InstanceID == _source) _remaining = 0; }
    public override void OnActorDeath(Actor actor) { if (actor.InstanceID == _source) _remaining = 0; }
}

class CreatureOfDarkness(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [];
    private readonly Dictionary<ulong, (WPos Origin, Angle Rotation, DateTime Start)> _tracks = [];
    public override void Update()
    {
        foreach (var head in Module.Enemies((uint)OID.DarkWanderer))
        {
            if (head.IsDead || head.IsDestroyed || head.ModelState.AnimState1 != 1) _tracks.Remove(head.InstanceID);
            else if (!_tracks.TryGetValue(head.InstanceID, out var track)) _tracks[head.InstanceID] = (head.Position, head.Rotation, WorldState.FutureTime(1));
            else
            {
                var elapsed = Math.Max(0, (WorldState.CurrentTime - track.Start).TotalSeconds);
                var predicted = track.Origin + (3f * (float)elapsed) * track.Rotation.ToDirection();
                if (!head.Rotation.AlmostEqual(track.Rotation, 0.05f) || !predicted.AlmostEqual(head.Position, 0.9f))
                    _tracks[head.InstanceID] = (head.Position, head.Rotation, WorldState.CurrentTime);
            }
        }
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        foreach (var head in Module.Enemies((uint)OID.DarkWanderer))
            if (head.ModelState.AnimState1 == 1 && !head.IsDead)
                _aoes.Add(new(new AOEShapeCircle(2), head.Position, actorID: head.InstanceID));
        return CollectionsMarshal.AsSpan(_aoes);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        hints.PreferSafeRefuge = true;
        foreach (var (id, track) in _tracks)
        {
            for (var i = 0; i < 30; ++i)
            {
                var activation = track.Start.AddSeconds(i * 0.5 - 0.8);
                var expiry = track.Start.AddSeconds((i + 1) * 0.5 + 0.9);
                if (expiry < WorldState.CurrentTime) continue;
                var origin = track.Origin + (1.5f * i) * track.Rotation.ToDirection();
                hints.TimedForbiddenZones.Add((new SDCapsule(origin, track.Rotation, 1.5f, 2.5f), activation, expiry, id, false));
            }
        }
    }
    public override void OnActorModelStateChange(Actor actor, byte modelState, byte animState1, byte animState2)
    {
        if (actor.OID != (uint)OID.DarkWanderer) return;
        _tracks.Remove(actor.InstanceID);
        if (animState1 == 1) _tracks[actor.InstanceID] = (actor.Position, actor.Rotation, WorldState.FutureTime(1));
    }
    public override void OnActorDestroyed(Actor actor) => _tracks.Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => _tracks.Remove(actor.InstanceID);
}

class DD30TiamatsCloneStates : StateMachineBuilder
{
    public DD30TiamatsCloneStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<DarkWyrmwing>()
            .ActivateOnEnter<DarkWyrmtail>()
            .ActivateOnEnter<DarkMegaflare>()
            .ActivateOnEnter<WheiMorn>()
            .ActivateOnEnter<CreatureOfDarkness>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Verified, Contributors = "Malediktus", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 899, NameID = 12242)]
public class DD30TiamatsClone(WorldState ws, Actor primary) : BossModule(ws, primary, new(-300f, -300f), new ArenaBoundsSquare(19.5f));
