using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// ARR 2026-09-19 14:45:26: six staggered lanes, six R5 hits each, advancing
// 7y every ~1s. Only the first hit (46889) casts; 46890 reuses OTHER helpers.
// Match impacts spatially, never by the original caster or the zero effect location.
sealed class CrucibleVoidBlizzard(BossModule module) : Components.GenericAOEs(module)
{
    private sealed class Lane(ulong caster, WPos next, WDir step, DateTime activation)
    {
        public readonly ulong Caster = caster;
        public WPos Next = next;
        public readonly WDir Step = step;
        public DateTime Activation = activation;
        public bool Started;
    }
    private static readonly AOEShapeCircle Shape = new(5);
    private readonly List<Lane> _lanes = [];
    private readonly Dictionary<uint, DateTime> _events = [];
    private readonly List<AOEInstance> _display = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != 46889 || spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        var lane = _lanes.FirstOrDefault(l => l.Caster == caster.InstanceID && !l.Started && l.Next.AlmostEqual(spell.LocXZ, 0.5f));
        if (lane != null)
            lane.Activation = Module.CastFinishAt(spell);
        else if (!_lanes.Any(l => l.Caster == caster.InstanceID && l.Started))
            _lanes.Add(new(caster.InstanceID, spell.LocXZ, 7f * spell.Rotation.ToDirection(), Module.CastFinishAt(spell)));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is not (46889 or 46890) || spell.GlobalSequence != 0 && !_events.TryAdd(spell.GlobalSequence, WorldState.CurrentTime))
            return;
        ++NumCasts;
        var lane = _lanes.FirstOrDefault(l => l.Next.AlmostEqual(caster.Position, 1f));
        // Mid-mechanic loading: recover the remaining lane from a real impact.
        if (lane == null)
        {
            lane = new(caster.InstanceID, caster.Position, 7f * caster.Rotation.ToDirection(), WorldState.CurrentTime);
            _lanes.Add(lane);
        }
        lane.Started = true;
        lane.Next = caster.Position + lane.Step;
        lane.Activation = WorldState.FutureTime(1);
        if (!Inside(lane.Next))
            _lanes.Remove(lane);
    }

    private bool Inside(WPos p) => (CrucibleArena.Find(Module.Center)?.SafeBounds ?? Module.Arena.Bounds).Contains(p - Module.Center);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _lanes.RemoveAll(l => WorldState.CurrentTime > l.Activation.AddSeconds(1.8));
        foreach (var key in _events.Where(e => WorldState.CurrentTime > e.Value.AddSeconds(3)).Select(e => e.Key).ToArray())
            _events.Remove(key);
        _display.Clear();
        foreach (var lane in _lanes)
            for (var i = 0; i < 2 && Inside(lane.Next + i * lane.Step); ++i)
                _display.Add(new(Shape, lane.Next + i * lane.Step, activation: lane.Activation.AddSeconds(i), color: i == 0 ? Colors.Danger : Colors.AOE));
        return CollectionsMarshal.AsSpan(_display);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 46889 && spell.Interrupted)
            _lanes.RemoveAll(l => l.Caster == caster.InstanceID && !l.Started);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == 19338)
            _lanes.Clear();
        else
            _lanes.RemoveAll(l => l.Caster == actor.InstanceID && !l.Started);
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}
