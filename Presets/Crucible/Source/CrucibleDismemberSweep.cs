namespace MasterPlayer.Mechanics.Crucible;

// B02 Dismember (46879): helpers drop 8y-wide, 35y-long strips one after another across the
// whole floor (23:04 ARR: x=536..504, hits 0.43/0.69/1.02/1.37s apart). The short dodge crosses
// the wave front once, next to the boss: pick the nearest strip boundary whose gap is long enough,
// wait on the side that fires later, and step across as soon as the earlier strip has fired.
// Navigation is staged so the planner never runs ahead of the sweep: before the crossing only the
// strips up to the earlier one are constraints; once it fires, every pending strip is.
sealed class CrucibleDismemberSweep(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    public const uint Dismember = 46879;
    private static readonly AOEShapeRect Strip = new(35f, 4f);
    // Exact strips do not receive the user's navigation margin; keep a small one of our own.
    private static readonly AOEShapeRect Guarded = new(35f, 4.4f);
    private const int MaxPredicted = 6;
    // Hit end margin (0.3s) + reduced start margin (0.2s) + the step and dispatch delay.
    private const double MinCrossingGap = 0.9;
    // If the earlier strip's effect is late, release the later strip this long before it hits.
    private const double LatestRelease = 0.8;
    private readonly List<(WPos Origin, Angle Rotation, DateTime Activation)> _seen = [];
    private (WPos Earlier, WPos Later)? _crossing;
    private ArenaBounds? _bounds;

    protected override AOEConfig? ConfigFor(uint actionID) => actionID == Dismember ? new(Strip, true) : null;

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var now = WorldState.CurrentTime;
        var pending = Pending;
        if (pending.Length == 0)
        {
            _seen.Clear();
            _crossing = null;
            return;
        }
        foreach (var entry in pending)
            if (!_seen.Any(s => s.Origin.AlmostEqual(entry.AOE.Origin, 0.5f)))
                _seen.Add((entry.AOE.Origin, entry.AOE.Rotation, entry.AOE.Activation));
        _seen.Sort((a, b) => a.Activation.CompareTo(b.Activation));
        _crossing ??= ChooseCrossing(actor);

        // Before the crossing, the later strip and everything after it are not constraints yet.
        DateTime? hideFrom = null;
        if (_crossing is { } crossing && Find(pending, crossing.Earlier) is { } earlier && Find(pending, crossing.Later) is { } later
            && now < later.Activation.AddSeconds(-LatestRelease))
            hideFrom = later.Activation;
        foreach (var entry in pending)
        {
            var aoe = entry.AOE;
            if (hideFrom is { } limit && aoe.Activation >= limit)
                continue;
            // A late effect keeps the strip dangerous; the end follows the clock in 0.1s steps.
            var end = aoe.Activation >= now ? aoe.Activation : now.AddTicks(TimeSpan.TicksPerSecond / 10 - now.Ticks % (TimeSpan.TicksPerSecond / 10));
            hints.ForbiddenZones.Add(new(Guarded.Distance(aoe.Origin, aoe.Rotation), aoe.Activation, end, ExactGeometry: true));
        }
    }

    private static AOEInstance? Find(ReadOnlySpan<PendingAOE> pending, WPos origin)
    {
        foreach (var entry in pending)
            if (entry.AOE.Origin.AlmostEqual(origin, 0.5f))
                return entry.AOE;
        return null;
    }

    // The sweep keeps its step and its gaps grow steadily; three casts give both.
    private (WPos, WPos)? ChooseCrossing(Actor actor)
    {
        if (_seen.Count < 3)
            return null;
        var last = _seen[^1]; var prev = _seen[^2];
        var step = last.Origin - prev.Origin;
        if (step.LengthSq() < 1)
            return null;
        var gap = (last.Activation - prev.Activation).TotalSeconds;
        var growth = Math.Max(0, gap - (prev.Activation - _seen[^3].Activation).TotalSeconds);
        var strips = _seen.Select(s => (s.Origin, s.Activation)).ToList();
        _bounds ??= CrucibleArena.Find(Module.Center)?.SafeBounds ?? Module.Arena.Bounds;
        var next = last.Origin; var activation = last.Activation;
        for (var i = 0; i < MaxPredicted; ++i)
        {
            next += step;
            if (!_bounds.Contains(next + last.Rotation.ToDirection() * (Strip.LengthFront / 2) - Module.Center))
                break;
            gap = Math.Max(0.4, gap + growth);
            activation = activation.AddSeconds(gap);
            strips.Add((next, activation));
        }

        var along = step.Normalized();
        (WPos, WPos)? best = null; var bestDistance = float.MaxValue;
        for (var i = 0; i + 1 < strips.Count; ++i)
        {
            var (a, b) = (strips[i], strips[i + 1]);
            if ((b.Activation - a.Activation).TotalSeconds < MinCrossingGap || a.Activation <= WorldState.CurrentTime)
                continue;
            var boundary = a.Origin + (b.Origin - a.Origin) * 0.5f;
            var distance = MathF.Abs((actor.Position - boundary).Dot(along));
            if (distance < bestDistance) { bestDistance = distance; best = (a.Origin, b.Origin); }
        }
        return best;
    }
}
