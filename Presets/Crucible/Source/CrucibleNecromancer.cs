using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// ARR 2026-09-19 12:37:03: icon 707 -> 48181 -> ground-locked 48182.
// The target's R10 follows them only until the helper reveals the actual location.
sealed class CrucibleDeathDrive(BossModule module) : Components.GenericAOEs(module)
{
    private readonly Dictionary<ulong, DateTime> _targets = [];
    private readonly List<AOEInstance> _display = [];
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == 707) _targets.TryAdd(actor.InstanceID, WorldState.FutureTime(7.2));
    }
    public override void Update()
    {
        foreach (var (id, expiry) in _targets.ToArray())
            if (expiry.AddSeconds(1) < WorldState.CurrentTime || WorldState.Actors.Find(id) is not { IsDeadOrDestroyed: false }) _targets.Remove(id);
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48182) _targets.Clear();
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 48181 or 48182 && spell.Interrupted) _targets.Clear();
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48182) _targets.Clear();
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor) _targets.Clear();
        else _targets.Remove(actor.InstanceID);
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
    private static readonly AOEShapeCircle TargetShape = new(10);
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        Update();
        _display.Clear();
        foreach (var (id, expiry) in _targets)
            if (WorldState.Actors.Find(id) is { } target)
                _display.Add(new(TargetShape, target.Position, activation: expiry, risky: false, actorID: id));
        return CollectionsMarshal.AsSpan(_display);
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_targets.ContainsKey(actor.InstanceID)) hints.Add("死亡驱动点名：引导落点，圆圈固定后离开", false);
    }

    // run2003/1772: the R10 lands on the player's position when 48182 starts, and
    // wakes every corpse within 10 + its 0.75 radius. Recorded drives woke 3-7 zombies
    // where another reachable spot wakes 0-1. Each zombie outruns the player (9y/s)
    // and feeds the 12-stack confusion, so the walk is worth more than the GCDs.
    private const float WakeRadius = 10.75f, BaitGoalRadius = .5f, BaitSpeed = 5.2f, ZombieCost = 25, KeepAdvantage = 5;
    private readonly Dictionary<ulong, WPos> _firstSeen = [];
    private readonly HashSet<ulong> _awake = [];
    private WPos? _bait;
    private DateTime _baitComputed;
    // kano 11-08 run2181: the goal was published at icon 707, so the player walked to the
    // spot at once and idled until 48182 locked the R10 (~7.2s). Keep attacking and leave
    // only when the straight walk at BaitSpeed plus a solve buffer just fits, as DarkRay does.
    private const double BaitDepartBuffer = 0.8;
    private bool _departed;

    private WPos[] Corpses()
    {
        var corpses = new List<WPos>();
        foreach (var zombie in Module.Enemies(19549))
        {
            if (zombie.IsDestroyed) continue;
            _firstSeen.TryAdd(zombie.InstanceID, zombie.Position);
            if (zombie.IsDead)
                _awake.Remove(zombie.InstanceID);
            else if (!zombie.Position.AlmostEqual(_firstSeen[zombie.InstanceID], 0.5f))
                _awake.Add(zombie.InstanceID);
            // A dormant zombie has a full or zero HP bar depending on the record; only motion tells them apart.
            if (zombie.IsDead) _firstSeen[zombie.InstanceID] = zombie.Position;
            if (!_awake.Contains(zombie.InstanceID)) corpses.Add(zombie.Position);
        }
        return corpses.ToArray();
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!_targets.TryGetValue(actor.InstanceID, out var expiry) || actor.FindStatus(1283) != null || actor.FindStatus(1257) != null)
        {
            _bait = null;
            _departed = false;
            return;
        }
        if (_bait == null || WorldState.CurrentTime >= _baitComputed.AddSeconds(0.5))
        {
            _baitComputed = WorldState.CurrentTime;
            _bait = ChooseBait(slot, actor, expiry);
        }
        if (_bait is not { } bait)
            return;
        var remaining = (expiry - WorldState.CurrentTime).TotalSeconds;
        _departed |= remaining <= (bait - actor.Position).Length() / BaitSpeed + BaitDepartBuffer;
        if (_departed)
            hints.GoalPoints.Add((bait, BaitGoalRadius, 100));
    }

    private WPos? ChooseBait(int slot, Actor actor, DateTime expiry)
    {
        var corpses = Corpses();
        if (corpses.Length == 0) return null;
        var reach = BaitSpeed * MathF.Max(0, (float)(expiry - WorldState.CurrentTime).TotalSeconds);
        var hazards = Module.Components.OfType<Components.GenericAOEs>()
            .SelectMany(c => c.ActiveAOEs(slot, actor).ToArray())
            .Where(a => a.Risky && (a.Activation == default || a.Activation <= expiry.AddSeconds(4)))
            .Select(a => a.ShapeDistance ?? a.Shape.Distance(a.Origin, a.Rotation)).ToArray();
        float Score(WPos p)
        {
            // Every point inside the arrival disk can become the locked origin.
            var wakeReach = WakeRadius + BaitGoalRadius;
            var woken = corpses.Count(c => (c - p).LengthSq() <= wakeReach * wakeReach);
            return woken * ZombieCost + (p - actor.Position).Length();
        }
        WPos? best = null;
        var bestScore = float.MaxValue;
        for (var x = -20f; x <= 20f; x += 1f)
            for (var z = -20f; z <= 20f; z += 1f)
            {
                var p = Module.Center + new WDir(x, z);
                if ((p - actor.Position).Length() > reach || !InsideWithMargin(p) || hazards.Any(h => h.Distance(p) <= 1.5f))
                    continue;
                var score = Score(p);
                if (score < bestScore) { best = p; bestScore = score; }
            }
        // Hold the previous spot unless the new one is clearly better; re-aiming every tick only wastes the walk.
        if (_bait is { } previous && InsideWithMargin(previous) && (previous - actor.Position).Length() <= reach
            && !hazards.Any(h => h.Distance(previous) <= 1.5f) && Score(previous) <= bestScore + KeepAdvantage)
            return previous;
        return best;
    }

    private bool InsideWithMargin(WPos p)
        => Arena.InBounds(p + new WDir(1.5f, 0)) && Arena.InBounds(p - new WDir(1.5f, 0))
            && Arena.InBounds(p + new WDir(0, 1.5f)) && Arena.InBounds(p - new WDir(0, 1.5f));
}

// run2003 records forward/left orders, then 1257 for 3s at 6y/s.
// Active march already turns the actor; only pending orders add an offset.
sealed class CrucibleNecromancerMarch(BossModule module) : BossComponent(module)
{
    internal Angle? SafePendingFacing(int slot, Actor actor)
    {
        var now=WorldState.CurrentTime;
        // Confusion removes player control; issuing a safe-facing request during
        // it would pretend MP can steer a character that the game controls.
        if (actor.IsDeadOrDestroyed || Module.PrimaryActor.IsDeadOrDestroyed
            || actor.FindStatus(1257) is {ExpireAt: var activeUntil} && activeUntil>now
            || actor.FindStatus(1283) is {ExpireAt: var confusedUntil} && confusedUntil>now)
            return null;
        var pending=actor.FindStatus(2161) ?? actor.FindStatus(2162) ?? actor.FindStatus(2163) ?? actor.FindStatus(2164);
        if (pending is not {} order || order.ExpireAt==DateTime.MaxValue || order.ExpireAt<=now
            || order.ExpireAt>now.AddSeconds(1.2)) return null;
        var offset=order.ID switch {2162=>180f,2163=>90f,2164=>-90f,_=>0f};
        var hazards=Module.Components.OfType<Components.GenericAOEs>()
            .SelectMany(c=>c.ActiveAOEs(slot,actor).ToArray())
            .Where(a=>a.Risky && (a.Activation==default || a.Activation>=order.ExpireAt.AddSeconds(-.6)
                && a.Activation<=order.ExpireAt.AddSeconds(3.6))).ToArray();
        float Clearance(Angle facing)
        {
            var direction=(facing+offset.Degrees()).ToDirection();
            var clearance=100f;
            for(var i=0;i<=36;++i)
            {
                var p=actor.Position+direction*(i*.5f);
                if(!Arena.InBounds(p+new WDir(.5f,0)) || !Arena.InBounds(p-new WDir(.5f,0))
                    || !Arena.InBounds(p+new WDir(0,.5f)) || !Arena.InBounds(p-new WDir(0,.5f))) return -1;
                foreach(var hazard in hazards)
                    clearance=Math.Min(clearance,(hazard.ShapeDistance ?? hazard.Shape.Distance(hazard.Origin,hazard.Rotation)).Distance(p)-.5f);
            }
            return clearance;
        }
        // A currently safe route needs no turn. Otherwise choose the safe route
        // with the most clearance; sampled path includes the whole 18y march.
        if(Clearance(actor.Rotation)>.01f) return actor.Rotation;
        Angle? best=null;
        var score=.01f;
        for(var i=0;i<72;++i)
        {
            var angle=(i*5f).Degrees();
            var value=Clearance(angle);
            if(value>score){best=angle;score=value;}
        }
        return best;
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if(SafePendingFacing(slot,actor) is not {} facing) return;
        var order=actor.FindStatus(2161) ?? actor.FindStatus(2162) ?? actor.FindStatus(2163) ?? actor.FindStatus(2164);
        hints.DesiredFacing=facing;
        hints.DesiredFacingExpire=order!.Value.ExpireAt;
        hints.ForcedMarchImminent=true;
        hints.ForceCancelCast=true;
        hints.MaxCastTime=0;
    }
    internal (WPos End, bool Unsafe)? Projection(Actor actor)
    {
        var now = WorldState.CurrentTime;
        var active = actor.FindStatus(1257);
        var pending = actor.FindStatus(2161) ?? actor.FindStatus(2162)
            ?? actor.FindStatus(2163) ?? actor.FindStatus(2164);
        if (actor.IsDeadOrDestroyed || Module.PrimaryActor.IsDeadOrDestroyed) return null;
        var seconds = active is { ExpireAt: var end } && end > now && end != DateTime.MaxValue
            ? Math.Clamp((float)(end - now).TotalSeconds, 0, 3)
            : pending is { ExpireAt: var expiry } && expiry > now && expiry != DateTime.MaxValue ? 3 : 0;
        if (seconds <= 0) return null;
        var offset = active is { ExpireAt: var activeEnd } && activeEnd > now ? 0
            : pending?.ID switch { 2162 => 180, 2163 => 90, 2164 => -90, _ => 0 };
        var direction = (actor.Rotation + offset.Degrees()).ToDirection();
        var destination = actor.Position + direction * (6 * seconds);
        var hazards = Module.Components.OfType<Components.GenericAOEs>()
            .SelectMany(c => c.ActiveAOEs(0, actor).ToArray()).Where(a => a.Risky && a.Activation == default).ToArray();
        var steps = Math.Max(1, (int)Math.Ceiling(6 * seconds / 0.25));
        for (var i = 0; i <= steps; ++i)
        {
            var point = actor.Position + direction * (6 * seconds * i / steps);
            if (!Arena.InBounds(point) || hazards.Any(a => a.Check(point))) return (destination, true);
        }
        return (destination, false);
    }
    public override void DrawArenaForeground(int slot, Actor actor)
    {
        if (Projection(actor) is not { } projection) return;
        var direction = (projection.End - actor.Position).Normalized();
        var color = projection.Unsafe ? Colors.Danger : Colors.Safe;
        Arena.AddLine(actor.Position, projection.End, color);
        Arena.AddLine(projection.End, projection.End - direction * 1.5f + direction.OrthoR() * 0.8f, color);
        Arena.AddLine(projection.End, projection.End - direction * 1.5f - direction.OrthoR() * 0.8f, color);
        Arena.ZoneCircleOutline(projection.End, 0.6f, color);
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Projection(actor) is { } projection)
            hints.Add(projection.Unsafe ? "精神控制：调整面向，避开沿途瘴气和场外" : "精神控制：保持当前面向，等待强制移动", projection.Unsafe);
        if (actor.FindStatus(5424) is { Extra: var stacks } && stacks > 0)
            hints.Add($"渐渐混乱 {stacks}/12：优先击杀僵尸，避开瘴气", stacks >= 9);
    }
}
