// See docs/reference/CrucibleAll.spec.md.
namespace MasterPlayer.Mechanics.Crucible;

// ARR 2026-09-11 23:48:43: OID 19674 clouds move at ~2y/s after 48812
// and 48814. Contact triggers helper action 50543, not another visible cast.
// That targeted action has no sheet radius. 23:15 ARR: contact fired at ~2.3y from the cloud's
// centre while the player stood still, so each cloud is a danger, not only a path obstacle.
// Clouds fly in straight lines from their spawn; their motion also feeds the B31 breath route.
sealed class CrucibleDragonClouds(BossModule module) : Components.Voidzone(module, 2f, Clouds)
{
    internal const float ContactRadius = 2.5f;
    private const double Lookahead = 1.2, Window = 0.1;
    private const long SampleTicks = TimeSpan.TicksPerSecond / 5;
    private readonly Dictionary<ulong, (WPos Position, DateTime At, WDir Velocity)> _motion = [];
    private static IEnumerable<Actor> Clouds(BossModule module) => module.Enemies(19674)
        .Where(a => !a.IsDeadOrDestroyed);

    // Current position and velocity of every live cloud (velocity zero until it has moved).
    internal IEnumerable<(WPos Position, WDir Velocity)> Motions()
    {
        var now = WorldState.CurrentTime;
        foreach (var cloud in Clouds(Module))
            yield return _motion.TryGetValue(cloud.InstanceID, out var m) && now <= m.At.AddSeconds(0.75)
                ? (cloud.Position, m.Velocity) : (cloud.Position, default);
    }

    public override void Update()
    {
        foreach (var cloud in Clouds(Module))
        {
            var now = WorldState.CurrentTime;
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
        var now = WorldState.CurrentTime;
        var first = new DateTime(now.Ticks - now.Ticks % SampleTicks + SampleTicks, DateTimeKind.Utc);
        foreach (var (position, velocity) in Motions())
        {
            if (velocity.LengthSq() <= 0.01f)
            {
                hints.ForbiddenZones.Add(new(new SDCircle(position, ContactRadius), default, null, ExactGeometry: true));
                continue;
            }
            // Short time windows along the cloud's line, matching the timed route's forecast:
            // the planner and the runtime check may pass where a cloud has been or will be later.
            hints.ForbiddenZones.Add(new(new SDCircle(position, ContactRadius), now.AddSeconds(-0.1), first.AddSeconds(Window), ExactGeometry: true));
            for (var at = first; at <= now.AddSeconds(Lookahead); at = at.AddTicks(SampleTicks))
            {
                var p = position + velocity * (float)(at - now).TotalSeconds;
                // Snap to 0.1y so a steady cloud keeps the same forecast between frames.
                p = new(MathF.Round(p.X * 10) / 10, MathF.Round(p.Z * 10) / 10);
                hints.ForbiddenZones.Add(new(new SDCircle(p, ContactRadius), at.AddSeconds(-Window), at.AddSeconds(Window), ExactGeometry: true));
            }
        }
    }

    public override void OnActorDestroyed(Actor actor) => _motion.Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}
