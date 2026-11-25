using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly HashSet<ulong> carnivaleMovingActors = [];
    private long carnivaleSapGeneration;
    private static bool CarnivaleBoomAction(uint action) => action is 14903 or 14906 or 14907 or 15040 or 14902;

    private static bool CarnivaleGroupAction(CarnivaleModuleState module, uint action) => module.Primary.Actor.Oid switch
    {
        0x26FE => action is 14906 or 14907,
        0x2CD2 => action is 19059 or 19064 or 19067,
        0x2C5B => action is 18696 or 18895,
        0x2C5D => action is 18971 or 18984,
        0x30F5 => action is 23101 or 23105,
        0x30F7 => action is 23131 or 23122 or 20055,
        0x3FA5 => action is 34448 or 34449,
        _ => false,
    };

    private void CarnivaleSapStarted(DungeonPendingCast cast, bool correction)
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && !m.SapNeedsBootstrap && CarnivaleGroupAction(m, cast.Rule.Action)))
            CarnivaleSapStarted(module, cast, correction);
    }

    private void CarnivaleSapStarted(CarnivaleModuleState module, DungeonPendingCast cast, bool correction)
    {
        var index = correction ? module.Saps.FindIndex(c => c.Cast.Source == cast.Source) : -1;
        if (index >= 0) module.Saps[index] = (module.Saps[index].Generation, cast);
        else module.Saps.Add((++carnivaleSapGeneration, cast));
        if (module.Saps.Count >= 99)
            module.Saps.Sort((a, b) => a.Cast.FinishAt.CompareTo(b.Cast.FinishAt));
    }

    private void CarnivaleSapBootstrap()
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.SapNeedsBootstrap))
        {
            module.SapNeedsBootstrap = false;
            foreach (var cast in CarnivaleOrderedCasts.Where(c => CarnivaleGroupAction(module, c.Rule.Action)))
                CarnivaleSapStarted(module, cast, false);
        }
    }

    private void CarnivaleSapFinished(DungeonPendingCast cast)
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && !m.SapNeedsBootstrap))
        {
            var index = module.Saps.FindIndex(c => c.Cast.Source == cast.Source);
            if (index >= 0) module.Saps.RemoveAt(index);
        }
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleSapAoes()
    {
        if (!IsCarnivale || Active?.BossOid is not (0x26FE or 0x2CD2 or 0x2C5B or 0x2C5D or 0x30F5 or 0x30F7 or 0x3FA5)) yield break;
        foreach (var item in carnivaleSelected!.Saps)
            yield return new($"carnivale/sap/{item.Generation}", item.Cast.Rule.Name, Active.BossOid == 0x2C5D ? new FanTemplate(39, MathF.PI / 6)
                    : new CircleTemplate(Active.BossOid == 0x2CD2 ? 6 : 8),
                item.Cast.Location, item.Cast.Rotation, Activation: item.Cast.FinishAt);
    }

    private IEnumerable<string> CarnivaleBoomHints(Vector3 position)
    {
        if (Active?.BossOid == 0x26FC)
            yield return "Kill the grenades in one hit each or they will wipe you. They got 543 HP.";
        if (Active?.BossOid != 0x26FE) yield break;
        if (CarnivaleEnemies(0x26FC).FirstOrDefault(a => !a.Dead) is { } grenade)
            yield return $"{grenade.Name} in one hit or it will wipe you! It got 543 HP.";
        if (CarnivaleEnemies(0x26FD).FirstOrDefault(a => !a.Dead) is { } bomb)
            yield return $"Use Sticky Tongue to pull the {bomb.Name} to the bos\nto interrupt the enrage!";
        if (CarnivaleSapAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position)))
            yield return "GTFO from aoe!";
    }
}
