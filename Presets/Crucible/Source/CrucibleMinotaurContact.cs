namespace MasterPlayer.Mechanics.Crucible;

// 11-10 低2 L2a：无尽回转 48207 在 12 次双牛无尽阶段里打中人物 19 下（每下 730–870，占无尽阶段人物承伤 69%），
// 命中时人物离追击那只牛中心 6.1–8.6 米（run2139/2186/2197/2213/2225/2229/2233）。R8 正好贴边，
// 位置更新是离散的（见 KeepMotionBetweenPositionUpdates），留 1.5 米余量：禁区 9.5，站位点至少 12.5 米。
sealed class CrucibleMinotaurContact(BossModule module) : CrucibleMovingContact(module, 9.5f, 12, [19551, 19552])
{
    private readonly Dictionary<ulong, (DateTime From, DateTime Until)> _active = [];
    private WPos? _standoff;
    protected override bool KeepMotionBetweenPositionUpdates => true;
    // A navigation forecast from recent measured velocity, not a claim that
    // the enemy keeps its speed/direction for the next two seconds.
    protected override float MotionLookAheadSeconds => 2f;
    public override IEnumerable<Actor> Sources => base.Sources.Where(a =>
        _active.TryGetValue(a.InstanceID, out var window) && window.From <= WorldState.CurrentTime && window.Until >= WorldState.CurrentTime);

    private Actor? Body(Actor source)
    {
        var bodies = base.Sources.Where(a => a.NameID == source.NameID).Take(2).ToArray();
        return bodies.Length == 1 ? bodies[0] : null;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (Body(caster) is not { } body)
            return;
        if (spell.Action.ID == 48206)
        {
            var from = Module.CastFinishAt(spell);
            _active[body.InstanceID] = (from, from.AddSeconds(1.6));
        }
        else if (spell.Action.ID is >= 48189 and <= 48204 or >= 48208 and <= 48214)
            _active.Remove(body.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 48206 or 48207 && Body(caster) is { } body)
            // run2003's 22 pulses are 1.066s apart; a missing phase must not
            // leave an unlimited aura. The true body moves, its helper does not.
            _active[body.InstanceID] = (WorldState.CurrentTime, WorldState.FutureTime(1.6));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48206 && spell.Interrupted && Body(caster) is { } body)
            _active.Remove(body.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        _active.Remove(actor.InstanceID);
        base.OnActorDestroyed(actor);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (Sources.Any())
        {
            hints.NoEscapeDelay = true;
            AddStandoffGoal(slot, actor, hints);
        }
        else
            _standoff = null;
    }

    private void AddStandoffGoal(int slot, Actor actor, AIHints hints)
    {
        if (actor.CastInfo != null || hints.GoalPoints.Count > 0)
            return;
        var moving = Sources.ToArray();
        var ids = moving.Select(a => a.InstanceID).ToHashSet();
        var other = base.Sources.FirstOrDefault(a => !ids.Contains(a.InstanceID));
        if (other == null)
            return;
        var hazards = Module.Components.OfType<Components.GenericAOEs>()
            .SelectMany(c => c.ActiveAOEs(slot, actor).ToArray()).Where(a => a.Risky)
            .Select(a => a.ShapeDistance ?? a.Shape.Distance(a.Origin, a.Rotation)).ToArray();
        var margin = WorldState.NavigationMargin + .35f;
        // The actual envelope is R8 (+player hitbox); the hint uses R9.5. A soft stand-off preference supplies
        // reaction distance while the mobile brother closes the gap.
        bool Safe(WPos p) => Module.Arena.InBounds(p) && moving.All(a => (p - a.Position).LengthSq() >= 156.25f)
            && hazards.All(h => h.Distance(p) > margin);
        if (_standoff is not { } cached || !Safe(cached))
        {
            var points = new List<WPos> { other.Position, actor.Position };
            foreach (var source in moving)
                foreach (var radius in new[] { 12.5f, 14f, 16f, 18f })
                    for (var i = 0; i < 48; ++i)
                        points.Add(source.Position + (i * 7.5f).Degrees().ToDirection() * radius);
            var melee = 2.5f + other.HitboxRadius + actor.HitboxRadius;
            _standoff = points.Where(Safe).OrderBy(p =>
                Math.Max(0, (p - other.Position).Length() - melee) * 5f + (p - actor.Position).Length() * .3f)
                .Select(p => (WPos?)p).FirstOrDefault();
        }
        if (_standoff is { } goal)
            hints.GoalPoints.Add((goal, .6f, 40));
    }
}
