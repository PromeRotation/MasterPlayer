using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal IEnumerable<CarnivaleLineOfSightCast> CarnivaleVoltages => IsCarnivale && Active?.BossOid == 0x2706
        && carnivaleSelected is { Started: true } module ? module.Voltages : [];

    private void CarnivaleVoltageStarted(DungeonPendingCast cast)
    {
        if (!IsCarnivale || cast.Rule.Action != 14710) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && !m.VoltageNeedsBootstrap && m.Primary.Actor.Oid == 0x2706))
            CarnivaleVoltageStarted(module, cast);
    }

    private void CarnivaleVoltageStarted(CarnivaleModuleState module, DungeonPendingCast cast)
    {
        if (!carnivaleActors.TryGetValue(cast.Source, out var caster)) return;
        var existing = module.Voltages.FindIndex(c => ReferenceEquals(c.Caster, caster));
        if (existing >= 0)
        {
            module.Voltages[existing] = module.Voltages[existing] with { Cast = cast };
            return;
        }
        var origin = caster.Actor.Position;
        var center = new Vector3(100, origin.Y, 100);
        var contours = CarnivaleArenas.Layout2CornersSightContours;
        module.Voltages.Add(new(++carnivaleLineOfSightGeneration, caster, cast, origin, contours,
            DungeonPolygonVisibility.Create(center, origin, contours)));
    }

    private void CarnivaleVoltageBootstrap()
    {
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.VoltageNeedsBootstrap))
        {
            module.VoltageNeedsBootstrap = false;
            foreach (var cast in CarnivaleOrderedCasts.Where(c => c.Rule.Action == 14710))
                CarnivaleVoltageStarted(module, cast);
        }
    }

    private void CarnivaleVoltageFinished(DungeonPendingCast cast)
    {
        if (!IsCarnivale || cast.Rule.Action != 14710) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Primary.Actor.Oid == 0x2706))
        {
            var i = module.Voltages.FindIndex(c => c.Cast.Source == cast.Source);
            if (i >= 0) module.Voltages.RemoveAt(i);
        }
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleVoltageAoes()
    {
        foreach (var cast in CarnivaleVoltages)
            yield return new($"carnivale/voltage/{cast.Generation}", "Low Voltage",
                new DungeonShapeTemplate(cast.Visibility), cast.Visibility.Position, Activation: cast.Cast.FinishAt);
    }
}
