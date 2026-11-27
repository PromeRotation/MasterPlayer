using MasterPlayer.Mechanics.Dungeons;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    private void CreatedTower(BozjaActor actor, BozjaRule rule)
    {
        if (!Active || !actor.NativeNpc || actor.Actor.Oid != rule.EnemyOid) return;
        Casts.Add(new(++serial, rule, actor, IsVirtue ? actor.Actor.Position : DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position),
            0, Now.AddSeconds(IsVirtue ? 20d : IsGauntlet ? 9d : 8.7d))
            { AllowedSoakers = IsVirtue ? [] : null });
    }

    private void CreatedTowerEffect(BozjaActor caster, uint action)
    {
        foreach (var rule in ActiveRules.Where(r => r.Kind == BozjaRuleKind.Tower
            && r.EnemyOid != 0 && r.Actions.Contains(action)))
        {
            var index = Casts.FindIndex(c => c.Rule == rule && AlmostEqual(c.Origin, caster.Actor.Position));
            if (index >= 0) Casts.RemoveAt(index);
        }
    }
}
