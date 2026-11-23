using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class SmiletonCatalog
{
    internal const uint TERRITORY = 976;
    internal static bool Owns(uint oid) => oid is 0x34CF or 0x34D1 or 0x34D3;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x34CF, "Face", p => new RectangleGeometry(new(-45, p.Y, -20), 0, 40, 40),
        [
            new(27742, 0x233C, "OffMyLawnKnockback", null, "向读条方向击退8码，遇墙停止"),
            new(26432, 0x233C, "TemperTemper", new CircleTemplate(5), PartyAoe: "分散"),
            new(26435, 0x34CF, "TempersFlare", null, "全屏伤害"),
        ], [26433, 26421, 26430, 26425]),
        new(0x34D1, "Frameworker", p => new CircleGeometry(new(64, p.Y, -112), 20),
        [
            new(26438, 0x34D1, "LeapForward", new CircleTemplate(15)),
            new(26439, 0x34E9, "PrintedLeapForward", new CircleTemplate(15)),
            new(26441, 0x233C, "OmnidimensionalOnslaught", new FanTemplate(40, MathF.PI / 4)),
            new(26437, 0x34D1, "CircularSaw", null, "全屏伤害"),
        ], [870, 26440, 26443, 28092]),
        new(0x34D3, "BigCheese", p => new RectangleGeometry(new(-22, p.Y, -44), 0, 29, 20),
        [
            // The source component uses R6 despite the action comment's R8.
            new(26451, 0x34D3, "ElectricArc", new CircleTemplate(6), PartyAoe: "分摊"),
            new(27698, 0x38CF, "Excavated", new CircleTemplate(8)),
            new(26445, 0x3585, "IronKiss", new CircleTemplate(16)),
            new(26447, 0x34D3, "RightDisassembler", RayRectangleTemplate.Symmetric(10, 30)),
            new(26448, 0x34D3, "LeftDisassembler", RayRectangleTemplate.Symmetric(10, 30)),
            new(26453, 0x233C, "LevelingMissile", new CircleTemplate(6), PartyAoe: "分散"),
            new(26449, 0x34D3, "PiercingMissile", null, "死刑"),
        ], [26444, 27696, 27697, 26446, 26452]),
    ];
}
