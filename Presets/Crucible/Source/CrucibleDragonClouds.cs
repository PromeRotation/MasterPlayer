// See docs/reference/CrucibleAll.spec.md.
namespace MasterPlayer.Mechanics.Crucible;

// ARR 2026-09-11 23:48:43: OID 19674 clouds move at ~2y/s after 48812
// and 48814. Contact triggers helper action 50543, not another visible cast.
// That targeted action has no sheet radius. 23:15 ARR: contact fired at ~2.3y from the cloud's
// centre while the player stood still, so each cloud is a danger, not only a path obstacle.
// Clouds fly in straight lines from their spawn; their motion also feeds the B31 breath route.
sealed class CrucibleDragonClouds(BossModule module) : Components.Voidzone(module, 2f, Clouds)
{
    // 2026-11-03 survey (18 normal high1 ARRs + run1856, .review/candidates/agent-dragon2): the farthest
    // contact was 2.458y from the nearest cloud centre and the nearest proven miss 3.164y, so the true
    // radius lies in [2.458, 3.164). The old 2.5y danger still took contacts at 2.5-3.1y.
    internal const float JudgedRadius = 3.164f;
    // Ordinary danger: a persistent capsule over the cloud's next 2.5s. The earlier short time windows
    // along the line left the shared planner without a safe cell (NoReachableSafeCell) as a cloud
    // approached, and the player stood still until it was touched (run1856 58-64s, both strategies).
    internal const float ContactRadius = 3.3f;
    // While the breath route walks through the cloud field, a persistent capsule would forbid every
    // crossing of a cloud's line. Then the clouds are time windows again (the route itself keeps
    // JudgedRadius at the forecast instant and RouteWindowRadius+0.3 from these windows).
    internal const float RouteWindowRadius = 2.6f;
    private const double Lookahead = 1.2, Window = 0.1;
    // The capsule's front must be farther than its side: an exit through the front runs ahead of the
    // cloud (v6 replay, 103.5s: the escape ran 3y along the line and was touched anyway).
    private const float CapsuleSeconds = 2.5f;
    private const long SampleTicks = TimeSpan.TicksPerSecond / 5;
    private readonly Dictionary<ulong, (WPos Position, DateTime At, WDir Velocity)> _motion = [];
    private readonly Dictionary<ulong, WPos> _spawn = [];
    // The capsule advances in 1y steps along a direction fixed from the spawn point, so its geometry
    // stays identical for ~0.5s. A capsule redrawn every frame invalidated each plan before dispatch
    // (v6 replay at 0.3s delay, 57-59s: Success plans released while a cloud crossed the centre).
    private const float CapsuleQuantum = 1f;
    private static IEnumerable<Actor> Clouds(BossModule module) => module.Enemies(19674)
        .Where(a => !a.IsDeadOrDestroyed);

    // Current position and velocity of every live cloud (velocity zero until it has moved).
    internal IEnumerable<(WPos Position, WDir Velocity)> Motions()
    {
        var now = WorldState.CurrentTime;
        foreach (var cloud in Clouds(Module))
            yield return _motion.TryGetValue(cloud.InstanceID, out var m) && now <= m.At.AddSeconds(0.75)
                ? (cloud.Position, Forecast(cloud, m.Velocity)) : (cloud.Position, default);
    }

    // Every recorded cloud leaves its spawn at 2y/s along one of eight 45-degree lines. A velocity from
    // two consecutive positions is noisy (4s ahead: 95th percentile 1.2-2.4y off, 0.4-0.6y with a 1.5s
    // baseline), enough to close the corridors the breath route needs. Only trust the line once the
    // measured motion agrees with it.
    private const float CloudSpeed = 2f, LineFromSpawn = 1f, LineAgreement = 0.9f;
    private WDir Forecast(Actor cloud, WDir measured)
    {
        if (!_spawn.TryGetValue(cloud.InstanceID, out var spawn) || (cloud.Position - spawn).LengthSq() < LineFromSpawn * LineFromSpawn || measured.LengthSq() < 0.25f)
            return measured;
        var away = (cloud.Position - spawn).Normalized();
        var angle = MathF.Round(MathF.Atan2(away.X, away.Z) / (MathF.PI / 4)) * (MathF.PI / 4);
        var line = new WDir(MathF.Sin(angle), MathF.Cos(angle));
        return line.Dot(measured.Normalized()) >= LineAgreement ? line * CloudSpeed : measured;
    }

    public override void Update()
    {
        foreach (var cloud in Clouds(Module))
        {
            var now = WorldState.CurrentTime;
            _spawn.TryAdd(cloud.InstanceID, cloud.Position);
            if (!_motion.TryGetValue(cloud.InstanceID, out var previous))
                _motion[cloud.InstanceID] = (cloud.Position, now, default);
            else if (cloud.Position != previous.Position && now > previous.At)
            {
                var delta = cloud.Position - previous.Position;
                var velocity = delta / (float)(now - previous.At).TotalSeconds;
                // Spawn/teleport corrections must not create arena-wide predictions.
                if (velocity.LengthSq() > 16f || delta.LengthSq() > 25f)
                    velocity = default;
                _motion[cloud.InstanceID] = (cloud.Position, now, velocity);
            }
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // A component's route is only a proposal. Clouds must remain in the runtime snapshot
        // when the worker is late, the route is refused, or another mechanic interrupts it.
        var now = WorldState.CurrentTime;
        var waves = Module.FindComponent<CrucibleDragonWaves>();
        var staging = waves?.Staging == true;
        var timed = waves?.Active == true || staging;
        // run2033's staging cloud sweep closed every persistent capsule exit.
        // During staging retain the full contact margin, but only forbid each
        // position when the moving cloud can actually occupy it.
        var windowRadius = waves?.Active == true ? RouteWindowRadius : ContactRadius;
        // Keep the capsule's full warning horizon while staging. A 1.2s forecast
        // let a delayed worker settle in the next cloud before the breath began.
        var lookahead = staging ? CapsuleSeconds : Lookahead;
        var first = new DateTime(now.Ticks - now.Ticks % SampleTicks + SampleTicks, DateTimeKind.Utc);
        foreach (var cloud in Clouds(Module))
        {
            var position = cloud.Position;
            var velocity = _motion.TryGetValue(cloud.InstanceID, out var m) && now <= m.At.AddSeconds(0.75) ? m.Velocity : default;
            var forecast = Forecast(cloud, velocity);
            // Snap to 0.1y so a steady cloud keeps the same geometry between frames.
            var snapped = new WPos(MathF.Round(position.X * 10) / 10, MathF.Round(position.Z * 10) / 10);
            var speed = velocity.Length();
            if (speed <= 0.1f)
            {
                hints.ForbiddenZones.Add(new(new SDCircle(snapped, ContactRadius), default, null, ExactGeometry: true));
                continue;
            }
            if (!timed || staging)
            {
                var anchor = _spawn.TryGetValue(cloud.InstanceID, out var spawn) && (position - spawn).LengthSq() > 1 ? spawn : snapped;
                var direction = anchor == snapped ? velocity / speed : (position - anchor).Normalized();
                var angle = MathF.Round(MathF.Atan2(direction.X, direction.Z) * 180 / MathF.PI) * MathF.PI / 180;
                direction = new(MathF.Sin(angle), MathF.Cos(angle));
                var along = MathF.Floor((position - anchor).Dot(direction) / CapsuleQuantum) * CapsuleQuantum;
                var reach = CapsuleQuantum + MathF.Ceiling(MathF.Round(speed * 2) / 2 * CapsuleSeconds / CapsuleQuantum) * CapsuleQuantum;
                // The cloud's own quantum and the swept area ahead are separate areas: an escape may
                // cross the area containing the player but never re-enter another one, so a player
                // ahead of the cloud must leave sideways instead of through the cloud (v8 replay, 102.8s).
                hints.ForbiddenZones.Add(new(new SDCapsule(anchor + direction * along, direction, CapsuleQuantum, ContactRadius), default, null, ExactGeometry: true));
                // Keep immediate contact as an escape danger. Only the future
                // swept track may be crossed after its timed cloud has passed.
                if (!timed)
                {
                    if (reach > CapsuleQuantum)
                        hints.ForbiddenZones.Add(new(new SDCapsule(anchor + direction * (along + CapsuleQuantum), direction, reach - CapsuleQuantum, ContactRadius), default, null, ExactGeometry: true));
                    continue;
                }
            }
            // Short time windows along the cloud's line, matching the timed route's forecast:
            // the planner and the runtime check may pass where a cloud has been or will be later.
            hints.ForbiddenZones.Add(new(new SDCircle(position, windowRadius), now.AddSeconds(-0.1), first.AddSeconds(Window), ExactGeometry: true));
            for (var at = first; at <= now.AddSeconds(lookahead); at = at.AddTicks(SampleTicks))
            {
                var p = position + forecast * (float)(at - now).TotalSeconds;
                p = new(MathF.Round(p.X * 10) / 10, MathF.Round(p.Z * 10) / 10);
                hints.ForbiddenZones.Add(new(new SDCircle(p, windowRadius), at.AddSeconds(-Window), at.AddSeconds(Window), ExactGeometry: true));
            }
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        _motion.Remove(actor.InstanceID);
        _spawn.Remove(actor.InstanceID);
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}
