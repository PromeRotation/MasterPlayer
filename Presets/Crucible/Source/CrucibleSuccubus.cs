namespace MasterPlayer.Mechanics.Crucible;

// Bloodsword precedes Mist in the recorded low1 sequence. Prepare the melee
// side facing the far corner while the boss is still stationary; the actual
// Mist cast remains responsible for escape timing and its forbidden circle.
sealed class CrucibleMistPreparation(BossModule module) : BossComponent(module)
{
    private DateTime _until;
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster != Module.PrimaryActor) return;
        if (spell.Action.ID == 46934) _until = WorldState.FutureTime(25);
        else if (spell.Action.ID == 46936) _until = default;
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == 46934 && spell.Interrupted) _until = default;
    }
    public override void OnActorDeath(Actor actor) { if (actor == Module.PrimaryActor) _until = default; }
    public override void OnActorDestroyed(Actor actor) { if (actor == Module.PrimaryActor) _until = default; }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var boss = Module.PrimaryActor;
        var mist = boss.CastInfo?.Action.ID == 46936;
        if ((!mist && WorldState.CurrentTime >= _until) || boss.IsDeadOrDestroyed || !boss.InCombat) return;
        var bounds = Arena.Bounds.Geometry(Module.Center, 0).GetBounds();
        WPos? corner = null;
        var farthest = 30.75f * 30.75f;
        foreach (var x in new[] { bounds.MinX + .7f, bounds.MaxX - .7f })
            foreach (var z in new[] { bounds.MinZ + .7f, bounds.MaxZ - .7f })
            {
                var p = new WPos(x, z);
                var distance = (p - boss.Position).LengthSq();
                if (Arena.InBounds(p) && distance > farthest) { corner = p; farthest = distance; }
            }
        if (corner is not { } escape) return; // No safe corner: don't manufacture one.
        if (mist)
        {
            // Only cross the expanded hit radius. Requiring the distant corner can
            // turn a reachable escape into a 40m timed route when the boss is near a wall.
            var direction = (escape - boss.Position).Normalized();
            // Keep a broad target when the field permits it. A 0.25m goal forces
            // the whole field onto a 0.125m grid even for an unobstructed escape.
            foreach (var radius in new[] { 1f, .5f, .25f })
            {
                var safeDistance = 30f + WorldState.NavigationMargin + radius + .05f;
                var landing = boss.Position + direction * safeDistance;
                if (landing.X - radius < bounds.MinX || landing.X + radius > bounds.MaxX
                    || landing.Z - radius < bounds.MinZ || landing.Z + radius > bounds.MaxZ) continue;
                hints.GoalPoints.Add((landing, radius, 100));
                break;
            }
            return;
        }
        var melee = Math.Clamp(boss.HitboxRadius + actor.HitboxRadius + 2.6f, 2.5f, 6.1f);
        var goal = boss.Position + (escape - boss.Position).Normalized() * melee;
        if (Arena.InBounds(goal)) hints.GoalPoints.Add((goal, .15f, 100));
    }
}

// run1763's two pheromone pairs converge at their spawn midpoint. The R24
// helper cast warns for only 0.7s, after ten seconds of Nightmare. Reserve
// the escape side as soon as both actors exist, without guessing a fuse.
sealed class CruciblePheromoneBurst(BossModule module) : Components.GenericAOEs(module, 46938u, "远离信息素汇合点！")
{
    private readonly HashSet<ulong> _resolved = [];
    private readonly Dictionary<ulong, WPos> _origins = [];
    private readonly HashSet<ulong> _casting = [];
    private ulong[] _pair = [];
    private WPos _center;

    public override void Update()
    {
        var live = Module.Enemies(19347u).Where(a => !a.IsDeadOrDestroyed && !_resolved.Contains(a.InstanceID)).ToArray();
        foreach (var actor in live) _origins.TryAdd(actor.InstanceID, actor.Position);
        if (_pair.Length == 2 && _pair.All(id => live.Any(a => a.InstanceID == id))) return;
        _pair = [];
        // Do not combine unrelated generations if a frame contains extra orbs.
        if (live.Length != 2) return;
        _pair = live.Select(a => a.InstanceID).ToArray();
        _center = _origins[_pair[0]] + (_origins[_pair[1]] - _origins[_pair[0]]) * .5f;
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _pair.Length == 2 && _casting.Count == 0
        ? new AOEInstance[] { new(CrucibleSpells.Table[46938].Shape, _center) } : [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction) _casting.Add(caster.InstanceID);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && spell.Interrupted) _casting.Remove(caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != WatchedAction) return;
        foreach (var id in _pair) _resolved.Add(id);
        _pair = [];
        _casting.Remove(caster.InstanceID);
        ++NumCasts;
    }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID != 19347u) return;
        _resolved.Remove(actor.InstanceID);
        _origins[actor.InstanceID] = actor.Position;
    }

    public override void OnActorDestroyed(Actor actor)
    {
        _resolved.Remove(actor.InstanceID);
        _origins.Remove(actor.InstanceID);
        _casting.Remove(actor.InstanceID);
        if (_pair.Contains(actor.InstanceID)) _pair = [];
    }
}
