using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class MatoyaCatalog
{
    internal const uint TERRITORY = 933;
    internal static bool Owns(uint oid) => oid is 0x300C or 0x307F or 0x300B;
    internal static GeometryTemplate Breccia(int step) => step switch
    {
        0 => new FanTemplate(6.5f, 3 * MathF.PI / 2),
        1 => new PtDonutSectorTemplate(6.5f, 12.5f, 3 * MathF.PI / 2),
        _ => new PtDonutSectorTemplate(12.5f, 18.5f, 3 * MathF.PI / 2),
    };
    internal static DungeonDefinition[] Create() =>
    [
        new(0x300C, "Mudman", p => new CircleGeometry(new(-180, p.Y, -140), 19.5f),
        [
            new(21631, 0x300C, "HardRock", null, "死刑"),
            new(21633, 0x233C, "Quagmire", new CircleTemplate(6)),
            new(21652, 0x233C, "FallingRock", null, "四人分摊"),
            new(21650, 0x233C, "StoneAge", null, "全屏伤害"),
            new(21646, 0x233C, "BrittleBreccia1", Breccia(0)),
            new(21647, 0x233C, "BrittleBreccia2", Breccia(1)),
            new(21648, 0x233C, "BrittleBreccia3", Breccia(2)),
        ], [872, 21632, 21634, 21639, 21635, 21636, 21637, 21640, 21645, 21649, 21641, 21651]),
        new(0x307F, "Nixie", p => NixieArena(p.Y, false),
        [
            new(22931, 0x233C, "Sputter", new CircleTemplate(6), PartyAoe: "分散"),
        ], [22932, 22933, 22927, 23481, 22928, 22929, 22930, 22920, 22926, 22934, 22921, 22922, 22924, 22923, 22925]),
        new(0x300B, "Mother Porxie", p => new RectangleGeometry(new(0, p.Y, 0), 0, 39, 39),
        [
            new(22803, 0x300B, "TenderLoinVisual", null, "延迟全屏伤害"),
            new(22801, 0x300B, "MincedMeatVisual", null, "延迟死刑"),
            new(22806, 0x300B, "MeatMallet", new CircleTemplate(30)),
            new(22808, 0x233C, "BarbequeCircle", new CircleTemplate(5)),
            new(22807, 0x233C, "BarbequeRect", RayRectangleTemplate.Symmetric(5, 50)),
            new(22822, 0x3052, "Buffet", RayRectangleTemplate.Symmetric(6, 40)),
            new(22814, 0x233C, "MediumRear1", new DonutTemplate(5, 40)),
            new(22815, 0x233C, "MediumRear2", new DonutTemplate(5, 40)),
            new(20045, 0x233C, "NeerDoneWell", new DonutTemplate(5, 40)),
            new(22809, 0x300B, "HuffAndPuff1", null, "靠近击退来源"),
            new(22810, 0x300B, "HuffAndPuffVisual", null),
            new(20020, 0x3052, "Explosion", null),
            new(23331, 0x300B, "Barbeque", null),
        ], [19087, 22804, 22802, 22813, 22820, 22821, 22811, 22812, 22818, 22819]),
    ];

    internal static IGeometryShape NixieArena(float y, bool cloud) => cloud
        ? new RectangleGeometry(new(0, y, -175), 0, 19, 11)
        : new RectangleGeometry(new(0, y, -150), 0, 39, 39);
}
