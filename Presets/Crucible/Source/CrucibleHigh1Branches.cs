// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// Reuse event-object lifetimes, including already active pools on module load.
// Ice shares the R9 b0522 asset/placement used by The Ram's Keeper.
sealed class CrucibleIcePools(BossModule module) : Components.Voidzone(module, 9, GetPools)
{
    private static IEnumerable<Actor> GetPools(BossModule m) => m.Enemies(2004778).Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed);
}
sealed class CrucibleMorbolPools(BossModule module) : Components.Voidzone(module, 6, GetPools)
{
    private static IEnumerable<Actor> GetPools(BossModule m) => m.Enemies(2006848).Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed);
    private readonly Dictionary<ulong, (WPos Origin, DateTime Expires)> _acidCasts = [];
    private readonly List<(WPos Origin, DateTime Expires)> _anticipated = [];
    internal bool HasAnticipatedAcid => _anticipated.Count > 0;
    internal bool HasActiveAcidPool => GetPools(Module).Any();
    // 22/22 sealed 1826/1829/1832 acid effects create this fixed pool after 2.165-2.564s.
    // Cover only the observed effect-to-object gap; native object lifetime then owns it.
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48679 && !spell.EventHappened)
            _acidCasts[caster.InstanceID] = (spell.LocXZ, Module.CastFinishAt(spell).AddSeconds(2));
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48679 && spell.Interrupted)
            _acidCasts.Remove(caster.InstanceID);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48679 && _acidCasts.Remove(caster.InstanceID, out var acid))
        {
            _anticipated.RemoveAll(p => p.Origin.AlmostEqual(acid.Origin, 0.5f));
            if (!GetPools(Module).Any(p => p.Position.AlmostEqual(acid.Origin, 0.5f)))
                _anticipated.Add((acid.Origin, WorldState.FutureTime(4)));
        }
    }
    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == 2006848 && actor.EventState != 7)
            _anticipated.RemoveAll(p => p.Origin.AlmostEqual(actor.Position, 0.5f));
    }
    public override void Update()
    {
        if (Module.PrimaryActor.IsDeadOrDestroyed || WorldState.Player?.IsDeadOrDestroyed == true)
        { _acidCasts.Clear(); _anticipated.Clear(); return; }
        foreach (var key in _acidCasts.Where(p => p.Value.Expires < WorldState.CurrentTime).Select(p => p.Key).ToArray())
            _acidCasts.Remove(key);
        _anticipated.RemoveAll(p => p.Expires < WorldState.CurrentTime);
    }
    public override void OnActorDeath(Actor actor)
    {
        _acidCasts.Remove(actor.InstanceID);
        if (actor == WorldState.Player || actor == Module.PrimaryActor)
        { _acidCasts.Clear(); _anticipated.Clear(); }
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var pool in GetPools(Module))
            hints.TemporaryObstacles.Add(new SDCircle(pool.Position, 6.4f));
        foreach (var pool in _anticipated)
            hints.TemporaryObstacles.Add(new SDCircle(pool.Origin, 6.4f));
    }
}
sealed class CrucibleBombPools(BossModule module) : Components.Voidzone(module, 6, GetPools)
{
    private static IEnumerable<Actor> GetPools(BossModule m) => m.Enemies(2002331).Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed);
}

// The safe pad is selected by the chapter, not whichever object is nearest.
// Keep the player on the pad through impact: these transformation statuses are
// proximity effects and disappear again when the player leaves.
// Anthology (选集): eight books on a 13y ring burst in pairs (48665, R15), four waves 4s apart,
// each wave rotated 45 degrees from the last; each pair is known only ~6.5s ahead.
// run1897/1903: dodging one pair at a time ran into the next pair (the centre is inside every
// burst; a just-burst book's neighbour bursts next). Stand on the fence-side arc that clears
// every pending book: two consecutive waves always share such an arc, so the goal slides with them.
// run1887/1888 replays: a spot clear of the two known waves can be a dead end for the next one
// (the planner reached it, then needed ~20y in the last 4s). Plan the whole known sequence:
// one waypoint per wave, each clear of that wave and reachable from the previous waypoint before
// its burst; steer to the first waypoint. Bursts are instantaneous, so walking through an area
// between its neighbours' hits is fine and the solver's own windows keep the path itself safe.
static class CrucibleOwlSequence
{
    internal const float RunSpeed = 6f;
    internal const float Step = 1f;
    // Budget per gap between consecutive hits: PlanSpeed (below run speed: paths bend round
    // live areas) after a reaction allowance covering goal publish, solve delay and dispatch.
    internal readonly record struct Tuning(float PlanSpeed, double Reaction);
    // A new best waypoint must beat the current one by this much (score units, ~5% leg load);
    // run1888 0.2s flipped between two routes while walking and ended on neither.
    internal const float Hysteresis = 0.05f;
    // Damage lands ~0.27s after the cast bar (run1887/1893 cast finish vs effect). A wave already
    // past that cannot be dodged any more; planning around it pins the first leg (run1898 0.3s).
    internal const double ImpactDelay = 0.4;
    // Navigation keeps a landed area forbidden until ~0.85s after the cast bar (window radius
    // +0.85y). run1893 0.3s: a waypoint 5y inside the row that just fell could only be entered
    // with 1.15s left before the next row, and the planner gave up (NoTemporalPath).
    internal const double Linger = 0.9;
    internal const float WindowMargin = 0.85f;
    internal const float ClearanceMargin = 1f;

    internal readonly record struct Wave(DateTime Activation, WPos[] Origins);

    // run2031: the plan parked 8-12y from the owl for the whole 18s anthology although every wave has a
    // clear spot inside melee range, costing about 15% of the fight's GCDs. Among plans with slack in
    // every leg, take the one nearest melee range; the safety ordering above that is unchanged.
    internal readonly record struct Melee(WPos Boss, float Reach);
    internal const float SlackLoad = 0.8f, MeleeWeight = 0.05f, MeleeClearanceMargin = 0.6f;
    private static float OutOfMelee(WPos p, Melee? melee)
        => melee is { } m ? Math.Max(0, (p - m.Boss).Length() - m.Reach) : 0;

    internal sealed class Cache
    {
        internal string? GridKey;
        internal WPos[] Grid = [];
        internal readonly Dictionary<string, Costs?> Costs = [];
        internal WPos? Last;
        internal string? LastKey;
    }

    private static WPos[] Grid(WPos center, float radius, Melee? melee)
    {
        var points = new List<WPos>();
        for (var z = -radius; z <= radius; z += Step)
            for (var x = -radius; x <= radius; x += Step)
                if (x * x + z * z <= radius * radius)
                    points.Add(center + new WDir(x, z));
        // The 1y grid can miss the thin clear sliver inside melee range.
        if (melee is { } m)
            for (var ring = 0; ring < 2; ++ring)
                for (var angle = 0; angle < 360; angle += 15)
                {
                    var p = m.Boss + (m.Reach - ring) * ((float)angle).Degrees().ToDirection();
                    if ((p - center).LengthSq() <= radius * radius)
                        points.Add(p);
                }
        return [.. points];
    }

    internal static List<Wave> Group(IEnumerable<(WPos Origin, DateTime Activation)> hits)
    {
        var waves = new List<Wave>();
        foreach (var group in hits.OrderBy(h => h.Activation).Aggregate(new List<List<(WPos Origin, DateTime Activation)>>(), (acc, h) =>
        {
            if (acc.Count > 0 && (h.Activation - acc[^1][0].Activation).TotalSeconds < 0.75) acc[^1].Add(h); else acc.Add([h]);
            return acc;
        }))
            waves.Add(new(group[0].Activation, group.Select(g => g.Origin).ToArray()));
        return waves;
    }

    // How far a point sits inside a wave's lingering navigation window.
    private static float Depth(WPos p, Wave wave, float radius)
    {
        var depth = 0f;
        foreach (var o in wave.Origins)
            depth = Math.Max(depth, radius + WindowMargin - (p - o).Length());
        return depth;
    }

    private static float[] Safe(WPos[] grid, Wave wave, float clearance, float meleeClearance, Melee? melee)
    {
        var result = new float[grid.Length];
        for (var i = 0; i < grid.Length; ++i)
        {
            var needed = OutOfMelee(grid[i], melee) <= 0 && melee != null ? meleeClearance : clearance;
            result[i] = wave.Origins.All(o => (grid[i] - o).LengthSq() >= needed * needed) ? 0 : float.PositiveInfinity;
        }
        return result;
    }

    // Backward pass over grid waypoints, one per wave. Load[i] is the worst leg from waypoint i
    // onwards as a fraction of what the gap allows (<= 1 is feasible); Dist[i] the weighted walk.
    // run1888 0.15-0.4s: shortest plans ran legs at the speed limit and missed once paths bent
    // round live areas; minimise the worst leg first, distance only breaks ties.
    internal sealed record class Costs(float[] Load, float[] Dist);
    internal const float DistanceTieBreak = 0.01f;

    // Waves the chain cannot satisfy are dropped from the end, never from the front.
    private static Costs? Chain(WPos[] grid, List<Wave> waves, float radius, Tuning tuning, Melee? melee)
    {
        var clearance = radius + ClearanceMargin;
        var meleeClearance = radius + MeleeClearanceMargin;
        for (var count = waves.Count; count > 0; --count)
        {
            var load = Safe(grid, waves[count - 1], clearance, meleeClearance, melee);
            var dist = new float[grid.Length];
            for (var k = count - 2; k >= 0; --k)
            {
                var gap = (waves[k + 1].Activation - waves[k].Activation).TotalSeconds;
                var budget = tuning.PlanSpeed * (float)Math.Max(0, gap - tuning.Reaction);
                // A waypoint inside the area that just landed can only be entered once its window
                // lapses, so that whole leg has to fit in what is left of the gap.
                var lateBudget = tuning.PlanSpeed * (float)Math.Max(0, gap - Linger - tuning.Reaction);
                // run1898 0.3s: equal plans that postpone the move leave only the last gap for
                // it. Weight later legs so the chain moves as early as it can.
                var weight = 1.5f + 0.5f * k;
                var (nextLoad, nextDist) = (load, dist);
                var landed = waves[k];
                var reachable = Enumerable.Range(0, grid.Length).Where(j => !float.IsPositiveInfinity(nextLoad[j]))
                    .Select(j => (J: j, Limit: Depth(grid[j], landed, radius) > 0 ? lateBudget : budget)).Where(j => j.Limit > 0).ToArray();
                load = Safe(grid, waves[k], clearance, meleeClearance, melee);
                dist = new float[grid.Length];
                for (var i = 0; i < grid.Length; ++i)
                {
                    if (float.IsPositiveInfinity(load[i])) continue;
                    var (bestScore, bestLoad, bestDist) = (float.PositiveInfinity, float.PositiveInfinity, 0f);
                    foreach (var (j, limit) in reachable)
                    {
                        var dx = grid[j].X - grid[i].X;
                        var dz = grid[j].Z - grid[i].Z;
                        if (Math.Abs(dx) > limit || Math.Abs(dz) > limit) continue;
                        var d = MathF.Sqrt(dx * dx + dz * dz);
                        if (d > limit) continue;
                        var l = Math.Max(d / limit, nextLoad[j]);
                        var w = weight * d + nextDist[j] + OutOfMelee(grid[j], melee) * MeleeWeight / DistanceTieBreak;
                        if (l + DistanceTieBreak * w < bestScore)
                            (bestScore, bestLoad, bestDist) = (l + DistanceTieBreak * w, l, w);
                    }
                    (load[i], dist[i]) = (bestLoad, bestDist);
                }
            }
            if (load.Any(c => !float.IsPositiveInfinity(c)))
                return new(load, dist);
        }
        return null;
    }

    // Each scenario is one possible complete hit list (known casts plus a guess at the rest);
    // all share the known first wave. Prefer a first waypoint that keeps every scenario's chain
    // feasible, then the lightest worst leg over all scenarios.
    internal static WPos? Plan(Cache cache, Tuning tuning, WPos center, float navRadius, float radius, WPos player, DateTime now,
        IReadOnlyList<IEnumerable<(WPos Origin, DateTime Activation)>> scenarios, IEnumerable<(WPos Origin, DateTime Activation)> landed, Melee? melee = null)
    {
        // A boss that moved less than this keeps its grid, so a sway cannot churn the plan.
        const float MeleeSnap = 1f;
        if (melee is { } snap)
            melee = snap with { Boss = new(MathF.Round(snap.Boss.X / MeleeSnap) * MeleeSnap, MathF.Round(snap.Boss.Z / MeleeSnap) * MeleeSnap), Reach = snap.Reach - MeleeSnap / 2 };
        var gridKey = $"{center.X:F2},{center.Z:F2},{navRadius},{melee}";
        if (cache.GridKey != gridKey)
            (cache.GridKey, cache.Grid, cache.Last) = (gridKey, Grid(center, navRadius, melee), null);
        if (cache.Costs.Count > 64)
            cache.Costs.Clear();
        var grid = cache.Grid;
        var recent = Group(landed).Where(w => w.Activation.AddSeconds(Linger) > now).ToList();
        var plans = new List<(Wave First, Costs Cost)>();
        var planKey = "";
        foreach (var hits in scenarios)
        {
            var waves = Group(hits).Where(w => w.Activation.AddSeconds(ImpactDelay) >= now).ToList();
            if (waves.Count == 0) continue;
            var key = $"{gridKey}|{radius}|{tuning}|" + string.Join(';', waves.Select(w => $"{w.Activation.Ticks / TimeSpan.TicksPerMillisecond / 100}:" + string.Join(',', w.Origins.Select(o => $"{o.X:F1}/{o.Z:F1}"))));
            if (!cache.Costs.TryGetValue(key, out var cost))
                cache.Costs[key] = cost = Chain(grid, waves, radius, tuning, melee);
            if (cost != null)
                plans.Add((waves[0], cost));
            planKey += key + "#";
        }
        if (plans.Count == 0) return cache.Last = null;
        var first = plans[0].First;
        // The chain assumes the player stands on the first waypoint at the first hit, so it must be
        // reachable by then (run1888: a 30y-away waypoint whose cost ignored the walk lost the last pair).
        var firstTime = (float)Math.Max(0, (first.Activation - now).TotalSeconds - tuning.Reaction);
        var reach = Math.Max(Step, RunSpeed * firstTime);
        WPos? choice = null, fallback = null;
        (int Failed, float Score) best = (int.MaxValue, float.PositiveInfinity), kept = best;
        (int Failed, float Distance) fallbackKey = (int.MaxValue, float.PositiveInfinity);
        for (var i = 0; i < grid.Length; ++i)
        {
            var failed = 0;
            var (worstLoad, worstDist) = (0f, 0f);
            foreach (var (_, cost) in plans)
                if (float.IsPositiveInfinity(cost.Load[i])) ++failed;
                else (worstLoad, worstDist) = (Math.Max(worstLoad, cost.Load[i]), Math.Max(worstDist, cost.Dist[i]));
            if (failed == plans.Count) continue;
            var p = grid[i];
            var d = (p - player).Length();
            var ok = d <= reach
                && recent.All(r => Depth(p, r, radius) <= 0 || d <= tuning.PlanSpeed * (float)Math.Max(0, (first.Activation - r.Activation.AddSeconds(Linger)).TotalSeconds - tuning.Reaction));
            var score = (failed, Math.Max(SlackLoad, Math.Max(d / reach, worstLoad)) + DistanceTieBreak * (d + worstDist) + MeleeWeight * OutOfMelee(p, melee));
            if (ok && (score.failed < best.Failed || score.failed == best.Failed && score.Item2 < best.Score))
                (choice, best) = (p, score);
            if (ok && cache.Last is { } last && (p - last).LengthSq() < 0.01f)
                kept = score;
            if (failed < fallbackKey.Failed || failed == fallbackKey.Failed && d < fallbackKey.Distance)
                (fallback, fallbackKey) = (p, (failed, d));
        }
        if (cache.LastKey == planKey && kept.Failed == best.Failed && kept.Score <= best.Score + Hysteresis)
            choice = cache.Last;
        cache.LastKey = planKey;
        return cache.Last = choice ?? fallback;
    }
}

sealed class CrucibleOwlAnthology(BossModule module) : BossComponent(module)
{
    internal const uint Burst = 48665u;
    internal const int Priority = 95;
    internal const float NavRadius = 18.5f, Radius = 15f, GoalRadius = 0.75f;
    // Legs of 10-20y round R15 areas: allow for the detours and the planner's publish lag.
    internal static readonly CrucibleOwlSequence.Tuning Tuning = new(4.8f, 0.4);
    internal const double WaveGap = 4.03;
    private readonly Dictionary<ulong, (WPos Book, DateTime Activation)> _pending = [];
    // All eight 45-degree ring slots burst exactly once (run1885/1887/1888/1893/1900), a pair every
    // ~4s. Once two pairs have started the other four slots are known, only not their order:
    // run1888 parked after pair two at a spot that every ordering of the last two pairs dead-ended.
    private readonly HashSet<int> _slots = [];
    private float _ring;
    private readonly Dictionary<int,float> _slotRadii = [];
    private DateTime _lastCast;
    private readonly CrucibleOwlSequence.Cache _cache = new();
    private int Slot(WPos book) => (((int)MathF.Round(Angle.FromDirection(book - Module.Center).Deg / 45f)) % 8 + 8) % 8;
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != Burst) return;
        if (_lastCast.AddSeconds(20) < WorldState.CurrentTime) { _slots.Clear(); _slotRadii.Clear(); }
        _lastCast = WorldState.CurrentTime;
        _pending[caster.InstanceID] = (caster.Position, Module.CastFinishAt(spell));
        _slots.Add(Slot(caster.Position));
        _ring = (caster.Position - Module.Center).Length();
        // Native books use distinct cardinal/diagonal rings (run1991 13/12y).
        // Infer each from observed casts; never project the last book's radius
        // onto a different parity when planning the unannounced pairs.
        _slotRadii[Slot(caster.Position)%2]=_ring;
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == Burst && spell.Interrupted) _pending.Remove(caster.InstanceID);
    }
    private readonly List<(WPos Book, DateTime Activation)> _landed = [];
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == Burst && _pending.Remove(caster.InstanceID, out var book)) _landed.Add(book);
    }
    // Possible orders of the books not cast yet (one empty list when none or too many remain).
    internal List<List<(WPos Book, DateTime Activation)>> Unseen()
    {
        var free = Enumerable.Range(0, 8).Where(s => !_slots.Contains(s)).ToArray();
        if (_pending.Count == 0 || free.Length is not (2 or 4)) return [[]];
        var last = _pending.Values.Max(p => p.Activation);
        WPos At(int s) => Module.Center + _slotRadii.GetValueOrDefault(s%2,_ring) * (s * 45f).Degrees().ToDirection();
        if (free.Length == 2)
            return [[.. free.Select(s => (At(s), last.AddSeconds(WaveGap)))]];
        var orders = new List<List<(WPos, DateTime)>>();
        for (var a = 0; a < 4; ++a)
            for (var b = a + 1; b < 4; ++b)
                orders.Add([.. free.Select((s, i) => (At(s), last.AddSeconds(i == a || i == b ? WaveGap : 2 * WaveGap)))]);
        return orders;
    }
    internal WPos? Stand(WPos player)
    {
        foreach (var stale in _pending.Where(p => p.Value.Activation.AddSeconds(1.5) < WorldState.CurrentTime).Select(p => p.Key).ToArray())
            _pending.Remove(stale);
        _landed.RemoveAll(p => p.Activation.AddSeconds(CrucibleOwlSequence.Linger) < WorldState.CurrentTime);
        if (_pending.Count == 0) return null;
        var known = _pending.Values.ToList();
        return CrucibleOwlSequence.Plan(_cache, Tuning, Module.Center, NavRadius, Radius, player, WorldState.CurrentTime,
            [.. Unseen().Select(order => known.Concat(order))], _landed, new(Module.PrimaryActor.Position, Module.PrimaryActor.HitboxRadius + 3.5f));
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Stand(actor.Position) is { } spot)
            hints.GoalPoints.Add((spot, GoalRadius, Priority));
    }
}

// Falling (48656, R10): three diagonal rows of three, 2s apart (run1887 t17.8/19.8/21.8,
// run1893 t137.4/139.4/141.4). No spot clears two adjacent rows; run1893 0.3s waited deep in
// row two and needed ~9y in the 2s after row one. Wait on the edge of the next row instead,
// then step into the row that just landed (same sequence planner as the anthology).
sealed class CrucibleOwlFalling(BossModule module) : BossComponent(module)
{
    internal const uint Fall = 48656u;
    internal const int Priority = 95;
    internal const float NavRadius = 18.5f, Radius = 10f, GoalRadius = 0.5f;
    internal static readonly CrucibleOwlSequence.Tuning Tuning = new(4.8f, 0.4);
    private readonly Dictionary<ulong, (WPos Origin, DateTime Activation)> _pending = [];
    private readonly List<(WPos Origin, DateTime Activation)> _landed = [];
    private readonly CrucibleOwlSequence.Cache _cache = new();
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == Fall) _pending[caster.InstanceID] = (spell.LocXZ, Module.CastFinishAt(spell));
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == Fall && spell.Interrupted) _pending.Remove(caster.InstanceID);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == Fall && _pending.Remove(caster.InstanceID, out var fall)) _landed.Add(fall);
    }
    internal WPos? Stand(WPos player)
    {
        foreach (var stale in _pending.Where(p => p.Value.Activation.AddSeconds(1) < WorldState.CurrentTime).Select(p => p.Key).ToArray())
            _pending.Remove(stale);
        _landed.RemoveAll(p => p.Activation.AddSeconds(CrucibleOwlSequence.Linger) < WorldState.CurrentTime);
        if (_pending.Count == 0) return null;
        return CrucibleOwlSequence.Plan(_cache, Tuning, Module.Center, NavRadius, Radius, player, WorldState.CurrentTime, [_pending.Values], _landed, new(Module.PrimaryActor.Position, Module.PrimaryActor.HitboxRadius + 3.5f));
    }
    // run2036 (kano): the goal appeared with the 7s cast and the player left melee 6s before the first row for a 1s walk.
    private const double LeaveLead = 1.6;
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Stand(actor.Position) is not { } spot)
            return;
        var first = _pending.Values.Concat(_landed).Min(p => p.Activation);
        if ((first - WorldState.CurrentTime).TotalSeconds <= (spot - actor.Position).Length() / Tuning.PlanSpeed + LeaveLead)
            hints.GoalPoints.Add((spot, GoalRadius, Priority));
    }
}

sealed class CrucibleOwlChapters(BossModule module) : Components.GenericAOEs(module)
{
    private uint _padOID;
    private uint _status;
    private uint _action;
    private ulong _caster;
    private DateTime _showAt;
    private DateTime _activation;
    private static readonly AOEShapeCircle Spot = new(6);
    // 23:15 ARR: the quake pad was 19y away; 3s of guidance arrived 0.7s late. Start early enough to
    // walk to the nearest pad at run speed with 2s to spare.
    private const float RunSpeed = 6;
    private const double Spare = 2;
    private Actor[] Pads(Actor actor) => _padOID != 0 && _activation.AddSeconds(1) >= WorldState.CurrentTime
        && Module.Enemies(_padOID).Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed).ToArray() is { Length: > 0 } pads
        && WorldState.CurrentTime >= Earliest(pads.Min(p => (p.Position - actor.Position).Length()))
        ? pads : [];
    private DateTime Earliest(float distance)
    {
        var lead = _activation.AddSeconds(-(distance / RunSpeed + Spare));
        return lead < _showAt ? lead : _showAt;
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || spell.Action.ID is not (48657 or 48658 or 48659))
            return;
        (_padOID, _status) = spell.Action.ID switch { 48657 => (2004354u, 12u), 48658 => (2015457u, 1134u), _ => (2015456u, 1608u) };
        _action = spell.Action.ID;
        _caster = caster.InstanceID;
        // Show pads and enable guidance for the final 3s of the cast bar.
        // The NPC finish correction and hammer's impact delay only affect resolution.
        _showAt = WorldState.FutureTime(spell.RemainingTime - 3);
        _activation = Module.CastFinishAt(spell, spell.Action.ID == 48659 ? 0.8 : 0);
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => Pads(actor)
        .Select(p => new AOEInstance(Spot, p.Position, activation: _activation, color: Colors.SafeFromAOE, risky: false)).ToArray();
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Pads(actor).Length > 0)
            hints.Add(_action switch { 48657 => "进入浮空区域，等爆震判定后再离开！", 48658 => "进入河童区域，等狂水判定后再离开！", _ => "进入变身区域应对魔法锤！" }, actor.FindStatus(_status) == null);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Status gain does not resolve the chapter. Leaving the pad removes
        // Levitate and previously made AI alternate between the pad and boss.
        var pad = Pads(actor).MinBy(p => (p.Position - actor.Position).LengthSq());
        if (pad != null)
        {
            hints.AddForbiddenConstraint(new SDInvertedCircle(pad.Position, Spot.Radius - 0.5f), _activation.AddSeconds(-0.8));
            // The deadline alone lets the planner wait for its last moment; walk there now.
            hints.GoalPoints.Add((pad.Position, Spot.Radius - 2, 100));
        }
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (_action == 48659 ? 48660u : _action))
            _padOID = 0;
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == _action && spell.Interrupted)
            _padOID = 0;
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor.InstanceID == _caster)
            _padOID = 0;
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

// 48671/48672 reveal -/+45 degree rotation before the first breath. Four
// followups have only 0.2s casts, 2.1s apart. Reconcile each actual helper cast
// without re-arming resolved steps or forbidding all five cones at once.
// ActionEffect8 (including zero-target misses) confirms 48673/48675 damage.
// 48674 is only the rotation visual and must not advance the damage sequence.
sealed class CrucibleMorbolBreath(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone Shape = new(50, 45f.Degrees());
    private readonly List<AOEInstance> _sequence = [];
    private readonly HashSet<uint> _events = [];
    private ulong _boss;
    private DateTime _first;
    private Angle _step;
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
            return;
        var activation = Module.CastFinishAt(spell);
        if (spell.Action.ID is 48671 or 48672)
        {
            activation = activation.AddSeconds(0.5); // visual 4.2 + NPC finish 0.3 + helper delay 0.5 = 5s
            if (_boss == caster.InstanceID && Math.Abs((activation - _first).TotalSeconds) < 0.75)
                return;
            _boss = caster.InstanceID;
            _first = activation;
            _step = (spell.Action.ID == 48671 ? -45f : 45f).Degrees();
            _sequence.Clear();
            for (var i = 0; i < 5; ++i)
                _sequence.Add(new(Shape, spell.LocXZ, spell.Rotation + i * _step, activation.AddSeconds(i * 2.1)));
        }
        else if (spell.Action.ID is 48673 or 48675)
        {
            var index = _sequence.FindIndex(a => Math.Abs((a.Activation - activation).TotalSeconds) < 1);
            var actual = new AOEInstance(Shape, spell.LocXZ, spell.Rotation, activation);
            if (index >= 0)
                _sequence[index] = actual;
            else if (_sequence.Count == 0)
                _sequence.Add(actual); // actual cast still works on late loading
        }
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _sequence.RemoveAll(a => a.Activation.AddSeconds(1) < WorldState.CurrentTime);
        return _sequence.Select((a, i) => a with { Risky = i == 0, Color = i == 0 ? Colors.Danger : Colors.AOE }).ToArray();
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is not (48673 or 48675) || !_events.Add(spell.GlobalSequence))
            return;
        var index = _sequence.FindIndex(a => a.Activation <= WorldState.FutureTime(0.75) && a.Origin.AlmostEqual(caster.Position, 1));
        if (index >= 0)
            _sequence.RemoveAt(index);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Keep staged paths through rotating breaths and pools in the existing
        // planner. Every danger, radius, and timing margin stays unchanged.
        if (_sequence.Count > 0 || Module.FindComponent<HighCrucibleCastAOEs>()?.HasPendingMorbolAcid == true
            || Module.FindComponent<CrucibleMorbolPools>() is { } pools && (pools.HasAnticipatedAcid || pools.HasActiveAcidPool))
            hints.GoalDirectedTemporalPlanning = true;

        foreach (var aoe in ActiveAOEs(slot, actor))
            // 五连锥每 2.1 秒转 45°。原来只把“当前这一道”当危险区，AI 只能等这一道结算后才开始
            // 跑位；在 R11 上横移 45° 需要 8.6 米，慢速职业(4.2y/s)要 2.05 秒，加上延迟必然被下一道扫到。
            // 把 5 秒内会结算的锥（也就是下一道）一并交给寻路器，让它提前把两段一起规划。
            CrucibleScorpionAI.Avoid(hints, aoe);
        if (_sequence.Count > 1 && Module.FindComponent<HighCrucibleCastAOEs>()?.HasPendingMorbolAcid != true
            && Module.FindComponent<CrucibleMorbolPools>()?.HasAnticipatedAcid != true
            && Module.FindComponent<CrucibleMorbolPools>()?.HasActiveAcidPool != true)
            // While acid overlaps the breath, keep all damaging shapes/pools but allow outer escape.
            // At R11 a 45-degree change costs <9y; it is traversable between
            // breaths even with the R6 pool occupying the middle of the arena.
            hints.AddForbiddenConstraint(new SDInvertedCircle(_sequence[0].Origin, 11), _sequence[0].Activation.AddSeconds(-0.5), _sequence[0].Activation.AddSeconds(0.6));
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 48671 or 48672 && spell.Interrupted)
            _sequence.Clear();
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor.InstanceID == _boss)
            _sequence.Clear();
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

// Add targeting is left to the rotation. Only report the targeted missile;
// chasing autoattacks do not impose movement or range requirements.
sealed class CrucibleMorbolAdds(BossModule module) : BossComponent(module)
{
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Module.Enemies(19644).Any(a => !a.IsDeadOrDestroyed && a.IsTargetable
            && a.CastInfo is { Action.ID: 48680 } cast && cast.TargetID == actor.InstanceID))
            hints.Add("粘液飞弹是锁定点名，注意减伤！", true);
    }
}

sealed class CrucibleMorbolKnockback(BossModule module) : HighCrucibleKnockback(module, 50758u, 10)
{
    private Components.GenericAOEs.AOEInstance[] Pools(int slot, Actor actor) => Module.FindComponent<CrucibleMorbolPools>()?.ActiveAOEs(slot, actor).ToArray() ?? [];
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InCircle(Module.Center, 19.5f) || Pools(slot, actor).Any(a => a.Check(pos));
    private sealed class Landing(WPos center, Knockback source, Components.GenericAOEs.AOEInstance[] pools) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            var end = p + source.Distance * (p - source.Origin).Normalized();
            var clearance = 19.5f - (end - center).Length();
            foreach (var pool in pools)
                clearance = Math.Min(clearance, (end - pool.Origin).Length() - 6.5f);
            return clearance;
        }
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var kb in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, kb.Activation))
                hints.AddForbiddenConstraint(new Landing(Module.Center, kb, Pools(slot, actor)), kb.Activation.AddSeconds(-0.4));
    }
}

// The 48761 helper target is the receiving golem (a real actor), so its position
// predicts the receiving golem's circle/ring ~6s before impact. The helper's
// own position is stale. 48759 selects a ring; 48760 selects an R20 circle.
sealed class CrucibleGolemJump(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeDonut Ring = new(5, 50);
    private static readonly AOEShapeCircle Circle = new(20);
    private AOEInstance? _prediction;
    private DateTime _sequenceExpires;
    private AOEShape? _shape;
    private ulong _caster;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        48763 => new(Ring, true),
        48765 => new(Circle, true),
        _ => null
    };
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (!spell.EventHappened && spell.Action.ID is 48759 or 48760)
        {
            _sequenceExpires = Module.CastFinishAt(spell, 8);
            _shape = spell.Action.ID == 48759 ? Ring : Circle;
            _caster = caster.InstanceID;
            _prediction = null;
        }
        if (spell.Action.ID is 48763 or 48765)
            _prediction = null;
        base.OnCastStarted(caster, spell);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48761 && _sequenceExpires > WorldState.CurrentTime && _shape != null
            && WorldState.Actors.Find(spell.MainTargetID) is { IsDeadOrDestroyed: false } target)
            _prediction ??= new(_shape, target.Position, activation: WorldState.FutureTime(6), actorID: target.InstanceID);
        else if (spell.Action.ID is 48763 or 48765)
        {
            _prediction = null;
            _shape = null;
        }
        base.OnEventCast(caster, spell);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 48759 or 48760 && spell.Interrupted)
        {
            _prediction = null;
            _shape = null;
        }
        base.OnCastFinished(caster, spell);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        // After the handoff, the receiving golem owns the attack. Killing the
        // original caster must not erase the surviving receiver's warning.
        if (actor.InstanceID == (_prediction?.ActorID ?? _caster))
        {
            _prediction = null;
            _shape = null;
        }
        base.OnActorDestroyed(actor);
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_prediction is { } p && (p.Activation.AddSeconds(1) < WorldState.CurrentTime || WorldState.Actors.Find(p.ActorID) is not { IsDeadOrDestroyed: false }))
            _prediction = null;
        var actual = base.ActiveAOEs(slot, actor);
        return actual.Length > 0 ? actual : _prediction is { } prediction ? new[] { prediction } : [];
    }
    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

// User strategy: let the rotation kill the chasing bombs and accept their burst.
// Keep an informational outline, but never kite them or force add targeting.
sealed class CrucibleMovingBombs(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Small = new(6), Large = new(10);
    private readonly HashSet<ulong> _resolved = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => Module.Enemies(19668).Concat(Module.Enemies(19669))
        .Where(a => !a.IsDeadOrDestroyed && !_resolved.Contains(a.InstanceID))
        .Select(a => new AOEInstance(a.OID == 19668 ? Small : Large, a.Position, risky: false, actorID: a.InstanceID)).ToArray();
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 48792 or 48794)
            _resolved.Add(caster.InstanceID);
    }
    public override void OnActorDestroyed(Actor actor) => _resolved.Remove(actor.InstanceID);
    public override void OnActorCreated(Actor actor) => _resolved.Remove(actor.InstanceID);
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var aoe in ActiveAOEs(pcSlot, pc))
            aoe.Shape.Outline(Arena, aoe.Origin, aoe.Rotation, Colors.AOE);
    }
}

sealed class CrucibleBombMeltdown(BossModule module) : Components.GenericAOEs(module, 48801u)
{
    private static readonly AOEShapeRect Shape = new(40, 6);
    private AOEInstance? _aoe;
    private Actor? _boss;
    private Actor? _target;
    private DateTime _activation;
    private bool _locked;

    // ARR 15:46:32: icon 412 -> bind 48800 ~7.2s -> hit 48801 ~0.96s.
    // The marked player cannot dodge after the bind; prepare their landing early.
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == 412 && Module.Enemies(19666).FirstOrDefault(a => !a.IsDeadOrDestroyed) is { } boss)
        {
            _boss = boss;
            _target = actor;
            _activation = WorldState.FutureTime(8.2);
            _locked = false;
            _aoe = null;
        }
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48800)
        {
            _boss = caster;
            _target = WorldState.Actors.Find(spell.MainTargetID);
            _activation = WorldState.FutureTime(0.95);
            _locked = true;
            // Boss facing in the bind packet can lag behind the real target.
            var rotation = _target != null ? Angle.FromDirection(_target.Position - caster.Position) : spell.Rotation;
            _aoe = new(Shape, caster.Position, rotation, _activation);
        }
        else if (spell.Action.ID == WatchedAction)
            Clear();
    }
    private void Clear()
    {
        _aoe = null;
        _boss = _target = null;
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_boss == null || _boss.IsDeadOrDestroyed || _target is { IsDeadOrDestroyed: true } || _activation.AddSeconds(1) < WorldState.CurrentTime)
        {
            Clear();
            return [];
        }
        if (!_locked && _target != null)
            _aoe = new(Shape, _boss.Position, Angle.FromDirection(_target.Position - _boss.Position), _activation);
        return _aoe is { } aoe ? new[] { aoe with { Risky = actor != _target } } : [];
    }
    public Components.GenericKnockback.Knockback? KnockbackFor(int slot, Actor actor)
    {
        var aoes = ActiveAOEs(slot, actor);
        return aoes.Length > 0 ? new(aoes[0].Origin, 15, _activation,
            shape: actor == _target ? null : Shape, direction: aoes[0].Rotation) : null;
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            if (aoe.Risky)
                CrucibleScorpionAI.Avoid(hints, aoe);
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor == _boss || actor == _target)
            Clear();
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

sealed class CrucibleBombMeltdownKnockback(BossModule module) : Components.GenericKnockback(module)
{
    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
        => Module.FindComponent<CrucibleBombMeltdown>()?.KnockbackFor(slot, actor) is { } source ? new[] { source } : [];

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InCircle(Module.Center, 19.5f)
        || (Module.FindComponent<CrucibleBombPools>()?.ActiveAOEs(slot, actor).ToArray().Any(a => a.Check(pos)) ?? false);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var pools = Module.FindComponent<CrucibleBombPools>()?.ActiveAOEs(slot, actor).ToArray() ?? [];
        foreach (var kb in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, kb.Activation))
                // Prepare before 48800 binds the target, not only before damage.
                hints.AddForbiddenConstraint(new Landing(Module.Center, kb, pools), kb.Activation.AddSeconds(-1.5));
    }

    private sealed class Landing(WPos center, Knockback source, Components.GenericAOEs.AOEInstance[] pools) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            if (source.Shape != null && !source.Shape.Check(p, source.Origin, source.Direction))
                return 1;
            var landing = AwayFromSource(p, source.Origin, source.Distance);
            var clearance = Math.Min((p - source.Origin).Length() - 0.5f, 19.5f - (landing - center).Length());
            foreach (var pool in pools)
                clearance = Math.Min(clearance, (landing - pool.Origin).Length() - 6.35f);
            return clearance;
        }
    }
}
sealed class CrucibleBombEnrageHint(BossModule module) : Components.CastHints(module, [48790u, 48802u], "大爆炸：全场伤害，注意减伤！");
