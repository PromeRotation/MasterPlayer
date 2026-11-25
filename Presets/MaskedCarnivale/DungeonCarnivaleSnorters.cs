using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private static bool CarnivaleSnorterAction(uint action) => action is 19258 or 19266;
    internal CarnivaleSnorterState? CarnivaleSnorter => IsCarnivale && Active?.BossOid == 0x2CF8 ? carnivaleSelected?.Snorter : null;

    private void CarnivaleSnorterCreated(CarnivaleModuleState module, AlphaActorState actor)
    {
        var state = module.Snorter!;
        if (actor.Actor.Oid == 0x2CEC) state.ExplosiveCreatedAt = _now;
        if (actor.Actor.Oid != 0x2CF9 || CarnivaleEnemies(0x2CF9).Count() != 8) return;
        state.FungahActivation = _now.AddSeconds(5d);
        if (CarnivaleEnemies(0x2CEC).FirstOrDefault() is not { } explosive) return;
        var p = new Vector2(explosive.Position.X, explosive.Position.Z);
        foreach (var special in new Vector2[] { new(96, 94), new(92, 100), new(96, 106), new(108, 100) })
            if (MathF.Abs(p.X - special.X) <= 3 && MathF.Abs(p.Y - special.Y) <= 3)
            {
                state.FungahActivation = _now.AddSeconds(5.3d);
                state.OtherPatterns = true;
                break;
            }
    }

    private void CarnivaleSnorterInput(AlphaActorState actor, bool created, DungeonActor? previous)
    {
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Snorter is { NeedsBootstrap: false }))
        {
            if (created) CarnivaleSnorterCreated(module, actor);
            if (actor.Actor.Oid == 0x2CF9 && actor.Actor.AnimationState1 == 1
                && (created || previous != null && (actor.Actor.ModelState != previous.ModelState || actor.Actor.AnimationState1 != previous.AnimationState1
                    || actor.Actor.AnimationState2 != previous.AnimationState2)))
            {
                var state = module.Snorter!;
                state.Bombs.Add((++state.Generation, actor));
                state.ExplosionActivation = _now.AddSeconds(6d);
            }
        }
    }

    private void CarnivaleSnorterStarted(DungeonPendingCast cast, bool correction)
    {
        if (!IsCarnivale || correction || cast.Rule.Action != 19266) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Snorter is { NeedsBootstrap: false }))
            module.Snorter!.SnortEffectEnds = cast.FinishAt.AddSeconds(2.5d);
    }

    private void CarnivaleSnorterEffect(ulong source, uint action)
    {
        if (!IsCarnivale || !carnivaleActors.TryGetValue(source, out var caster) || !_actors.ContainsKey(source)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Snorter != null))
        {
            var state = module.Snorter!;
            if (action == 19259)
            {
                var index = state.Bombs.FindIndex(b => ReferenceEquals(b.Actor, caster));
                if (index >= 0) state.Bombs.RemoveAt(index);
                state.FungahActivation = default;
            }
            else if (action is 19256 or 19257) state.OtherPatterns = false;
        }
    }

    private void CarnivaleSnorterUpdate()
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Snorter != null))
        {
            var state = module.Snorter!;
            if (state.NeedsBootstrap)
            {
                state.NeedsBootstrap = false;
                foreach (var actor in carnivaleActorOrder) CarnivaleSnorterCreated(module, actor);
                // Component activation replays Create and Cast, never ModelState.
                foreach (var cast in CarnivaleOrderedCasts.Where(c => c.Rule.Action == 19266))
                    state.SnortEffectEnds = cast.FinishAt.AddSeconds(2.5d);
            }
            if (state.SnortEffectEnds != default && state.SnortEffectEnds < _now)
                state.SnortEffectEnds = default;
        }
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleSnorterExplosions()
    {
        if (CarnivaleSnorter is not { } state || carnivaleSelected == null) yield break;
        if (state.SnortEffectEnds != default && state.SnortEffectEnds <= _now) yield break;
        foreach (var bomb in state.Bombs)
        {
            var position = bomb.Actor.Actor.Position;
            if (state.SnortEffectEnds > _now)
            {
                var delta = position - carnivaleSelected.Primary.Actor.Position;
                delta.Y = 0;
                var direction = delta.LengthSquared() > 0 ? Vector3.Normalize(delta) : Vector3.Zero;
                position += direction * MathF.Min(15, QuestWindRay(position, direction) - .4f);
            }
            yield return new($"carnivale/snorter/bomb/{bomb.Generation}", "Explosion", new CircleTemplate(8),
                position, Activation: state.ExplosionActivation);
        }
    }

    internal Vector3? CarnivaleFungahLanding(DungeonActor actor)
    {
        if (CarnivaleSnorter is not { } state || carnivaleSelected == null
            || state.FungahActivation == default && !state.OtherPatterns
            || carnivaleSelected.ChargeImmunity.ImmuneAt(CarnivaleSlot(actor.Id), state.FungahActivation)) return null;
        var delta = actor.Position - carnivaleSelected.Primary.Actor.Position;
        delta.Y = 0;
        if (delta.LengthSquared() == 0 || delta.LengthSquared() > 12.5f * 12.5f) return null;
        return TreasureWallLanding(actor.Position, Vector3.Normalize(delta), 15, actor.HitboxRadius);
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleSnorterAoes()
    {
        foreach (var aoe in CarnivaleSnorterExplosions()) yield return aoe;
        if (Active?.BossOid != 0x2CF8 || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        foreach (var movement in CarnivaleCastKnockbackMovements(player, 19266))
            foreach (var aoe in QuestKnockbackProjection($"carnivale/snorter/snort/{movement.Source.Generation}", player, movement.From, movement.To))
                yield return aoe;
        if (CarnivaleFungahLanding(player) is { } to)
            foreach (var aoe in QuestKnockbackProjection("carnivale/snorter/fungah", player, player.Position, to))
                yield return aoe;
    }

    private IEnumerable<string> CarnivaleSnorterHints(Vector3 position)
    {
        if (CarnivaleSnorter is not { } state) yield break;
        if (CarnivaleEnemies(0x2CEC).FirstOrDefault() is { } first)
        {
            if (!first.Dead) yield return $"A {first.Name} spawned, destroy it asap.";
            if (first.Targetable) yield return $"Explosion in ca.: {Math.Max(35 - (_now - state.ExplosiveCreatedAt).TotalSeconds, 0):f1}s";
        }
        var bombs = CarnivaleSnorterExplosions().ToArray();
        if (bombs.Any(a => a.Shape.Resolve(a.Position, 0, null).Contains(position)))
            yield return "GTFO from aoe!";
        if (_actors.GetValueOrDefault(PlayerId) is { } player && CarnivaleFungahLanding(player) is { } to
            && bombs.Any(a => a.Shape.Resolve(a.Position, 0, null).Contains(to)))
            yield return "About to be knocked into danger!";
    }
}
