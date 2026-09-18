// Ported from BossmodRebornCN f6914f3e8. See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// ARR 2026-09-11 23:48:43: OID 19674 clouds move at ~2y/s after 48812
// and 48814. Contact triggers helper action 50543, not another visible cast.
// That targeted action has no sheet radius; use a conservative 2y contact zone.
sealed class CrucibleDragonClouds(BossModule module) : Components.Voidzone(module, 2f, Clouds)
{
    private readonly Dictionary<ulong, (WPos Position, DateTime At, WDir Velocity)> _motion = [];
    private static IEnumerable<Actor> Clouds(BossModule module) => module.Enemies(19674)
        .Where(a => !a.IsDeadOrDestroyed);

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
        foreach (var cloud in Clouds(Module))
        {
            hints.TemporaryObstacles.Add(new SDCircle(cloud.Position, 2.3f));
            if (_motion.TryGetValue(cloud.InstanceID, out var motion)
                && WorldState.CurrentTime <= motion.At.AddSeconds(0.75) && motion.Velocity.LengthSq() > 0.01f)
                hints.AddForbiddenZone(new SDCapsule(cloud.Position, Angle.FromDirection(motion.Velocity),
                    motion.Velocity.Length() * 1.2f, 2.3f), WorldState.FutureTime(0.6));
        }
    }

    public override void OnActorDestroyed(Actor actor) => _motion.Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}
