using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// 19618 spawns ~5.8s before each R9 explosion. The cast alone is only 0.7s.
// Keep later waves visible without forbidding all nine circles simultaneously.
sealed class CrucibleSirenExplosions(BossModule module) : Components.GenericAOEs(module, 48577u)
{
    private readonly Dictionary<ulong, AOEInstance> _pending = [];
    private readonly HashSet<ulong> _resolved = [];
    private static readonly AOEShapeCircle Shape = new(9);

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == 19618 && !actor.IsDeadOrDestroyed && !_resolved.Contains(actor.InstanceID))
            _pending.TryAdd(actor.InstanceID, new(Shape, actor.Position, activation: WorldState.FutureTime(5.6), actorID: actor.InstanceID));
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && !spell.EventHappened && !_resolved.Contains(caster.InstanceID) && Module.CastFinishAt(spell) > WorldState.CurrentTime)
            _pending[caster.InstanceID] = new(Shape, spell.LocXZ, activation: Module.CastFinishAt(spell), actorID: caster.InstanceID);
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        foreach (var (id, aoe) in _pending.ToArray())
            if (aoe.Activation.AddSeconds(2) < WorldState.CurrentTime)
                _pending.Remove(id);
        var result = _pending.Values.OrderBy(a => a.Activation).ToArray();
        for (var i = 0; i < result.Length; ++i)
        {
            result[i].Risky = result[i].Activation <= result[0].Activation.AddSeconds(0.5);
            result[i].Color = result[i].Risky ? Colors.Danger : Colors.AOE;
        }
        return result;
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && !spell.EventHappened && spell.Interrupted)
            _pending.Remove(caster.InstanceID);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            _pending.Remove(caster.InstanceID);
            _resolved.Add(caster.InstanceID);
            ++NumCasts;
        }
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == 19617)
            _pending.Clear();
        else
            _pending.Remove(actor.InstanceID);
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}
