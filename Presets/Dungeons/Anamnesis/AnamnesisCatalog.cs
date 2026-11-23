using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AnamnesisCatalog
{
    internal const uint TERRITORY = 898;
    internal static bool Owns(uint oid) => oid is 0x2CD9 or 0x2CDA or 0x2CFE or 0x2CFF;
    internal static IGeometryShape Default(float y) => new RectangleGeometry(new(0, y, -450), 0, 31, 39);
    internal static IGeometryShape Narrow(float y) => new RectangleGeometry(new(0, y, -450), 0, 16, 39);
    internal static DungeonDefinition[] Create() =>
    [
        new(0x2CD9, "Unknown", p => AnamnesisArenas.Unknown(p.Y),
        [
            new(19305, 0x2CD9, "FetidFang", null, "死刑"),
            new(19314, 0x2CDA, "FetidFang", null, "死刑"),
            new(19306, 0x2CD9, "Inscrutability", null, "全屏伤害"),
            new(19315, 0x2CDA, "Inscrutability", null, "全屏伤害"),
            new(19316, 0x2CDA, "Clearout", new FanTemplate(9, 2 * MathF.PI / 3)),
            new(19307, 0x2CD9, "Clearout", new FanTemplate(9, 2 * MathF.PI / 3)),
            new(19308, 0x2CD9, "Setback", new FanTemplate(9, 2 * MathF.PI / 3)),
            new(19317, 0x2CDA, "Setback", new FanTemplate(9, 2 * MathF.PI / 3)),
            new(19310, 0x2CDB, "Explosion", new CircleTemplate(8)),
            new(20006, 0x2CD9, "LuminousRay", RayRectangleTemplate.Symmetric(8, 50)),
            new(20007, 0x2CDA, "LuminousRay", RayRectangleTemplate.Symmetric(8, 50)),
        ], [870, 19309, 20005, 19311, 20043, 19319, 19320, 19312, 19313, 19321, 19322], [0x2CDA]),
        new(0x2CFE, "Kyklops", p => new RectangleGeometry(new(20, p.Y, -80.1f), 0, 29, 28.8f),
        [
            new(19288, 0x2CFE, "TheFinalVerse", null, "全屏伤害"),
            new(19287, 0x2CFE, "EyeOfTheCyclone", new DonutTemplate(8, 25)),
            new(19286, 0x2CFE, "RagingGlower", RayRectangleTemplate.Symmetric(6, 45)),
            new(19284, 0x2CFE, "MinaSwipe2000", new FanTemplate(12, 2 * MathF.PI / 3)),
            new(19285, 0x2CFE, "MinaSwing2000", new CircleTemplate(12)),
            new(19296, 0x233C, "OpenHearth", null, "分摊"),
            new(19295, 0x233C, "WanderersPyre", new CircleTemplate(5), PartyAoe: "分散"),
        ], [19283, 19289, 19290, 19293, 19294, 19292, 19291]),
        new(0x2CFF, "Rukshs Dheem", p => Default(p.Y),
        [
            new(19340, 0x2CFF, "Bonebreaker", null, "死刑"),
            new(19324, 0x233C, "SeabedCeremony", null, "全屏伤害"),
            new(19336, 0x2D01, "Seafoam", null, "全屏伤害"),
            new(19326, 0x233C, "FallingWater", new CircleTemplate(8), PartyAoe: "分散"),
            new(19328, 0x233C, "FlyingFount", null, "分摊"),
            new(19330, 0x233C, "CommandCurrent", new FanTemplate(40, MathF.PI / 6)),
            new(19337, 0x2D01, "CoralTrident", new FanTemplate(6, MathF.PI / 2)),
            new(19339, 0x2CFF, "RisingTide", new DungeonCrossTemplate(50, 3)),
        ], [870, 19331, 19323, 19332, 19333, 19334, 19335, 13268, 13269, 19325, 19338, 19327, 19329]),
    ];
}
