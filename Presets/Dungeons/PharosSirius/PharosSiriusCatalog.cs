using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class PharosSiriusCatalog
{
    internal const uint TERRITORY = 160;

    internal static DungeonDefinition[] Create() =>
    [
        new(0x8FB, "Symond the Unsinkable", p => new CircleGeometry(new(42, p.Y, -56), 20),
        [
            new(1498, 0x8FB, "CorruptingShot", RayRectangleTemplate.Symmetric(3, 50)),
            new(1499, 0x8FC, "CorruptingSpit", new CircleTemplate(6)),
            new(1542, 0x1B2, "CrystallineShower", new CircleTemplate(9)),
            new(1502, 0x8FB, "GigaSlash", new CircleTemplate(20)),
        ], [1501]),
        new(0x8F4, "Zu", p => PharosSiriusArenas.Zu(p.Y),
        [
            new(1491, 0x8F4, "BreathWing", null, "全屏伤害"),
            new(1543, 0x8F6, "PulletBreathWing", null, "全屏伤害"),
            new(1494, 0x8F4, "SonicStorm", new CircleTemplate(6)),
        ], []),
        new(0x918, "Tyrant", p => PharosSiriusArenas.Tyrant(p.Y),
        [
            new(1672, 0x918, "AeroBlast", null, "全屏伤害"),
            new(1674, 0x918, "Bombination", new CircleTemplate(6)),
        ], []),
        new(0x8EF, "Siren", p => new CircleGeometry(new(0, p.Y, 0), 24),
        [
            new(1487, 0x8EF, "DeathlyCadenza", new DonutTemplate(4, 30)),
            new(1484, 0x8EF, "FeralLunge", RayRectangleTemplate.Symmetric(12, 50)),
            new(1483, 0x8EF, "DeathlyVerse", null, "治疗至满血解除海妖之歌"),
            new(1486, 0x8EF, "SongOfTorment", null, "驱散坦克的出血"),
            new(1485, 0x8EF, "LunaticVoice", null, "全屏伤害"),
            new(1658, 0x8F0, "Wallop", RayRectangleTemplate.Symmetric(3, 3)),
        ], []),
    ];
}
