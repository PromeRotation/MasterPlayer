using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private static bool CarnivalePitAction(uint action) => action is 15317 or 15316 or 15318 or 15322 or 15319
        or 15320 or 15321 or 15327 or 15324 or 15325 or 15326;

    private void CarnivalePitStarted(DungeonPendingCast cast, bool correction)
    {
        if (!IsCarnivale || cast.Rule.Action != 15325) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && !m.PitNeedsBootstrap && m.Primary.Actor.Oid == 0x2739))
            CarnivalePitKnockback(module, cast, correction);
    }

    private void CarnivalePitKnockback(CarnivaleModuleState module, DungeonPendingCast cast, bool correction)
    {
        var index = correction ? module.CastKnockbacks.FindIndex(c => c.Cast.Source == cast.Source) : -1;
        var source = new CarnivaleKnockbackCast(index >= 0 ? module.CastKnockbacks[index].Generation : ++carnivaleTyphonGeneration,
            cast, 10, false, null, Forward: true);
        if (index >= 0) module.CastKnockbacks[index] = source;
        else module.CastKnockbacks.Add(source);
    }

    private void CarnivalePitBootstrap()
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.PitNeedsBootstrap))
        {
            module.PitNeedsBootstrap = false;
            foreach (var cast in CarnivaleOrderedCasts)
            {
                if (module.Primary.Actor.Oid == 0x2739 && cast.Rule.Action == 15325)
                    CarnivalePitKnockback(module, cast, false);
                if ((module.Primary.Actor.Oid == 0x2735 && cast.Rule.Action == 15318
                    || module.Primary.Actor.Oid == 0x2736 && cast.Rule.Action == 15321)
                    && carnivaleActors.TryGetValue(cast.Source, out var caster))
                    module.Interrupts.Add(new(++carnivaleInterruptGeneration, caster, _casts[cast.Source]));
            }
        }
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleVacuumAoes()
    {
        if (!IsCarnivale || Active?.BossOid != 0x2739) yield break;
        foreach (var actor in CarnivaleEnemies(0x273B).Where(a => !a.Dead))
            yield return new($"carnivale/vacuum/{actor.Id}", "Vacuum Blade", new CircleTemplate(3),
                actor.Position, actor.Rotation, Obstacle: true);
    }

    private IEnumerable<DungeonAmbientAoe> CarnivalePitAoes()
    {
        foreach (var aoe in CarnivaleVacuumAoes()) yield return aoe;
        if (Active?.BossOid != 0x2739 || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        foreach (var movement in CarnivaleCastKnockbackMovements(player, 15325))
            foreach (var aoe in QuestKnockbackProjection($"carnivale/pit/kb/{movement.Source.Generation}", player, movement.From, movement.To))
                yield return aoe;
    }

    private IEnumerable<string> CarnivalePitHints(Vector3 position)
    {
        if (Active?.BossOid == 0x2735)
        {
            if (carnivalePrimary is { Dead: false } primary)
                yield return $"{primary.Name} is immune to magical damage!";
            if (CarnivaleEnemies(0x2734).FirstOrDefault() is { Dead: false } viking)
                yield return $"{viking.Name} is immune to physical damage!";
        }
        if (Active?.BossOid == 0x2736 && CarnivaleEnemies(0x2738).FirstOrDefault(a => !a.Dead) is { } magus)
        {
            yield return $"{magus.Name} is immune to magical damage!";
            // The source queries ArenaMagus for both branches. Keep its text
            // without turning the unfinished damage-type TODO into restrictions.
            yield return $"{magus.Name} is immune to physical damage!";
        }
        if (Active?.BossOid != 0x2739) yield break;
        if (CarnivaleEnemies(0x273A).FirstOrDefault(a => !a.Dead) is { } add)
            yield return $"Kill {add.Name} fast or wipe!\nUse ranged physical attacks.";
        var pools = CarnivaleVacuumAoes().ToArray();
        if (pools.Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position)))
            yield return "GTFO from voidzone!";
        if (_actors.GetValueOrDefault(PlayerId) is { } player
            && CarnivaleCastKnockbackMovements(player, 15325).Any(m => Arena?.Contains(m.To) == false
                || pools.Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(m.To))))
            yield return "About to be knocked into danger!";
    }

    private IEnumerable<DungeonDamagePrediction> CarnivaleSingleTargetDamage()
    {
        if (!IsCarnivale || Active?.BossOid is not (0x2736 or 0x2C58)) yield break;
        foreach (var cast in _casts.Values.Where(c => Active.BossOid == 0x2C58 ? c.Rule.Action == 18601 : c.Rule.Action is 15319 or 15320))
        {
            var caster = _actors.GetValueOrDefault(cast.Source);
            var current = caster?.Cast;
            var target = current?.Action == cast.Rule.Action ? current.TargetId : cast.TargetId;
            if (target == cast.Source) target = caster?.TargetId ?? 0;
            var slot = CarnivaleSlot(target);
            if (slot is < 0 or >= 64) continue;
            var activation = current?.Action == cast.Rule.Action ? current.FinishAt : cast.FinishAt;
            if (activation < _now.AddSeconds((double).3f)) activation = _now.AddSeconds((double).3f);
            var actor = _actors.GetValueOrDefault(target);
            var member = CurrentPartyMembers.FirstOrDefault(m => m.EntityId == target);
            yield return new(target, slot, member?.Role ?? actor?.PartyRole ?? "", actor?.Name ?? "", activation, "Tankbuster");
        }
    }
}
