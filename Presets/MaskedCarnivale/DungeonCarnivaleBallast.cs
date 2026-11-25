using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long carnivaleBallastGeneration;
    internal IEnumerable<CarnivaleBallastSequence> CarnivaleBallasts => IsCarnivale && Active?.BossOid == 0x26F9
        && carnivaleSelected is { Started: true } module ? module.Ballasts : [];

    private void CarnivaleBallastStarted(DungeonPendingCast cast, bool correction)
    {
        if (!IsCarnivale || cast.Rule.Action != 14893) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && !m.BallastNeedsBootstrap && m.Primary.Actor.Oid == 0x26F9))
            CarnivaleBallastStarted(module, cast, correction);
    }

    private void CarnivaleBallastStarted(CarnivaleModuleState module, DungeonPendingCast cast, bool correction)
    {
        var index = correction && module.BallastVisuals.TryGetValue(cast.Source, out var generation)
            ? module.Ballasts.FindIndex(s => s.Generation == generation && s.Order == 0) : -1;
        // A correction cannot resurrect a consumed stage or a fully consumed sequence.
        if (correction && index < 0) return;
        var sequence = new CarnivaleBallastSequence(index < 0 ? ++carnivaleBallastGeneration : module.Ballasts[index].Generation,
            cast, cast.Location, cast.Rotation, cast.FinishAt.AddSeconds(3.6));
        if (index < 0) module.Ballasts.Add(sequence);
        else module.Ballasts[index] = sequence;
        module.BallastVisuals[cast.Source] = sequence.Generation;
    }

    private void CarnivaleBallastBootstrap()
    {
        foreach (var module in carnivaleLoaded.Where(m => m.BallastNeedsBootstrap))
        {
            module.BallastNeedsBootstrap = false;
            foreach (var cast in _casts.Values)
            {
                if (cast.Rule.Action == 14893) CarnivaleBallastStarted(module, cast, false);
                if (cast.Rule.Action == 14890 && carnivaleActors.TryGetValue(cast.Source, out var caster))
                    module.Interrupts.Add(new(++carnivaleInterruptGeneration, caster, cast));
            }
        }
    }

    private void CarnivaleBallastFinished(DungeonPendingCast cast)
    {
        if (IsCarnivale && cast.Rule.Action == 14893)
            foreach (var module in carnivaleModules) module.BallastVisuals.Remove(cast.Source);
        var order = cast.Rule.Action switch { 14894 => 0, 14895 => 1, 14896 => 2, _ => -1 };
        if (!IsCarnivale || order < 0) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Primary.Actor.Oid == 0x26F9))
        {
            var index = module.Ballasts.FindIndex(s => s.Order == order
                && MathF.Abs(s.Origin.X - cast.Location.X) <= 1 && MathF.Abs(s.Origin.Z - cast.Location.Z) <= 1
                && MathF.Abs(MathF.IEEERemainder(s.Rotation - cast.Rotation, MathF.Tau)) <= .05f);
            if (index < 0) continue;
            if (order == 2) module.Ballasts.RemoveAt(index);
            else module.Ballasts[index] = module.Ballasts[index] with { Order = order + 1, Activation = _now.AddSeconds(.6) };
        }
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleBallastAoes()
    {
        foreach (var sequence in CarnivaleBallasts)
            for (var order = sequence.Order; order < 3; ++order)
            {
                GeometryTemplate shape = order switch
                {
                    0 => new FanTemplate(5.5f, MathF.PI * 1.5f),
                    1 => new PtDonutSectorTemplate(5.5f, 10.5f, MathF.PI * 1.5f),
                    _ => new PtDonutSectorTemplate(10.5f, 15.5f, MathF.PI * 1.5f),
                };
                yield return new($"carnivale/ballast/{sequence.Generation}/{order}", "Ballast", shape,
                    sequence.Origin, sequence.Rotation, Activation: sequence.Activation);
            }
    }
}
