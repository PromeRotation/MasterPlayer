using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsGodmother => territory == HeavenswardIntroCatalog.GODMOTHER;
    private readonly Dictionary<ulong, AlphaActorState> godmotherActors = [];
    private readonly List<AlphaActorState> godmotherActorOrder = [];
    private readonly HashSet<AlphaActorState> godmotherCreated = [];
    private readonly List<PalaceGodmotherExplosion> godmotherExplosions = [];
    private int godmotherBombIndex;
    private long godmotherGeneration;
    internal IReadOnlyList<PalaceGodmotherExplosion> GodmotherExplosions => godmotherExplosions;
    internal int GodmotherBombIndex => godmotherBombIndex;
    internal bool UsesGodmotherNativeEffect(uint action) => IsGodmother && action == 7104;

    private void GodmotherInput()
    {
        if (!IsGodmother) return;
        foreach (var id in godmotherActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
            GodmotherRemoved(id);
        foreach (var actor in _actors.Values)
        {
            if (godmotherActors.TryGetValue(actor.Id, out var state)) state.Actor = actor;
            else
            {
                var created = new AlphaActorState(actor);
                godmotherActors.Add(actor.Id, created);
                godmotherActorOrder.Add(created);
            }
        }
    }

    private void GodmotherRemoved(ulong id)
    {
        if (godmotherActors.Remove(id, out var state))
        {
            state.Removed = true;
            godmotherActorOrder.Remove(state);
        }
    }

    private void GodmotherCreated(DungeonActor actor)
    {
        if (!IsGodmother || Active == null || actor.Oid != 0x18E9
            || !godmotherActors.TryGetValue(actor.Id, out var state) || !godmotherCreated.Add(state)) return;
        godmotherExplosions.Add(new(++godmotherGeneration, state, QuantizedDungeonPosition(actor.Position), _now.AddSeconds(10)));
    }

    private void GodmotherFinish(DungeonPendingCast cast)
    {
        if (IsGodmother && cast.Rule.Action == 7106) godmotherExplosions.Clear();
    }

    private DungeonActor? GodmotherFirstGiddy => IsGodmother && Active != null
        ? godmotherActorOrder.FirstOrDefault(a => a.Actor.Oid == 0x18EA)?.Actor : null;

    private IEnumerable<ulong> GodmotherForbiddenTargets
    {
        get
        {
            if (GodmotherFirstGiddy is { } giddy && _actors.GetValueOrDefault(PrimaryId) is { } boss
                && NativeEnemyDistanceSquared(boss.Position, giddy.Position) <= 7.2f * 7.2f)
                yield return giddy.Id;
        }
    }

    internal PalaceGodmotherPull? GodmotherPull
    {
        get
        {
            if (!IsGodmother || Active == null || GodmotherFirstGiddy is not { Dead: true }
                || !godmotherActors.TryGetValue(PrimaryId, out var primary)
                || primary.Actor is not { Dead: false, Targetable: true, Hostile: true } boss
                || boss.TargetId != PlayerId) return null;
            Vector2[] spawns = [new(-305, -240), new(-295, -240), new(-295, -240), new(-300, -235)];
            var next = spawns[godmotherBombIndex & 3];
            return new(primary, new(next.X, boss.Position.Y, next.Y), 2, 2);
        }
    }

    private IEnumerable<DungeonAmbientAoe> GodmotherAoes()
    {
        if (!IsGodmother || Active == null) yield break;
        foreach (var explosion in godmotherExplosions)
            yield return new($"godmother/explosion/{explosion.Generation}", "Self Destruct", new CircleTemplate(6.6f),
                explosion.Origin, Activation: explosion.Activation);
        if (!_casts.Values.Any(c => c.Rule.Action is 7102 or 7101))
            foreach (var source in _actors.Values.Where(a => a.Oid == 0x1817 && !a.Dead && a.Targetable
                && _actors.ContainsKey(a.TargetId)))
            {
                var target = _actors[source.TargetId];
                var rotation = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
                var origin = QuantizedDungeonPosition(source.Position);
                var prefix = $"godmother/cleave/{source.Id}";
                yield return new(prefix + "/display", "Scalding Scolding", new FanTemplate(11.75f, 2 * MathF.PI / 3),
                    origin, rotation, Navigate: false, OutlineOnly: true);
                if (target.Id != PlayerId)
                    yield return new(prefix + "/avoid", "Scalding Scolding", new FanTemplate(11.75f, 2 * MathF.PI / 3),
                        origin, rotation, Display: false);
                else
                    foreach (var member in _actors.Values.Where(a => a.PartySlot is >= 0 and < 8 && !a.Dead && a.Id != PlayerId))
                        yield return new($"{prefix}/party/{member.Id}", "Do not cleave party", new FanTemplate(100, 2 * MathF.PI / 3),
                            origin, MathF.Atan2(member.Position.X - source.Position.X, member.Position.Z - source.Position.Z), Display: false);
            }
        if (GodmotherPull is { } pull)
        {
            var delta = pull.Source.Actor.Position - pull.Destination;
            delta.Y = 0;
            if (delta.LengthSquared() > pull.DestinationRadius * pull.DestinationRadius)
            {
                var position = pull.Destination - Vector3.Normalize(delta) * (pull.TankDistance + pull.Source.Actor.HitboxRadius + .5f);
                // The existing MP bridge ignores standalone goals below 100.
                // Retain the source weight/geometry without adding new movement.
                yield return new("godmother/pull", "引导首领至下一炸弹位置", new CircleTemplate(.5f), position,
                    Navigate: false, Display: false, Safe: true, GoalPriority: 10, OverrideDangerMargin: false);
            }
        }
    }

    private IEnumerable<string> GodmotherHints(Vector3? player)
    {
        if (!IsGodmother || Active == null) yield break;
        foreach (var id in ForbiddenDotTargets) yield return $"停止对 {id:X} 施加持续伤害";
        if (player is not { } position) yield break;
        if (godmotherExplosions.Any(e => NativeEnemyDistanceSquared(position, e.Origin) <= 6.6f * 6.6f))
            yield return "GTFO from aoe!";
        if (_casts.Values.Any(c => c.Rule.Action is 7102 or 7101)) yield break;
        foreach (var source in _actors.Values.Where(a => a.Oid == 0x1817 && !a.Dead && a.Targetable
            && a.TargetId != PlayerId && _actors.ContainsKey(a.TargetId)))
        {
            var target = _actors[source.TargetId];
            var rotation = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            if (new FanTemplate(11.75f, 2 * MathF.PI / 3).Resolve(source.Position, rotation, null)
                .Contains(QuantizedDungeonPosition(position)))
            {
                yield return "GTFO from cleave!";
                yield break;
            }
        }
    }

    private IEnumerable<DungeonDamagePrediction> GodmotherDamage()
    {
        if (!IsGodmother || Active == null) yield break;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 7102 or 7105))
            foreach (var actor in _actors.Values.Where(a => !a.Dead && a.PartySlot is >= 0 and < 8))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, PalacePlantCastFinish(cast), "Raidwide");
    }

    private void ResetGodmother()
    {
        // Keep world identities and creation order across phase resets; only
        // component-local predictions and counters are reconstructed on pull.
        godmotherCreated.Clear();
        godmotherExplosions.Clear();
        godmotherBombIndex = 0;
        godmotherGeneration = 0;
    }
}
