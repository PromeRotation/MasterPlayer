using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private static bool CarnivaleBehemothAction(uint action) => action is 15258 or 15256 or 15261 or 15257;

    private IEnumerable<DungeonAmbientAoe> CarnivaleMaelstromAoes()
    {
        if (!IsCarnivale || Active?.BossOid is not (0x2732 or 0x2680)) yield break;
        // Source Voidzone consumes all live references; Dead, Targetable and
        // EventState do not filter this particular list. Positions stay unquantized.
        foreach (var actor in CarnivaleEnemies(Active.BossOid == 0x2680 ? 0x2681u : 0x2733u))
            yield return new($"carnivale/maelstrom/{actor.Id}", "Maelstrom", new CircleTemplate(8),
                actor.Position, actor.Rotation, Obstacle: true);
    }

    private IEnumerable<string> CarnivaleBehemothHints(Vector3 position)
    {
        if (CarnivaleMaelstromAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position)))
            yield return "GTFO from voidzone!";
    }
}
