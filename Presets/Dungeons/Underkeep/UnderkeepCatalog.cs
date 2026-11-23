using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class UnderkeepCatalog
{
    internal const uint TERRITORY = 1266;
    internal static bool Owns(uint oid) => oid is 0x4791 or 0x47AD or 0x478E;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x4791, "Gargant", p => UnderkeepArenas.Gargant(p.Y),
        [
            new(42547, 0, "ChillingChirp", null, "全屏伤害"),
            new(42544, 0, "Earthsong", null, "全屏伤害"),
            new(42546, 0, "AlmightyRacket", RayRectangleTemplate.Symmetric(30, 30)),
            new(42543, 0, "AerialAmbush", RayRectangleTemplate.Symmetric(15, 30)),
            new(43161, 0, "FoundationalDebris", new CircleTemplate(10)),
            new(43160, 0, "SedimentaryDebris", new CircleTemplate(5), PartyAoe: "分散"),
            new(42548, 0, "TrapJaws", null, "死刑"),
            new(42545, 0, "SphereShatter", null), new(43135, 0, "SphereShatter", null),
        ], [870, 42542]),
        new(0x47AD, "Soldier S0", p => new RectangleGeometry(new(0, p.Y, -182), 0, 31, 31),
        [
            new(42579, 0, "FieldOfScorn", null, "全屏伤害"),
            new(43136, 0, "ThunderousSlash", null, "死刑"),
            new(42573, 0, "OrderedFire", RayRectangleTemplate.Symmetric(8, 55)),
            new(43139, 0, "ElectricExcess", new CircleTemplate(6), PartyAoe: "分散"),
            new(42566, 0, "SectorBisector", null), new(42567, 0, "SectorBisector", null),
        ], [870, 42576, 42562, 42563, 42568, 43163, 42564, 42569, 43164, 42565, 42572, 42574, 42575, 42570]),
        new(0x478E, "Valia Pira", p => new RectangleGeometry(new(0, p.Y, -331), 0, 35, 35),
        [
            new(42525, 0, "EntropicSphere", null, "全屏伤害"),
            new(43130, 0, "Electray", RayRectangleTemplate.Symmetric(9, 40)),
            new(42521, 0, "ConcurrentField", new FanTemplate(26, 50 * MathF.PI / 180)),
            new(42738, 0, "NeutralizeFrontLines", new FanTemplate(30, MathF.PI)),
            new(42524, 0, "HyperchargedLight", new CircleTemplate(5), PartyAoe: "分散"),
            new(42737, 0, "EnforcementRay", null),
        ], [42526, 42513, 42514, 42508, 42509, 42519, 43261, 42739, 42540]),
    ];
}
