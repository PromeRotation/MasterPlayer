namespace BossMod.Endwalker.DeepDungeon.EurekaOrthos.DD10Gancanagh;

public enum OID : uint
{
    Boss = 0x3D54, // R1.8
    PachypodiumMine = 0x3D55 // R1.5
}

public enum AID : uint
{
    Attack = 6499, // Boss->player, no cast, single-target

    AuthoritativeShriek = 31477, // Boss->self, 3.0s cast, single-target
    Mandrashock1 = 31478, // PachypodiumMine->self, 5.0s cast, range 10 circle
    Mandrashock2 = 32700, // PachypodiumMine->self, 8.0s cast, range 10 circle
    Mandrastorm = 31479 // Boss->self, 5.0s cast, range 60 circle, damage fall off AOE
}

class GancanaghAOEs(BossModule module, uint aid, float radius) : Components.SimpleAOEs(module, aid, radius)
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
        // Native IsCasting can clear before the NPC effect, whose delay is included in Activation.
        if (spell.Action.ID == WatchedAction && spell.RemainingTime > 0.1f)
            Remove(caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == WatchedAction)
            Remove(caster.InstanceID);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in Casters)
            hints.TimedForbiddenZones.Add((aoe.Shape.Distance(aoe.Origin, aoe.Rotation), aoe.Activation,
                aoe.Activation.AddSeconds(0.6), aoe.ActorID, false));
    }

    public override void OnActorDestroyed(Actor actor) => Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => Remove(actor.InstanceID);
    public override void Update() => Casters.RemoveAll(a => WorldState.CurrentTime > a.Activation.AddSeconds(0.6));
    private void Remove(ulong id) => Casters.RemoveAll(a => a.ActorID == id);
}

class MandraStorm(BossModule module) : GancanaghAOEs(module, (uint)AID.Mandrastorm, 20f);
class Mandrashock1(BossModule module) : GancanaghAOEs(module, (uint)AID.Mandrashock1, 10f);
class Mandrashock2(BossModule module) : GancanaghAOEs(module, (uint)AID.Mandrashock2, 10f);

class DD10GancanaghStates : StateMachineBuilder
{
    public DD10GancanaghStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<Mandrashock1>()
            .ActivateOnEnter<Mandrashock2>()
            .ActivateOnEnter<MandraStorm>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Verified, Contributors = "The Combat Reborn Team (Malediktus)", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 897, NameID = 12240)]
public class DD10Gancanagh(WorldState ws, Actor primary) : BossModule(ws, primary, new(-300f, -300f), new ArenaBoundsSquare(19.5f));
