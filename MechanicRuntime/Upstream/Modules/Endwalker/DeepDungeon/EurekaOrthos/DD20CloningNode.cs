namespace BossMod.Endwalker.DeepDungeon.EurekaOrthos.DD20CloningNode;

public enum OID : uint
{
    Boss = 0x3E77, // R6.0
    ClonedShieldDragon = 0x3E78, // R3.445
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 32817, // Boss->player, no cast, single-target
    Teleport = 32554, // ClonedShieldDragon->location, no cast, single-target

    OffensiveCommand = 32543, // Boss->self, 5.0s cast, single-target
    FlameBreathVisual1 = 32544, // ClonedShieldDragon->self, 2.0+1,5s cast, single-target
    FlameBreathVisual2 = 32553, // ClonedShieldDragon->self, no cast, single-target
    FlameBreathVisual3 = 32552, // ClonedShieldDragon->self, no cast, single-target
    FlameBreath = 32864, // Helper->self, 3.5s cast, range 50 30-degree cone
    PiercingLaser = 32547, // Boss->self, 2.5s cast, range 40 width 5 rect
    OrderRelay = 32545 // Boss->self, 8.0s cast, single-target
}

// Native self casts have no reliable target location. Keep danger until its effect,
// not the earlier IsCasting transition, and clear by source rather than list order.
class CloningNodeAOEs(BossModule module, uint aid, AOEShape shape) : Components.SimpleAOEs(module, aid, shape)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction)
            return;
        Remove(caster.InstanceID);
        var origin = caster.Position;
        Casters.Add(new(Shape, origin, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID,
            shapeDistance: Shape.Distance(origin, spell.Rotation)));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && spell.RemainingTime > 0.1f)
            Remove(caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == WatchedAction)
            Remove(caster.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor) => Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => Remove(actor.InstanceID);
    public override void Update() => Casters.RemoveAll(a => WorldState.CurrentTime > a.Activation.AddSeconds(0.6));
    protected void Remove(ulong id) => Casters.RemoveAll(a => a.ActorID == id);
}

sealed class FlameBreath(BossModule module) : CloningNodeAOEs(module, (uint)AID.FlameBreath, new AOEShapeCone(50f, 15f.Degrees()))
{
    private readonly Dictionary<ulong, DateTime> _cues = [];
    private readonly Dictionary<ulong, DateTime> _lastCue = [];
    private double _predictionDelay = 8.5;
    private int _offensiveCommands;
    private const float IntercardinalDistance = 22.627417f;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.OffensiveCommand or (uint)AID.OrderRelay)
        {
            // The opening commands, later commands and relay have different timelines.
            _predictionDelay = spell.Action.ID == (uint)AID.OrderRelay ? 11.4 : ++_offensiveCommands <= 2 ? 9.6 : 8.5;
            // ARR/live event delivery can put a cue just before its command cast.
            for (var i = 0; i < Casters.Count; ++i)
            {
                var a = Casters[i];
                if (_cues.TryGetValue(a.ActorID, out var at) && (WorldState.CurrentTime - at).TotalSeconds <= 0.2)
                {
                    a.Activation = at.AddSeconds(_predictionDelay);
                    Casters[i] = a;
                }
            }
        }
        else if (spell.Action.ID == WatchedAction)
        {
            // The helper is the authoritative inner-ring origin, cast facing and deadline.
            for (var i = Casters.Count - 1; i >= 0; --i)
            {
                var a = Casters[i];
                if (_cues.ContainsKey(a.ActorID) && a.Origin.AlmostEqual(caster.Position, 1f)
                    && a.Rotation.AlmostEqual(spell.Rotation, 0.05f))
                {
                    _cues.Remove(a.ActorID);
                    Casters.RemoveAt(i);
                }
            }
            base.OnCastStarted(caster, spell);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID != (uint)AID.FlameBreathVisual3)
            return;
        if (_lastCue.TryGetValue(caster.InstanceID, out var previous) && (WorldState.CurrentTime - previous).TotalSeconds < 0.5)
            return;
        _lastCue[caster.InstanceID] = _cues[caster.InstanceID] = WorldState.CurrentTime;
        Remove(caster.InstanceID);
        var origin = (caster.Position - Arena.Center).LengthSq() > 625f ? CalculatePosition(caster) : RoundPosition(caster.Position);
        Casters.Add(new(Shape, origin.Quantized(), caster.Rotation, WorldState.FutureTime(_predictionDelay), actorID: caster.InstanceID));
        // Expose both relay waves to time-aware navigation; the later wave stays delayed.
    }

    public override void Update()
    {
        base.Update();
        foreach (var (id, _) in _cues.ToArray())
            if (!Casters.Any(a => a.ActorID == id))
                _cues.Remove(id);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // FanTemplate's host margin extends radius only. Shift the navigation apex
        // backwards for 0.5y side clearance; the solver adds its own margin.
        // Effect packets precede cast+0.3 by up to 0.13s in ARR; allow 0.2s jitter.
        var shift = 0.5f / MathF.Sin(15f * Angle.DegToRad);
        var navigationCone = new AOEShapeCone(50f + shift, 15f.Degrees());
        foreach (ref readonly var a in ActiveAOEs(slot, actor))
            hints.AddForbiddenZone(navigationCone.Distance(a.Origin - shift * a.Rotation.ToDirection(), a.Rotation), a.Activation.AddSeconds(-0.2));
    }

    private static WPos CalculatePosition(Actor caster)
    {
        var isIntercardinal = IsCasterIntercardinal(caster);
        var distance = isIntercardinal ? IntercardinalDistance : 22f;
        var position = caster.Position + distance * caster.Rotation.ToDirection();
        var pos = RoundPosition(position);
        // the top left add is slightly off for some reason
        return pos == new WPos(-315f, -315f) ? new(-315.5f, -315.5f) : pos;

        static bool IsCasterIntercardinal(Actor caster)
        {
            for (var i = 0; i < 4; ++i)
            {
                if (caster.Rotation.AlmostEqual(Angle.AnglesIntercardinals[i], Angle.DegToRad))
                {
                    return true;
                }
            }
            return false;
        }
    }

    private static WPos RoundPosition(WPos position) => new(MathF.Round(position.X * 2f) * 0.5f, MathF.Round(position.Z * 2f) * 0.5f);
}

sealed class PiercingLaser(BossModule module) : CloningNodeAOEs(module, (uint)AID.PiercingLaser, new AOEShapeRect(40f, 2.5f));

sealed class DD20CloningNodeStates : StateMachineBuilder
{
    public DD20CloningNodeStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<FlameBreath>()
            .ActivateOnEnter<PiercingLaser>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Verified, Contributors = "The Combat Reborn Team (Malediktus)", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 898, NameID = 12261)]
public sealed class DD20CloningNode(WorldState ws, Actor primary) : BossModule(ws, primary, new(-300f, -300f), new ArenaBoundsCircle(19.5f));
