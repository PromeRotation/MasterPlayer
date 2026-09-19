using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// 48827 is the R6 impact; 48805 is the accompanying 15y knockback (row 112).
// Three helpers cast 2s apart. The latter's R40 reach is not a full-arena AOE.
sealed class CrucibleBombFuryAOEs(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    protected override double RiskyActivationWindow => 0.5;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID == 48827 ? new(new AOEShapeCircle(6), true) : null;
    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            // Future positions are reached by knockback, not walking. The
            // sequence constraint validates them; PR's walking planner must
            // only treat the current wave as a damage zone.
            if (aoe.Risky)
                CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

sealed class CrucibleBombFuryKnockback(BossModule module) : HighCrucibleKnockback(module, 48805u, 15)
{
    private Knockback[] _sources = [];
    private float[] _distances = [];
    private WPos[] _poolPositions = [];
    private Landings? _landings;
    private float _margin;
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        Casters.Sort((a, b) => a.Activation.CompareTo(b.Activation));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        // The normal cast end can precede the effect packet; release on impact.
        if (spell.Interrupted)
            base.OnCastFinished(caster, spell);
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InCircle(Module.Center, 19.5f)
        || (Module.FindComponent<CrucibleBombPools>()?.ActiveAOEs(slot, actor).ToArray().Any(a => a.Check(pos)) ?? false);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var sources = ActiveKnockbacks(slot, actor).ToArray();
        if (sources.Length == 0)
            return;
        var displacements = sources.Select(kb => IsImmune(slot, kb.Activation) ? 0 : kb.Distance).ToArray();
        var pools = Module.FindComponent<CrucibleBombPools>()?.ActiveAOEs(slot, actor).ToArray() ?? [];
        // Solve the sequence from each candidate's actual previous landing, not
        // three independent knockbacks from the player's initial position.
        var poolPositions = pools.Select(p => p.Origin).ToArray();
        var margin = Math.Max(0, WorldState.NavigationMargin);
        if (_landings == null || _margin != margin || !_sources.SequenceEqual(sources) || !_distances.SequenceEqual(displacements) || !_poolPositions.SequenceEqual(poolPositions))
        {
            _margin = margin;
            _sources = sources; _distances = displacements; _poolPositions = poolPositions;
            _landings = new(Module.Center, sources, displacements, pools, margin);
        }
        hints.AddForbiddenConstraint(_landings, sources[0].Activation.AddSeconds(-0.5));
        if (_landings.StagingPoint(actor.Position) is { } point)
            hints.GoalPoints.Add((point, 0.2f, 100));
    }

    private sealed class Landings(WPos center, Knockback[] sources, float[] distances, Components.GenericAOEs.AOEInstance[] pools, float margin) : ShapeDistance
    {
        private FrozenConstraintGeometry? _geometry;
        private readonly List<(WPos Position, float Clearance)> _candidates = [];
        private float _bestClearance;
        private bool _candidatesBuilt;
        public WPos? StagingPoint(WPos current)
        {
            EnsureCandidates();
            // Keep a good landing instead of crossing the arena for a different
            // globally optimal point during the two-second knockback interval.
            var required = Math.Min(0.8f, _bestClearance * 0.8f);
            if (required > 0 && GoalClearance(current) >= required) return current;
            return _candidates.Where(p => p.Clearance >= required).OrderBy(p => (p.Position - current).LengthSq())
                .Select(p => (WPos?)p.Position).FirstOrDefault();
        }
        public override PromeRotation.Spatial.Geometry.IGeometryShape Geometry(float y)
        {
            if (_geometry != null && _geometry.Position.Y == y) return _geometry;
            // Three chained landings leave a narrow valid starting pocket.
            // The ordinary 0.5y conservative snapshot can erase it completely.
            // Cache a finer immutable snapshot only when sources/pools change.
            return _geometry = FrozenConstraintGeometry.Create(p => Distance(p), center, y, radius: 21, step: 0.1f);
        }
        private void EnsureCandidates()
        {
            if (_candidatesBuilt) return;
            _candidatesBuilt = true;
            for (var z = -19.5f; z <= 19.5f; z += 0.25f)
                for (var x = -19.5f; x <= 19.5f; x += 0.25f)
                {
                    var p = center + new WDir(x,z);
                    var clearance = GoalClearance(p);
                    if (clearance > 0)
                    {
                        _candidates.Add((p, clearance));
                        _bestClearance = Math.Max(_bestClearance, clearance);
                    }
                }
        }
        private float GoalClearance(WPos p)
        {
            // A good chain landing is insufficient if its starting point lies
            // in an existing pool or the inflated current explosion.
            var clearance = Math.Min(Distance(p), (p - sources[0].Origin).Length() - 6.35f - margin - 0.5f);
            foreach (var pool in pools)
                clearance = Math.Min(clearance, (p - pool.Origin).Length() - 6 - margin - 0.5f);
            return clearance;
        }
        public override float Distance(in WPos p)
        {
            var pos = p;
            var clearance = 19.5f - (pos - center).Length();
            for (var i = 0; i < sources.Length; ++i)
            {
                var offset = pos - sources[i].Origin;
                var length = offset.Length();
                clearance = Math.Min(clearance, length - 6.35f);
                if (length < 0.1f)
                    return -1;
                pos += offset * (distances[i] / length);
                clearance = Math.Min(clearance, 19.5f - (pos - center).Length());
                foreach (var pool in pools)
                    clearance = Math.Min(clearance, (pos - pool.Origin).Length() - 6.35f);
            }
            return clearance;
        }
    }
}
