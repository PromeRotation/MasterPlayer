using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class PortaCatalog
{
    internal const uint TERRITORY = 1048;
    internal static bool Owns(uint oid) => oid is 0x38FB or 0x3900;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x38FB, "Ultima Weapon I", p => new CircleGeometry(new(-772, p.Y, -600), 19.5f),
        [
            new(28998, 0, "EarthenFury", null, "全屏伤害"),
            new(28999, 0, "Geocrush", new CircleTemplate(25)),
            new(29000, 0, "Landslide", RayRectangleTemplate.Symmetric(6, 40)),
            new(28981, 0, "Landslide", RayRectangleTemplate.Symmetric(6, 40)),
            new(29001, 0, "WeightOfTheLand", new CircleTemplate(6)),
            new(28996, 0, "AerialBlast", null, "全屏伤害"),
            new(28980, 0, "EyeOfTheStorm", new DonutTemplate(12.5f, 25)),
            new(28997, 0, "MistralShriek", new CircleTemplate(23)),
            new(29002, 0, "Hellfire", null, "全屏伤害"),
            new(28983, 0, "RadiantPlume", new CircleTemplate(8)),
            new(29003, 0, "VulcanBurst", null),
        ], [29004, 28628, 28977, 28987, 28976, 28979, 28984, 28978, 28982, 28988, 28992, 28511, 29435, 28510, 28990, 28991, 28989, 28994, 28993, 28995]),
        new(0x3900, "Ultima Weapon II", p => new CircleGeometry(new(-704, p.Y, 480), 19.5f),
        [
            new(29022, 0, "TankPurge", null, "全屏伤害"),
            new(29023, 0, "HomingLasers", null, "死刑"),
            new(29008, 0, "MagitekRayForward", RayRectangleTemplate.Symmetric(6, 40)),
            new(29009, 0, "MagitekRayRight", RayRectangleTemplate.Symmetric(6, 40)),
            new(29010, 0, "MagitekRayLeft", RayRectangleTemplate.Symmetric(6, 40)),
            new(29012, 0, "HomingRay", null),
            new(29014, 0, "LaserFocus", null),
            new(29015, 0, "AethericBoom", null),
            new(29019, 0, "AssaultCannon", RayRectangleTemplate.Symmetric(4, 40)),
            new(29020, 0, "CitadelBuster", RayRectangleTemplate.Symmetric(12, 40)),
            new(29021, 0, "Explosion", null),
            new(29024, 0, "Ultima", null),
        ], [29004, 28628, 29005, 29006, 29007, 29011, 29013, 29016, 29017, 29018, 28542]),
    ];
}
