using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private static bool CarnivaleChimeraAction(uint action) => action is 15063 or 15064 or 15079 or 15080 or 15081;
    private const string CARNIVALE_IMP_HINT = "The imps are weak to fire spells and strong against ice.\nInterrupt Void Blizzard with Flying Sardine.";

    private void CarnivaleChimeraBootstrap()
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.ChimeraNeedsBootstrap))
        {
            module.ChimeraNeedsBootstrap = false;
            foreach (var cast in CarnivaleOrderedCasts.Where(c => c.Rule.Action == 15063))
                if (carnivaleActors.TryGetValue(cast.Source, out var caster))
                    module.Interrupts.Add(new(++carnivaleInterruptGeneration, caster, _casts[cast.Source]));
        }
    }

    private IEnumerable<string> CarnivaleChimeraHints(Vector3 position)
    {
        if (Active?.BossOid == 0x272F || Active?.BossOid == 0x2730 && CarnivaleEnemies(0x2731).Any(a => !a.Dead))
            yield return CARNIVALE_IMP_HINT;
        if (Active?.BossOid == 0x2730 && CarnivaleClawAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position)))
            yield return "GTFO from voidzone!";
    }
}
