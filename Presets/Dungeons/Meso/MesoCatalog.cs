using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class MesoCatalog
{
    internal const uint TERRITORY = 1292;
    internal static bool Headsman(uint oid) => oid is 0x4890 or 0x4891 or 0x4892 or 0x4893;
    internal static bool Owns(uint oid) => oid is 0x488F or 0x48BE || Headsman(oid);
    internal static DungeonDefinition[] Create() =>
    [
        new(0x488F, "Chirurgeon General", p => new RectangleGeometry(new(270, p.Y, 12), 0, 43, 43),
        [
            new(43798, 0, "MedicineField", null, "全屏伤害"),
            new(43807, 0, "PungentAerosol", null, "全屏伤害"),
            new(43799, 0, "ConcentratedDose", null, "死刑"),
            new(43802, 0, "BiochemicalFront", RayRectangleTemplate.Symmetric(65, 40)),
            new(43806, 0, "SterileSphereSmall", null),
            new(43805, 0, "SterileSphereBig", null),
        ], [44246, 43804, 43797]),
        new(0x4891, "Four Headsmen", p => MesoArenas.Headsmen(p.Y),
        [
            new(43579, 0, "HeadSplittingRoar", null, "全屏伤害"),
            new(43587, 0, "Dismemberment", RayRectangleTemplate.Symmetric(4, 16)),
            new(43596, 0, "ExecutionWheel", new DonutTemplate(4, 9)),
            new(43592, 0, "FlayingFlail", new CircleTemplate(5)),
            new(43595, 0, "ChoppingBlock", new CircleTemplate(6)),
            new(43588, 0, "DeathPenalty", null, "单体伤害"),
            new(44856, 0, "WillBreaker", null, "可打断死刑"),
            new(43589, 0, "RelentlessTorment", null, "死刑"),
        ], [43598, 43599, 43600, 43580, 43577, 43578, 43581, 43582, 43583,
            43586, 43593, 43594, 43591, 43590, 43597, 43584, 43585]),
        new(0x48BE, "Immortal Remains", p => new RectangleGeometry(new(0, p.Y, 0), 0, 40, 40),
        [
            new(43825, 0, "Recollection", null, "全屏伤害"),
            new(43819, 0, "ImpressionKB", null, "全屏伤害"),
            new(43824, 0, "MemoryOfThePyre", null, "死刑"),
            new(43810, 0, "Electray", RayRectangleTemplate.Symmetric(8, 45)),
            new(43818, 0, "Impression", new CircleTemplate(10)),
            new(43813, 0, "KeraunographyVisual", null),
            new(45176, 0, "Keraunography", null),
            new(43811, 0, "BombardmentSmall", null),
            new(43812, 0, "BombardmentBig", null),
        ], [43826, 43809, 43821, 43822, 43814, 43815, 43816, 43817, 43823]),
    ];
}
