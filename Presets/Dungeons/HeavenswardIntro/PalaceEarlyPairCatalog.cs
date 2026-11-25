using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static partial class HeavenswardIntroCatalog
{
    private static DungeonDefinition BlackRiderDefinition() => new(0x1814, "The Black Rider",
        p => PalaceArena(new(-300, p.Y, -220), 24.94f, 48, new(-300, -245.68f), 1.55f),
    [
        new(7157, 0, "Infatuation", new CircleTemplate(7)),
        new(7088, 0, "Hall of Sorrow", null),
        new(7089, 0, "Valfodr", null),
    ], []);

    private static DungeonDefinition YaquaruDefinition() => new(0x1815, "Yaquaru",
        p => PalaceArena(new(-300, p.Y, -220), 24.94f, 48, new(-300, -245.68f), 1.55f),
    [
        new(7091, 0, "Douse", null),
        new(7094, 0, "Electrogenesis", new CircleTemplate(8)),
    ], []);
}
