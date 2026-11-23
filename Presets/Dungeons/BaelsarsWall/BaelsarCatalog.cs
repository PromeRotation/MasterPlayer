using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class BaelsarCatalog
{
    internal const uint TERRITORY = 1114;
    internal static bool Owns(uint oid) => oid is 0x1938 or 0x193A or 0x193D;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x1938, "Magitek Predator", p => new RectangleGeometry(new(-174, p.Y, 73), 0, 39, 39),
        [
            new(7346, 0x1938, "MagitekClaw", null, "死刑"),
            new(7347, 0x1938, "MagitekRay", RayRectangleTemplate.Symmetric(6, 42.94f)),
        ], [870, 7349, 7350, 7348]),
        new(0x193A, "Armored Weapon", p => new RectangleGeometry(new(116, p.Y, 0), 0, 39, 39),
        [
            new(7356, 0x193A, "Launcher", null, "全屏伤害"),
            new(7353, 0x193A, "DynamicSensoryJammer", null, "极度警戒即将到期时停止一切动作"),
            new(31469, 0x193A, "DiffractiveLaser", new CircleTemplate(5)),
            new(7355, 0x193A, "DiffractiveLaser", new CircleTemplate(5)),
            new(7360, 0x193B, "AssaultCannon", RayRectangleTemplate.Symmetric(2, 40.94f)),
        ], [7351, 870, 7359, 7352, 7354, 31352, 7358, 7357]),
        new(0x193D, "The Griffin", p => BaelsarArenas.Griffin(p.Y),
        [
            new(7363, 0x193D, "BeakOfTheGriffin", null, "全屏伤害"),
            new(7364, 0x193D, "FlashPowder", null, "背对视线"),
            new(7365, 0x193D, "SanguineBlade", new FanTemplate(41.5f, MathF.PI)),
            new(7362, 0x193D, "ClawOfTheGriffin", null, "死刑"),
            new(7367, 0x193D, "BigBoot", null, "被点名者将被击退15码"),
            new(7372, 0x193F, "Corrosion", new CircleTemplate(9)),
            new(7368, 0x193D, "RestraintCollarVisual", null),
            new(7369, 0x193E, "RestraintCollar", null, "打破拘束项圈"),
        ], [870, 7366, 7361, 7371, 7370]),
    ];
}
