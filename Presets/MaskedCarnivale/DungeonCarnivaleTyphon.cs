using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long carnivaleTyphonGeneration;
    private static bool CarnivaleTyphonAction(uint action) =>
        action is 14704 or 14706 or 14712 or 14713 or 14714 or 14717 or 14718 or 14744 or 14745 or 14746 or 14747 or 14748 or 19266;
    internal IEnumerable<CarnivaleKnockbackCast> CarnivaleCastKnockbacks => IsCarnivale && Active?.BossOid is 0x272A or 0x272C or 0x2739 or 0x2CF8 or 0x2C5B or 0x2C5D or 0x2C6A or 0x2C6C or 0x3FA5 or 0x3FA7
        ? carnivaleSelected!.CastKnockbacks : [];

    private void CarnivaleTyphonStarted(DungeonPendingCast cast, bool correction)
    {
        if (!IsCarnivale || !CarnivaleTyphonAction(cast.Rule.Action)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && !m.TyphonNeedsBootstrap
            && m.Primary.Actor.Oid is 0x272A or 0x272C or 0x2CF8))
            CarnivaleTyphonStarted(module, cast, correction);
    }

    private void CarnivaleTyphonStarted(CarnivaleModuleState module, DungeonPendingCast cast, bool correction)
    {
        var oid = module.Primary.Actor.Oid;
        if (oid is 0x272A or 0x272C && cast.Rule.Action == 14704
            || oid == 0x272C && cast.Rule.Action == 14748 || oid == 0x2CF8 && cast.Rule.Action == 19266)
        {
            var index = correction ? module.CastKnockbacks.FindIndex(k => k.Cast.Source == cast.Source
                && k.Cast.Rule.Action == cast.Rule.Action) : -1;
            var value = new CarnivaleKnockbackCast(index >= 0 ? module.CastKnockbacks[index].Generation : ++carnivaleTyphonGeneration,
                cast, cast.Rule.Action switch { 14704 => 30, 19266 => 15, _ => 20 }, oid is 0x272A or 0x2CF8,
                cast.Rule.Action == 14748 ? new RayRectangleTemplate(-5, 5, 57.2f) : null);
            if (index >= 0) module.CastKnockbacks[index] = value;
            else module.CastKnockbacks.Add(value);
        }
        if (oid == 0x272C && cast.Rule.Action is 14706 or 14747 || oid == 0x272A && cast.Rule.Action == 14706)
        {
            var index = correction ? module.TyphonFireballs.FindIndex(c => c.Cast.Source == cast.Source) : -1;
            if (index >= 0) module.TyphonFireballs[index] = (module.TyphonFireballs[index].Generation, cast);
            else module.TyphonFireballs.Add((++carnivaleTyphonGeneration, cast));
            if (oid == 0x272C && module.TyphonFireballs.Count >= 99)
                module.TyphonFireballs.Sort((a, b) => a.Cast.FinishAt.CompareTo(b.Cast.FinishAt));
        }
    }

    private void CarnivaleTyphonBootstrap()
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.TyphonNeedsBootstrap))
        {
            module.TyphonNeedsBootstrap = false;
            foreach (var cast in CarnivaleOrderedCasts)
            {
                CarnivaleTyphonStarted(module, cast, false);
                if ((module.Primary.Actor.Oid == 0x272B && cast.Rule.Action == 14712
                    || module.Primary.Actor.Oid == 0x272C && cast.Rule.Action == 14744)
                    && carnivaleActors.TryGetValue(cast.Source, out var source))
                    module.Interrupts.Add(new(++carnivaleInterruptGeneration, source, _casts[cast.Source]));
            }
            CarnivaleImpSongHint(module);
        }
    }

    private static void CarnivaleImpSongHint(CarnivaleModuleState module)
    {
        var casters = module.Interrupts.Where(c => c.Cast.Rule.Action == 14744).ToArray();
        module.ImpSongHint = casters.Length == 1 ? $"Interrupt {casters[0].Caster.Actor.Name}!" : "Interrupt!";
    }

    private void CarnivaleTyphonFinished(DungeonPendingCast cast)
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && !m.TyphonNeedsBootstrap))
        {
            var index = module.CastKnockbacks.FindIndex(k => k.Cast.Source == cast.Source && k.Cast.Rule.Action == cast.Rule.Action);
            if (index >= 0) module.CastKnockbacks.RemoveAt(index);
            index = module.TyphonFireballs.FindIndex(c => c.Cast.Source == cast.Source);
            if (index >= 0) module.TyphonFireballs.RemoveAt(index);
        }
    }

    internal IEnumerable<(CarnivaleKnockbackCast Source, Vector3 From, Vector3 To)> CarnivaleCastKnockbackMovements(DungeonActor actor, uint action)
    {
        if (Arena == null || carnivaleSelected == null) yield break;
        var from = actor.Position;
        foreach (var source in CarnivaleCastKnockbacks.Where(k => k.Cast.Rule.Action == action))
        {
            var cast = source.Cast;
            if (carnivaleSelected.ChargeImmunity.ImmuneAt(CarnivaleSlot(actor.Id), cast.FinishAt)
                || source.EffectShape?.Resolve(cast.Location, cast.Rotation, null).Contains(from, 0) == false) continue;
            var direction = source.Forward ? new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation)) : from - cast.Location;
            direction.Y = 0;
            if (direction.LengthSquared() == 0) continue;
            var distance = source.Distance;
            if (source.Pull)
            {
                distance = MathF.Min(distance, direction.Length());
                direction = -direction;
            }
            direction = Vector3.Normalize(direction);
            var to = source.StopAtWall ? TreasureWallLanding(from, direction, distance, actor.HitboxRadius)
                : from + direction * distance;
            yield return (source, from, to);
            from = to;
        }
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleTyphonAoes()
    {
        if (!IsCarnivale || Active?.BossOid is not (0x272A or 0x272C)) yield break;
        foreach (var item in carnivaleSelected!.TyphonFireballs)
            yield return new($"carnivale/typhon/fireball/{item.Generation}", "Tentacle / Fireball",
                new CircleTemplate(8), item.Cast.Location, item.Cast.Rotation, Activation: item.Cast.FinishAt);
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        // Separate components each start from the actor's current position.
        foreach (var action in new uint[] { 14748, 14704 })
            foreach (var movement in CarnivaleCastKnockbackMovements(player, action))
                foreach (var aoe in QuestKnockbackProjection($"carnivale/typhon/kb/{movement.Source.Generation}", player,
                    movement.From, movement.To))
                    yield return aoe;
    }

    private IEnumerable<string> CarnivaleTyphonHints()
    {
        if (Active?.BossOid is not (0x272A or 0x272C) || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (carnivaleSelected!.TyphonFireballs.Any(c =>
            new CircleTemplate(8).Resolve(c.Cast.Location, c.Cast.Rotation, null).Contains(player.Position)))
            yield return "GTFO from aoe!";
        foreach (var action in new uint[] { 14748, 14704 })
            if (CarnivaleCastKnockbackMovements(player, action).Any(m => !m.Source.StopAtWall && Arena?.Contains(m.To) == false))
                yield return "About to be knocked into danger!";
    }
}
