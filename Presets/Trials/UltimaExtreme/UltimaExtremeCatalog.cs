using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class UltimaExtremeCatalog
{
    internal const uint TERRITORY = 348;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x90F, "Ultima Weapon Extreme", p => new CircleGeometry(new(0, p.Y, 0), 20),
        [
            new(1521, 0, "RadiantPlume", new CircleTemplate(8)),
            new(1522, 0, "WeightOfTheLand", new CircleTemplate(6)),
            new(1524, 0, "Eruption", new CircleTemplate(8)),
            new(1507, 0, "MagitekRayCenter", RayRectangleTemplate.Symmetric(6, 40)),
            new(1508, 0, "MagitekRayRight", RayRectangleTemplate.Symmetric(6, 40)),
            new(1509, 0, "MagitekRayLeft", RayRectangleTemplate.Symmetric(6, 40)),
            new(1526, 0, "AssaultCannon", RayRectangleTemplate.Symmetric(2, 45)),
            new(1514, 0, "TankPurge", null, "全屏伤害"),
            new(1517, 0, "MistralSong", null),
            new(1519, 0, "EyeOfTheStorm", null),
            new(1520, 0, "Geocrush", null),
            new(1525, 0, "CrimsonCyclone", null),
            new(1511, 0, "AethericBoom", null, "击退及魔科学爆弹"),
        ], []),
    ];
}
