using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class SkallaCatalog
{
    internal const uint TERRITORY = 1172;
    internal static bool Owns(uint oid) => oid is 0x1FAB or 0x1FAC or 0x1FAE;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x1FAB, "Kelpie", p => new RectangleGeometry(new(-220, p.Y, 4), 0, 29, 29),
        [
            new(9808, 0x1FAB, "RisingSeas", null, "全屏伤害"),
            new(9809, 0x1FAB, "HydroPull", null, "定向拉近20码"),
            new(9810, 0x1FAB, "HydroPush", null, "定向击退20码"),
        ], [872, 9807, 9811, 9812, 9813, 9755]),
        new(0x1FAC, "The Old One", p => new CircleGeometry(new(115, p.Y, 4), 20),
        [
            new(9815, 0x1FAC, "MysticLight", new FanTemplate(40, MathF.PI / 3)),
            new(9817, 0x18D6, "MysticFlame", new CircleTemplate(8)),
            new(9818, 0x1FAC, "ShiftingLight", null, "全屏伤害"),
            new(9819, 0x1FAC, "OrderToDetonate", null, "全屏伤害：变身后使用碎岩攻击仆从"),
        ], [29791, 9816, 9824]),
        new(0x1FAE, "Hrodric Poisontongue", p => new CircleGeometry(new(479, p.Y, 4), 20),
        [
            new(9825, 0x1FAE, "RustingClaw", new FanTemplate(17, MathF.PI / 2)),
            new(9827, 0x1FAE, "TailDrive", new FanTemplate(50, 80 * MathF.PI / 180)),
            new(9828, 0x1FAE, "TheSpin", new CircleTemplate(17)),
            new(9829, 0x1FAE, "EyeOfTheFire", null, "背对视线"),
            new(9826, 0x1FAE, "WordsOfWoe", RayRectangleTemplate.Symmetric(6, 65)),
        ], [870, 9830, 9831, 9832, 9833]),
    ];
}
