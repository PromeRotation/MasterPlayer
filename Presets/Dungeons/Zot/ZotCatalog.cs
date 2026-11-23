using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ZotCatalog
{
    internal const uint TERRITORY = 952;
    internal static bool Owns(uint oid) => oid is 0x33EE or 0x33EF or 0x33F1 or 0x33F2 or 0x33F3;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x33EE, "Minduruva", p => ZotArenas.Minduruva(p.Y),
        [
            new(25248, 0x33EE, "ManusyaBio", null, "死刑"),
            new(25244, 0x33EE, "Dhrupad", null),
            new(25239, 0, "ManusyaThunderIII", new CircleTemplate(3)),
            new(25240, 0, "ManusyaBioIII", new FanTemplate(40.5f, MathF.PI)),
            new(25238, 0, "ManusyaBlizzardIII", new FanTemplate(40.5f, MathF.PI / 9)),
            new(25237, 0, "ManusyaFireIII", new DonutTemplate(5, 60)),
        ], [870, 25241, 25234, 25233, 25235, 25236, 25699, 25245, 25246, 25247, 25243, 25242, 25371, 25372, 25373]),
        new(0x33EF, "Sanduruva", p => ZotArenas.Sanduruva(p.Y),
        [
            new(25257, 0x33EF, "IsitvaSiddhi", null, "死刑"),
            new(25256, 0x33EF, "PraptiSiddhi", RayRectangleTemplate.Symmetric(4, 40)),
            new(25251, 0x33EF, "PrakamyaSiddhi", new CircleTemplate(5)),
            new(25253, 0x33EF, "ManusyaConfuse", null, "即将施加混乱"),
            new(25255, 0x33EF, "ManusyaStop", null, "即将施加止步"),
            new(25252, 0x33F0, "SphereShatter", null),
        ], [871, 25254, 25250, 25249]),
        new(0x33F1, "Magus Sisters", p => ZotArenas.Sisters(p.Y),
        [
            new(25290, 0x33F3, "ManusyaBio", null, "死刑"),
            new(25280, 0x33F2, "IsitvaSiddhi", null, "死刑"),
            new(25273, 0x33F1, "Samsara", null, "全屏伤害"),
            new(25281, 0x33F3, "Dhrupad", null),
            new(25269, 0, "DeltaThunderIII1", new CircleTemplate(3)),
            new(25270, 0, "DeltaThunderIII2", new CircleTemplate(5)),
            new(25271, 0, "DeltaThunderIII3", RayRectangleTemplate.Symmetric(10, 40)),
            new(25272, 0, "DeltaThunderIII4", new CircleTemplate(5), PartyAoe: "分摊"),
            new(25266, 0, "DeltaBlizzardIII1", new FanTemplate(40.5f, MathF.PI / 9)),
            new(25267, 0, "DeltaBlizzardIII2", RayRectangleTemplate.Symmetric(4, 44)),
            new(25268, 0, "DeltaBlizzardIII3", new CircleTemplate(15)),
            new(25263, 0, "DeltaFireIII1", new DonutTemplate(5, 40)),
            new(25264, 0, "DeltaFireIII2", RayRectangleTemplate.Symmetric(10, 44)),
            new(25265, 0, "DeltaFireIII3", new CircleTemplate(6), PartyAoe: "分散"),
            new(25275, 0x33F2, "PraptiSiddhi", RayRectangleTemplate.Symmetric(4, 40)),
            new(25278, 0x33F2, "PrakamyaSiddhi", new CircleTemplate(5)),
            new(25286, 0, "ManusyaBlizzardIII", new FanTemplate(40.5f, MathF.PI / 9)),
            new(25279, 0x33F0, "SphereShatter", null),
        ], [871, 25254, 25260, 25261, 25262, 25282, 25283, 25284, 25288, 25258, 25287, 25274, 25259, 25289, 25276, 25277, 25285],
            PrimaryAliases: [0x33F2, 0x33F3]),
    ];
}
