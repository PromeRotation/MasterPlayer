using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class BabilCatalog
{
    internal const uint TERRITORY = 969;
    internal static bool Owns(uint oid) => oid is 0x33F7 or 0x33FA or 0x33FD;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x33F7, "Barnabas", p => BabilArenas.BarnabasStarting(p.Y),
        [
            new(25157, 0x33F7, "DynamicPoundMinus", RayRectangleTemplate.Symmetric(6, 40)),
            new(25326, 0x33F7, "DynamicPoundPlus", RayRectangleTemplate.Symmetric(6, 40)),
            new(25158, 0x33F7, "DynamicScraplineMinus", new CircleTemplate(8)),
            new(25328, 0x33F7, "DynamicScraplinePlus", new CircleTemplate(8)),
            new(25327, 0x233C, "ElectromagneticReleaseRect", RayRectangleTemplate.Symmetric(6, 40)),
            new(25329, 0x233C, "ElectromagneticReleaseCircle", new CircleTemplate(8)),
            new(25159, 0x33F7, "GroundAndPound1", RayRectangleTemplate.Symmetric(6, 40)),
            new(25322, 0x33F7, "GroundAndPound2", RayRectangleTemplate.Symmetric(6, 40)),
            new(25323, 0x33F7, "RollingScrapline", new CircleTemplate(8)),
            new(25330, 0x33F8, "Shock", new CircleTemplate(8)),
            new(25324, 0x33F7, "ShockingForce", new CircleTemplate(6), PartyAoe: "分摊"),
        ], [872, 24693, 24694, 25053, 25054, 25325]),
        new(0x33FA, "Lugae", p => new RectangleGeometry(new(221, p.Y, 306), 0, 39, 39),
        [
            new(25333, 0x33FA, "Downpour", null),
            new(25331, 0x33FA, "MagitekChakram", null),
            new(25338, 0x33FA, "ThermalSuppression", null, "全屏伤害"),
            new(25340, 0x33FA, "MagitekRay", RayRectangleTemplate.Symmetric(6, 50)),
            new(25337, 0x33FC, "Explosion", new DungeonCrossTemplate(40, 4)),
            new(25335, 0x233C, "SurfaceMissile", new CircleTemplate(6)),
        ], [872, 25336, 25334, 25332]),
        new(0x33FD, "Anima", p => AnimaArena(p.Y, false),
        [
            new(25343, 0x233C, "PhantomPain", RayRectangleTemplate.Symmetric(20, 20)),
            new(24168, 0x233C, "PaterPatriae", RayRectangleTemplate.Symmetric(8, 60)),
            new(25357, 0x3401, "CharnelClaw", RayRectangleTemplate.Symmetric(5, 40)),
            new(25352, 0x233C, "EruptingPain", new CircleTemplate(6), PartyAoe: "分散"),
            new(25356, 0x3401, "ObliviatingClawSpawn", new CircleTemplate(3)),
            new(25359, 0x3400, "Oblivion", null, "全屏伤害"),
            new(25344, 0x33FD, "MegaGraviton", null, "全屏伤害"),
            new(25358, 0x233C, "CoffinScratchFirst", null),
        ], [25341, 25345, 26229, 25347, 25348, 25349, 21239, 25353, 23929, 25342, 25354, 25355,
            23697, 23872, 25346, 25350, 21182, 27228, 25351]),
    ];

    internal static IGeometryShape AnimaArena(float y, bool lower) => new RectangleGeometry(new(0, y, lower ? -400 : -180), 0, 39, 39);

    internal static Vector3 MagneticLanding(Vector3 center, Vector3 position, bool rectangle, bool pull)
    {
        var delta = position - center; delta.Y = 0;
        if (rectangle)
        {
            if (delta.LengthSquared() > 900) return position;
            var distance = pull ? Math.Min(9, MathF.Abs(delta.X)) : 9;
            var side = delta.X < 0 ? -1 : 1;
            return position + Vector3.UnitX * distance * side * (pull ? -1 : 1);
        }
        var length = delta.Length();
        return length > .000001f ? position + delta / length * (pull ? -Math.Min(5, length) : 5) : position;
    }
}
