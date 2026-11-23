using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class LabyrinthCatalog
{
    internal const uint TERRITORY = 174;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x92B, "Bone Dragon", p => LabyrinthArenas.BoneDragon(p.Y),
        [
            new(749, 0, "Apocalypse", new CircleTemplate(6)),
            new(750, 0, "EvilEye", new FanTemplate(105, MathF.PI * 2 / 3)),
            new(970, 0, "Stone", null, "死刑"),
            new(1828, 0, "Level5Petrify", new FanTemplate(7.8f, MathF.PI / 2)),
        ], [1461, 745, 736, 341, 735, 746, 751]),
        new(0x92E, "Thanatos", p => new CircleGeometry(new Vector3(440, p.Y, 280), 30),
        [
            new(758, 0, "BlackCloud", new CircleTemplate(6)),
            new(760, 0, "Cloudscourge", new CircleTemplate(6)),
            new(1829, 0, "VoidFireII", new CircleTemplate(5)),
            new(1759, 0, "AstralLight", new CircleTemplate(6.8f)),
        ], [1461, 759, 762, 763, 1459, 1661, 489]),
        new(0x932, "King Behemoth", p => new CircleGeometry(new Vector3(-110, p.Y, -380), 35),
        [
            new(1756, 0, "EclipticMeteor", null),
            new(1789, 0, "SelfDestruct", new CircleTemplate(8.4f)),
            new(1752, 0, "CharybdisAOE", null),
        ], [1461, 768, 1785, 1787, 769, 1751, 766]),
        new(0x938, "Phlegethon", p => new CircleGeometry(new Vector3(-110, p.Y, 180), 35),
        [
            new(1741, 0, "MegiddoFlame2", new CircleTemplate(3)),
            new(1742, 0, "MegiddoFlame3", new CircleTemplate(4)),
            new(1743, 0, "MegiddoFlame4", new CircleTemplate(5)),
            new(1744, 0, "MegiddoFlame5", new CircleTemplate(6)),
            new(1780, 0, "MoonfallSlash", new FanTemplate(15, MathF.PI * 2 / 3)),
            new(1736, 0, "VacuumSlash2", new FanTemplate(80, MathF.PI / 4)),
            new(1730, 0, "AncientFlare1", new CircleTemplate(35)),
        ], [1461, 1459, 1725, 1733, 1734, 1737, 1739, 1738, 1735, 1748, 1745, 1732, 1785, 610, 1731]),
    ];
}
