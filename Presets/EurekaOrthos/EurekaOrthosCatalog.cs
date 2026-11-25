using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class EurekaOrthosCatalog
{
    internal const uint GANCANAGH = 1099;
    internal const uint CLONING_NODE = 1100;
    internal const uint TIAMAT = 1101;
    internal const uint TWINTANIA = 1102;
    internal const uint MINOTAUR = 1104;
    internal const uint CHIMERA = 1103;
    internal const uint AETURNA = 1105;
    internal const uint PROTO_KALIYA = 1106;
    internal const uint ADMINISTRATOR = 1107;
    internal const uint EXCALIBUR = 1108;
    internal static bool IsTerritory(uint territory) => territory is GANCANAGH or CLONING_NODE or TIAMAT or TWINTANIA or CHIMERA or MINOTAUR or AETURNA or PROTO_KALIYA or ADMINISTRATOR or EXCALIBUR;
    internal static bool Owns(uint territory, uint oid) => territory switch
    {
        GANCANAGH => oid == 0x3D54,
        CLONING_NODE => oid == 0x3E77,
        TIAMAT => oid == 0x3D9A,
        TWINTANIA => oid == 0x3D1D,
        MINOTAUR => oid == 0x3DA1,
        CHIMERA => oid == 0x3D9C,
        AETURNA => oid == 0x3D1B,
        PROTO_KALIYA => oid == 0x3D18,
        ADMINISTRATOR => oid == 0x3D23,
        EXCALIBUR => oid == 0x3CFC,
        _ => false,
    };
    internal static IGeometryShape Arena(uint territory, float y) => territory is GANCANAGH or TIAMAT
        ? new RectangleGeometry(new(-300, y, -300), 0, 39, 39)
        : territory == ADMINISTRATOR ? new RectangleGeometry(new(-300, y, -300), 0, 40, 40)
        : territory == EXCALIBUR ? new CircleGeometry(new(-600, y, -300), 19.5f)
        : territory is TWINTANIA or MINOTAUR or PROTO_KALIYA ? new CircleGeometry(new(-600, y, -300), 20)
        : territory == AETURNA ? new CircleGeometry(new(-300, y, -300), 20)
        : new CircleGeometry(new(-300, y, -300), 19.5f);

    internal static DungeonDefinition[] Create(uint territory) => territory switch
    {
        GANCANAGH => [new(0x3D54, "蜜言妖", p => Arena(territory, p.Y),
        [
            new(31478, 0, "Mandrashock", new CircleTemplate(10)),
            new(32700, 0, "Mandrashock", new CircleTemplate(10)),
            new(31479, 0, "Mandrastorm", new CircleTemplate(20)),
        ], [])],
        CLONING_NODE => [new(0x3E77, "复制系统", p => Arena(territory, p.Y),
        [
            new(32547, 0, "Piercing Laser", RayRectangleTemplate.Symmetric(5, 40)),
            // Only a real CastFinished consumes the event-created breath FIFO.
            new(32864, 0, "Flame Breath", null),
        ], [])],
        TIAMAT => [new(0x3D9A, "提亚马特复制体", p => Arena(territory, p.Y),
        [
            new(31846, 0, "Dark Wyrmwing", RayRectangleTemplate.Symmetric(16, 40)),
            new(31844, 0, "Dark Wyrmtail", RayRectangleTemplate.Symmetric(16, 40)),
            new(31850, 0, "Dark Megaflare", new CircleTemplate(6)),
            new(31847, 0, "Whei Morn", null),
        ], [])],
        TWINTANIA => [new(0x3D1D, "双塔尼亚复制体", p => Arena(territory, p.Y),
        [
            new(31468, 0, "Twister", null),
            new(31471, 0, "Twisting Dive", null),
            new(31463, 0, "Gust", null),
            new(31466, 0, "Meracydian Squall", new CircleTemplate(5)),
            new(31467, 0, "Turbine", null),
        ], [])],
        CHIMERA => [new(0x3D9C, "自控化奇美拉14X", p => Arena(territory, p.Y),
        [
            new(31851, 0, "Songs of Ice and Thunder", null),
            new(31852, 0, "Songs of Thunder and Ice", null),
            new(31855, 0, "Cold Thunder", null),
            new(31856, 0, "Thunderous Cold", null),
            new(31863, 0, "Rightbreathed Cold", new FanTemplate(40, MathF.PI)),
            new(31861, 0, "Leftbreathed Thunder", new FanTemplate(40, MathF.PI)),
        ], [])],
        MINOTAUR => [new(0x3DA1, "自控化弥诺陶洛斯16", p => Arena(territory, p.Y),
        [
            new(31867, 0, "Octuple Swipe", null),
            new(32795, 0, "Bullish Swipe", new FanTemplate(40, MathF.PI / 2)),
            new(31875, 0, "Bullish Swing", new CircleTemplate(13)),
            new(31876, 0, "Disorienting Groan", null),
            new(31874, 0, "Shock", null),
        ], [])],
        AETURNA => [new(0x3D1B, "Aeturna", p => Arena(territory, p.Y),
        [
            new(31445, 0, "Steel Claw", null),
            new(31442, 0, "Ferocity", null),
            new(31436, 0, "Preternatural Turn", new CircleTemplate(15)),
            new(31437, 0, "Preternatural Turn", new DonutTemplate(6, 30)),
            new(31439, 0, "Shatter", null),
            new(31440, 0, "Shatter", null),
            new(31441, 0, "Falling Rock", new CircleTemplate(3)),
            new(31438, 0, "Impact", new CircleTemplate(5)),
            new(31435, 0, "Roar", null, "全屏伤害"),
        ], [])],
        PROTO_KALIYA => [new(0x3D18, "Proto-Kaliya", p => Arena(territory, p.Y),
        [
            new(31427, 0, "Barofield", null),
            new(31429, 0, "Nanospore Jet", null),
            new(32930, 0, "Nerve Gas Ring", null),
            new(31432, 0, "Auto Cannons", null),
            new(32934, 0, "Leftward Nerve Gas", null),
            new(32935, 0, "Rightward Nerve Gas", null),
            new(32933, 0, "Centralized Nerve Gas", null),
            new(31422, 0, "Resonance", null),
        ], [])],
        ADMINISTRATOR => [new(0x3D23, "Administrator", p => Arena(territory, p.Y),
        [
            new(31447, 0, "Peripheral Lasers", null),
            new(31448, 0, "Cross Laser", null),
            new(31451, 0, "Aetherochemical Laser Cone", null),
            new(31452, 0, "Aetherochemical Laser Line", null),
            new(31453, 0, "Aetherochemical Laser Donut", null),
            new(32833, 0, "Aetherochemical Laser Line", null),
            new(32832, 0, "Aetherochemical Laser Cone", null),
            new(31461, 0, "Homing Lasers", null),
            new(31456, 0, "Laserstream", null),
        ], [])],
        EXCALIBUR => [new(0x3CFC, "Excalibur", p => Arena(territory, p.Y),
        [
            new(31339, 0, "Abyssal Slash", null),
            new(31340, 0, "Abyssal Slash", null),
            new(31341, 0, "Abyssal Slash", null),
            new(31342, 0, "Vacuum Slash", null),
            new(32701, 0, "Thermal Divide", null),
            new(31346, 0, "Ice Shoot", null),
            new(31347, 0, "Ice Bloom Circle", null),
            new(31344, 0, "Exflammeus", null),
            new(31328, 0, "Empty Soul's Caliber", null),
            new(31327, 0, "Solid Soul's Caliber", null),
            new(31333, 0, "Caliburni", null),
            new(31353, 0, "Paradoxum", null),
            new(31329, 0, "Flameforge", null),
            new(31330, 0, "Frostforge", null),
            new(31349, 0, "Thermal Divide", null),
            new(31350, 0, "Thermal Divide", null),
            new(31334, 0, "Steelstrike", null),
            new(31335, 0, "Steelstrike", null),
            new(31336, 0, "Steel Flame", null),
            new(31337, 0, "Steel Frost", null),
        ], [])],
        _ => [],
    };
}
