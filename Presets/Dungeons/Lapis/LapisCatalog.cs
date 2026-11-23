using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class LapisCatalog
{
    internal const uint TERRITORY = 1097;
    internal static bool Owns(uint oid) => oid is 0x3D56 or 0x3CFE or 0x3971 or 0x3AE2;
    internal static bool IsCarriage(uint oid) => oid is 0x3D56 or 0x3CE2 or 0x3E9F;
    internal static uint StunAction(uint job) => job switch
    {
        1 or 3 or 19 or 21 or 32 or 37 => 7540,
        2 or 4 or 20 or 22 or 29 or 30 or 34 or 39 or 41 => 7863,
        _ => 0,
    };
    internal static IGeometryShape ShrinkRing(Vector3 center)
    {
        Vector2[] outer = [new(-30, -30), new(30, -30), new(30, 30), new(-30, 30)];
        Vector2[] inner = [new(-20, -20), new(20, -20), new(20, 20), new(-20, 20)];
        var triangles = new List<Vector2[]>();
        for (var i = 0; i < 4; ++i)
        {
            var j = (i + 1) % 4;
            triangles.Add([outer[i], outer[j], inner[j]]);
            triangles.Add([outer[i], inner[j], inner[i]]);
        }
        return new PolygonRegionGeometry(center, [outer], [inner], triangles.ToArray());
    }
    internal static DungeonDefinition[] Create() =>
    [
        new(0x3D56, "AlbusGriffin", p => new RectangleGeometry(new(47, p.Y, -570.5f), 0, 17, 23),
        [
            new(32535, 0, "TransonicBlast", new FanTemplate(9, MathF.PI / 2)),
            new(32785, 0, "WindsOfWinter", null, "眩晕阻止全屏伤害"),
            new(32786, 0, "Freefall", new CircleTemplate(8)),
            new(32787, 0, "GoldenTalons", new FanTemplate(8, MathF.PI / 2)),
        ], [872, 870], [0x3CE2, 0x3E9F]),
        new(0x3CFE, "Albion", p => new RectangleGeometry(new(24, p.Y, -744), 0, 39, 39),
        [
            new(31365, 0, "AlbionsEmbrace", null, "死刑"),
            new(32813, 0, "RightSlam", RayRectangleTemplate.Symmetric(20, 80)),
            new(32814, 0, "LeftSlam", RayRectangleTemplate.Symmetric(20, 80)),
            new(31359, 0, "KnockOnIce", new CircleTemplate(5)),
            new(31361, 0, "Icebreaker", new CircleTemplate(17)),
            new(32783, 0, "IcyThroes1", new CircleTemplate(6)),
            new(32697, 0, "IcyThroes2", new CircleTemplate(6)),
            new(31363, 0, "IcyThroesSpread", new CircleTemplate(6), PartyAoe: "分散"),
            new(31364, 0, "RoarOfAlbion", null, "躲在冰块后"),
        ], [872, 32812, 31356, 31357, 31358, 31362]),
        new(0x3971, "GalateaMagna", p => LapisArenas.Galatea(p.Y),
        [
            new(32622, 0, "WaningCycle1", new DonutTemplate(10, 40)),
            new(32624, 0, "WaningCycle2", new CircleTemplate(10)),
            new(31377, 0, "WaxingCycle1", new CircleTemplate(10)),
            new(31379, 0, "WaxingCycle2", new DonutTemplate(10, 40)),
            new(31386, 0, "SoulScythe", new CircleTemplate(18)),
            new(32703, 0, "ScarecrowChase", null),
        ], [870, 32625, 32623, 31378, 31387, 31389, 31383, 31384, 31385]),
        new(0x3AE2, "Cagnazzo", p => new RectangleGeometry(new(-250, p.Y, 130), 0, 59, 59),
        [
            new(31139, 0, "StygianDeluge", null, "全屏伤害"),
            new(31120, 0, "Antediluvian", new CircleTemplate(15)),
            new(31122, 0, "BodySlamKB", null, "击退10码"),
            new(31123, 0, "BodySlam", new CircleTemplate(8)),
            new(32693, 0, "HydraulicRamTelegraph", null),
            new(32695, 0, "HydrobombTelegraph", null),
            new(31136, 0, "Hydrovent", new CircleTemplate(6)),
            new(32691, 0, "VoidMiasma", new FanTemplate(50, MathF.PI / 6)),
            new(31113, 0, "Lifescleaver", new FanTemplate(50, MathF.PI / 6)),
            new(31118, 0, "VoidTorrent", null, "死刑"),
            new(31111, 0, "Voidcleaver", null, "全屏伤害"),
        ], [870, 31131, 31119, 31121, 32692, 32694, 32696, 31126, 31375, 31376,
            31130, 31132, 31134, 31135, 31137, 31138, 31110, 31112]),
    ];
}
