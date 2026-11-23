using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class WanderersPalaceCatalog
{
    internal const uint TERRITORY = 159;

    internal static DungeonDefinition[] Create() =>
    [
        new(0x41C, "Keeper of Halidom", p => new RectangleGeometry(new(125, p.Y, 108), 0, 40, 40),
        [
            new(950, 0x41C, "Inhale", new FanTemplate(22.85f, MathF.PI / 2)),
            new(942, 0x41C, "GoobbuesGrief", new CircleTemplate(8.85f)),
            new(941, 0x41C, "MoldyPhlegm", new CircleTemplate(6)),
        ], []),
        new(0x41D, "Giant Bavarois", p => new RectangleGeometry(new(43, p.Y, -232), 0, 40, 40),
        [
            new(1394, 0x41D, "Fire", null, "单体伤害"),
        ], []),
        new(0x374, "Tonberry King", p => new CircleGeometry(new(73, p.Y, -435), 30), [], []),
    ];
}
