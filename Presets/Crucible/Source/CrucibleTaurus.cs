using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// Normal second board, ARR 2026-09-19 14:57:58. Opposite aether lanes are 3s
// apart; double circle/donut hits are 3.5s apart. Future waves must not seal
// every currently safe tile. The helper cast location is the snapshot origin.
class CrucibleTaurusAOEs(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    protected override double RiskyActivationWindow => 0.5;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        48150 => new(new AOEShapeRect(50, 5), true),
        48155 or 48159 or 48165 => new(new AOEShapeCircle(8), true),
        48157 or 48161 or 48163 => new(new AOEShapeDonut(7, 50), true),
        _ => null
    };
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || spell.Interrupted)
            base.OnCastFinished(caster, spell);
    }
    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var aoes = ActiveAOEs(slot, actor);
        var explosions = Module.FindComponent<CrucibleTaurusExplosions>()?.ActiveAOEs(slot, actor).ToArray() ?? [];
        foreach (var aoe in aoes)
            if (explosions.Any(b => NeedsDonutStaging(aoe, b)))
                hints.ForbiddenZones.Add(new(aoe.Shape.Distance(aoe.Origin, aoe.Rotation),
                    aoe.Activation.AddSeconds(-0.25), aoe.Activation.AddSeconds(0.6), ExactGeometry: true));
            else
                CrucibleScorpionAI.Avoid(hints, aoe);
        if (aoes.Length > 1 && aoes[0].Shape is AOEShapeCircle && aoes[1].Shape is AOEShapeDonut)
        {
            // Prefer to wait near the first circle's edge, while respecting other
            // concurrent hazards. A hard staging ring could conflict with a bomb.
            var origin = aoes[0].Origin;
            hints.GoalZones.Add(p => Math.Clamp(12f - (p - origin).Length(), 0, 3));
        }
        foreach (var aoe in aoes)
        {
            if (!aoe.Risky || aoe.Shape is not AOEShapeDonut)
                continue;
            foreach (var bomb in explosions)
                if (NeedsDonutStaging(aoe, bomb))
                {
                    // Recorded late overlap: the R18 pulse follows the donut by
                    // only ~0.6s. Enter on the side furthest from the orb, ready
                    // to step out, rather than waiting deep inside both circles.
                    var center = aoe.Origin;
                    var direction = (center - bomb.Origin).Normalized();
                    // This precision transition keeps PR's 0.5y hard margin,
                    // without stacking two more margins over the real R7 pocket.
                    var goal = center + 6.3f * direction;
                    hints.GoalPoints.Add((goal, 0.2f, 100));
                }
        }
    }
    internal static bool NeedsDonutStaging(AOEInstance donut, AOEInstance bomb)
        => donut.Risky && donut.Shape is AOEShapeDonut ring && bomb.Shape is AOEShapeCircle circle
            && bomb.Activation > donut.Activation && bomb.Activation <= donut.Activation.AddSeconds(1.5)
            && (donut.Origin - bomb.Origin).Length() + ring.InnerRadius <= circle.Radius + 0.35f;
}

// Each dark orb pulses four times at R6, then four at R12, then four at R18.
// Spawn is 5.1-5.4s before the first hit; the individual casts warn for only
// 1.2s. Predict the next pulse immediately and reconcile it with every cast.
sealed class CrucibleTaurusExplosions(BossModule module) : Components.GenericAOEs(module)
{
    private sealed class Orb(WPos position, DateTime activation)
    {
        public WPos Position = position;
        public DateTime Activation = activation;
        public int Completed;
        public bool Staged;
        public DateTime? EscapeAt;
    }
    private static readonly AOEShapeCircle[] Shapes = [new(6), new(12), new(18)];
    private readonly Dictionary<ulong, Orb> _orbs = [];
    private readonly Dictionary<uint, DateTime> _events = [];
    private readonly HashSet<ulong> _finished = [];
    private readonly List<AOEInstance> _display = [];

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == 19541 && !actor.IsDeadOrDestroyed && !_finished.Contains(actor.InstanceID))
            _orbs.TryAdd(actor.InstanceID, new(actor.Position, WorldState.FutureTime(5.1)));
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is < 48146 or > 48148 || spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime || _finished.Contains(caster.InstanceID))
            return;
        if (!_orbs.TryGetValue(caster.InstanceID, out var orb))
            _orbs[caster.InstanceID] = orb = new(spell.LocXZ, Module.CastFinishAt(spell));
        orb.Position = spell.LocXZ;
        orb.Activation = Module.CastFinishAt(spell);
        orb.Completed = Math.Max(orb.Completed, 4 * (int)(spell.Action.ID - 48146));
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is < 48146 or > 48148 || _finished.Contains(caster.InstanceID)
            || spell.GlobalSequence != 0 && !_events.TryAdd(spell.GlobalSequence, WorldState.CurrentTime))
            return;
        ++NumCasts;
        if (!_orbs.TryGetValue(caster.InstanceID, out var orb))
            _orbs[caster.InstanceID] = orb = new(caster.Position, WorldState.CurrentTime);
        orb.Completed = Math.Max(orb.Completed, 4 * (int)(spell.Action.ID - 48146)) + 1;
        orb.Position = caster.Position;
        orb.Activation = WorldState.FutureTime(3.08);
        orb.Staged = false;
        orb.EscapeAt = null;
        if (orb.Completed >= 12)
        {
            _orbs.Remove(caster.InstanceID);
            _finished.Add(caster.InstanceID);
        }
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        foreach (var id in _orbs.Where(e => WorldState.CurrentTime > e.Value.Activation.AddSeconds(2)).Select(e => e.Key).ToArray())
            _orbs.Remove(id);
        foreach (var key in _events.Where(e => WorldState.CurrentTime > e.Value.AddSeconds(3)).Select(e => e.Key).ToArray())
            _events.Remove(key);
        _display.Clear();
        foreach (var (id, orb) in _orbs)
            _display.Add(new(Shapes[orb.Completed / 4], orb.Position, activation: orb.Activation, actorID: id));
        return CollectionsMarshal.AsSpan(_display);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var rings = Module.FindComponent<CrucibleTaurusAOEs>()?.ActiveAOEs(slot, actor).ToArray() ?? [];
        foreach (var aoe in ActiveAOEs(slot, actor))
        {
            var orb = _orbs[aoe.ActorID];
            if (rings.Any(r => CrucibleTaurusAOEs.NeedsDonutStaging(r, aoe)))
            {
                orb.Staged = true;
                continue;
            }
            if (orb.Staged)
            {
                // After the donut effect, evacuate immediately. Marking this as
                // already dangerous uses PR's exit-only escape behavior: stacked
                // early-start cushions cannot veto the recorded 0.7s transition.
                // The real pulse time still owns the preview and expiry.
                orb.EscapeAt ??= WorldState.CurrentTime;
                hints.AddForbiddenZone(new AOEShapeCircle(((AOEShapeCircle)aoe.Shape).Radius + 0.35f),
                    aoe.Origin, default, orb.EscapeAt.Value, aoe.Activation.AddSeconds(0.6));
            }
            else
                CrucibleScorpionAI.Avoid(hints, aoe);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is >= 48146 and <= 48148 && spell.Interrupted)
            _orbs.Remove(caster.InstanceID);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == 19540)
            _orbs.Clear();
        else
            _orbs.Remove(actor.InstanceID);
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// Icon 234 marks the player's dark orb. Its position snapshots AFTER 48145:
// the third recorded orb appears 1.2s later at the player's new position.
// Hold the selected edge until the orb is actually created, then dodge normally.
sealed class CrucibleTaurusDarkRay(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<ulong, (WPos Position, DateTime Expires)> _baits = [];
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID != 234)
            return;
        if (_baits.TryGetValue(actor.InstanceID, out var existing) && existing.Expires > WorldState.CurrentTime)
            return;
        var corners = new WPos[] { Module.Center + new WDir(-17, -12), Module.Center + new WDir(17, -12),
            Module.Center + new WDir(-17, 12), Module.Center + new WDir(17, 12) };
        var explosions = Module.FindComponent<CrucibleTaurusExplosions>()?.ActiveAOEs(0, actor).ToArray() ?? [];
        var safe = corners.Where(p => !explosions.Any(e => e.Check(p))).ToArray();
        var goal = (safe.Length > 0 ? safe : corners).OrderBy(p => (p - actor.Position).LengthSq()).First();
        _baits[actor.InstanceID] = (goal, WorldState.FutureTime(10));
    }
    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == 19541)
            _baits.Clear();
    }
    private bool Bait(Actor actor, out WPos position)
    {
        if (_baits.TryGetValue(actor.InstanceID, out var bait) && bait.Expires > WorldState.CurrentTime)
        {
            position = bait.Position;
            return true;
        }
        _baits.Remove(actor.InstanceID);
        position = default;
        return false;
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Bait(actor, out var p))
            hints.Add("将暗黑射线放在场边，等黑球出现再离开", !actor.Position.InCircle(p, 1));
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Bait(actor, out var p))
            hints.GoalPoints.Add((p, 0.3f, 100));
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (Bait(pc, out var p))
            Arena.ZoneCircleOutline(p, 1, Colors.Safe);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == 19540)
            _baits.Clear();
        else
            _baits.Remove(actor.InstanceID);
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// The north pair and south pair resolve 4s apart; only the imminent pair
// constrains facing. 48143 is an unavoidable raidwide, not this gaze.
sealed class CrucibleTaurusGaze(BossModule module) : Components.CastGaze(module, 48153u, range: 50)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction || spell.EventHappened)
            return;
        Eyes.RemoveAll(e => e.ActorID == caster.InstanceID);
        base.OnCastStarted(caster, spell);
    }
    public override ReadOnlySpan<Eye> ActiveEyes(int slot, Actor actor)
    {
        Eyes.RemoveAll(e => WorldState.CurrentTime > e.Activation.AddSeconds(1));
        return Eyes.Count == 0 ? [] : Eyes.Where(e => e.Activation <= Eyes.Min(x => x.Activation).AddSeconds(0.5)).ToArray();
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || spell.Interrupted)
            base.OnCastFinished(caster, spell);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            Eyes.RemoveAll(e => e.ActorID == caster.InstanceID);
            ++NumCasts;
        }
    }
    public override void OnActorDestroyed(Actor actor) => Eyes.RemoveAll(e => e.ActorID == actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

sealed class CrucibleTaurusStare(BossModule module) : Components.CastHints(module, [48372u], "凶眼注目：击杀小眼怪，减少全体伤害");
