using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AmdaporCatalog
{
    internal const uint TERRITORY = 167;

    internal static DungeonDefinition[] Create() =>
    [
        new(0x5D1, "Psycheflayer", p => new CircleGeometry(new(-29, p.Y, 0), 40),
        [
            new(1084, 0x5D1, "VoidFire", new CircleTemplate(5)),
            new(1085, 0x5D0, "VoidThunder", null, "可打断死刑"),
            new(1078, 0x5D0, "MindMelt", null, "全屏伤害"),
            new(1079, 0x5D0, "Canker", null, "可打断减益"),
            new(1086, 0x5D2, "Rockslide", RayRectangleTemplate.Symmetric(8, 12.76f)),
            new(1088, 0x5D3, "Obliterate", null, "全屏伤害"),
            new(1087, 0x5D4, "Plaincracker", new CircleTemplate(30.5f)),
        ], [1082, 1083], [0x5D0]),
        new(0x283, "Demon Wall", p => new RectangleGeometry(new(200, p.Y, -131), 0, 20, 42),
        [
            new(1045, 0x19A, "LiquefyCenter", RayRectangleTemplate.Symmetric(8, 50)),
            new(1046, 0x19A, "LiquefySides", RayRectangleTemplate.Symmetric(7, 50)),
            new(1047, 0x283, "Repel", null, "不可免疫击退：前往中央窄带"),
        ], []),
        new(0x5CC, "Anantaboga", p => new RectangleGeometry(new(10, p.Y, 0), 0, 50, 50),
        [
            new(584, 0x5CC, "RottenBreath", new FanTemplate(11.5f, MathF.PI / 2)),
            new(1073, 0x5CC, "TailDrive", new FanTemplate(35.5f, MathF.PI / 2)),
            new(1074, 0x5CC, "ImminentCatastrophe", null, "躲在仍存在的石柱后"),
            new(1077, 0x5CD, "TerrorEye", new CircleTemplate(6)),
        ], []),
    ];
}
