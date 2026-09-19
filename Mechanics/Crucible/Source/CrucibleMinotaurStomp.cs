namespace MasterPlayer.Mechanics.Crucible;

// ARR 2026-09-19 22:50:25: R5 landing hits pair with Knockback row 96 (20y).
// Check the next push from the previous landing: 3.462s is too short to defer
// all preparation until the first push has already sent the player to the wall.
sealed class CrucibleMinotaurStomp(BossModule module) : Components.GenericKnockback(module)
{
    private readonly Dictionary<ulong, Knockback> _pending = [];
    private Knockback[] _sources = [];
    private float[] _distances = [];
    private Components.GenericAOEs.AOEInstance[] _aoes = [];
    private Landing? _landing;
    private WPos _center;

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        foreach (var (id, source) in _pending.ToArray())
            if (source.Activation.AddSeconds(2) < WorldState.CurrentTime)
                _pending.Remove(id);
        return _pending.Count > 0 ? new[] { _pending.Values.MinBy(k => k.Activation) } : [];
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 48199 or 48202 && !spell.EventHappened && Module.CastFinishAt(spell) > WorldState.CurrentTime)
            _pending[caster.InstanceID] = new(spell.LocXZ, 20, Module.CastFinishAt(spell), actorID: caster.InstanceID);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 48199 or 48202 && spell.Interrupted)
            _pending.Remove(caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 48199 or 48202 && _pending.Remove(caster.InstanceID))
            ++NumCasts;
    }

    public override void OnActorDestroyed(Actor actor) => _pending.Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InRect(Module.Center, 19.5f, 19.5f);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (ActiveKnockbacks(slot, actor).Length == 0) return;
        var sources = _pending.Values.OrderBy(k => k.Activation).ToArray();
        var distances = sources.Select(k => IsImmune(slot, k.Activation) ? 0f : k.Distance).ToArray();
        if (distances.All(d => d == 0)) return;
        var aoes = Module.Components.OfType<Components.GenericAOEs>().SelectMany(c => c.ActiveAOEs(slot, actor).ToArray()).ToArray();
        if (_landing == null || _center != Module.Center || !_sources.SequenceEqual(sources) || !_distances.SequenceEqual(distances) || !_aoes.SequenceEqual(aoes))
        {
            _center = Module.Center; _sources = sources; _distances = distances; _aoes = aoes;
            var hazards = sources.Select(source => aoes
                .Where(a => a.Activation == default || a.Activation >= source.Activation.AddSeconds(-0.6) && a.Activation <= source.Activation.AddSeconds(0.8))
                .Select(a => a.ShapeDistance ?? a.Shape.Distance(a.Origin, a.Rotation)).ToArray()).ToArray();
            _landing = new(Module.Center, sources, distances, hazards);
        }
        hints.AddForbiddenConstraint(_landing, sources[0].Activation.AddSeconds(-0.4));
    }

    private sealed class Landing(WPos center, Knockback[] sources, float[] distances, ShapeDistance[][] hazards) : ShapeDistance
    {
        private FrozenConstraintGeometry? _geometry;
        public override PromeRotation.Spatial.Geometry.IGeometryShape Geometry(float y)
        {
            if (_geometry != null && _geometry.Position.Y == y) return _geometry;
            return _geometry = FrozenConstraintGeometry.Create(p => Distance(p), center, y, radius: 21);
        }

        public override float Distance(in WPos p)
        {
            var pos = p;
            var clearance = ArenaClearance(pos);
            for (var i = 0; i < sources.Length; ++i)
            {
                foreach (var hazard in hazards[i]) clearance = Math.Min(clearance, hazard.Distance(pos) - 0.5f);
                var delta = pos - sources[i].Origin;
                if (distances[i] > 0)
                {
                    if (delta.LengthSq() < 0.01f) return -1;
                    pos += delta.Normalized() * distances[i];
                }
                clearance = Math.Min(clearance, ArenaClearance(pos));
                foreach (var hazard in hazards[i]) clearance = Math.Min(clearance, hazard.Distance(pos) - 0.5f);
            }
            return clearance;
        }

        private float ArenaClearance(WPos p) => 19.5f - Math.Max(Math.Abs(p.X - center.X), Math.Abs(p.Z - center.Z));
    }

    public override void AddGlobalHints(GlobalHints hints)
    {
        if (ActiveKnockbacks(0, Module.PrimaryActor).Length > 0)
            hints.Add("十吨重踏：避开落点圆，依次调整两次20米击退，落点避开电网！");
    }
}
