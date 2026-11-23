using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class VaultCatalog
{
    internal const uint TERRITORY = 1066;

    internal static DungeonDefinition[] Create() =>
    [
        new(0x1051, "Ser Adelphel", p => VaultArenas.Adelphel(p.Y),
        [
            new(1099, 0x104E, "Bloodstain", new CircleTemplate(5)),
            new(4126, 0x1051, "HoliestOfHoly", null, "全屏伤害"),
            new(4127, 0x1051, "HolyShieldBash", null, "眩晕及两次单体伤害"),
        ], [4125, 4130, 4131, 4132, 4980], [0x104E]),
        new(0x1054, "Ser Grinnaux", p => VaultArenas.Grinnaux(p.Y),
        [
            new(1135, 0x1053, "Rive", RayRectangleTemplate.Symmetric(2, 30.5f)),
            new(4134, 0x1054, "HyperdimensionalSlash", RayRectangleTemplate.Symmetric(8, 47.2f)),
            new(4137, 0xD25, "DimensionalCollapseInner", new PtDonutSectorTemplate(2.5f, 7.5f, MathF.PI)),
            new(4138, 0xD25, "DimensionalCollapseMiddle", new PtDonutSectorTemplate(7.5f, 12.5f, MathF.PI)),
            new(4139, 0xD25, "DimensionalCollapseOuter", new PtDonutSectorTemplate(12.5f, 17.5f, MathF.PI)),
            new(4140, 0x1054, "DimensionalRip", new CircleTemplate(5)),
            new(4135, 0x1054, "FaithUnmoving", null, "击退20码：落点避开次元裂缝"),
        ], [2188, 4133, 4141, 4142, 4136, 4980], [0x1053]),
        new(0x1056, "Ser Charibert", p => new RectangleGeometry(new(0, p.Y, 4.1f), 0, 39, 39),
        [
            new(4146, 0xD25, "Heavensflame", new CircleTemplate(5)),
            new(4149, 0x1056, "AltarPyre", null, "全屏伤害"),
            new(4152, 0x1057, "WhiteKnightsTour", RayRectangleTemplate.Symmetric(4, 40)),
            new(4153, 0x1058, "BlackKnightsTour", RayRectangleTemplate.Symmetric(4, 40)),
        ], [4144, 4145, 4147, 4148, 4150, 4151, 4154, 4155, 4156]),
    ];
}
