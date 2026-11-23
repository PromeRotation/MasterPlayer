using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class TwinningCatalog
{
    internal const uint TERRITORY = 840;
    internal static bool Owns(uint oid) => oid is 0x27D1 or 0x2803 or 0x2805;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x27D1, "Alpha Zaghnal", p => new RectangleGeometry(new(200, p.Y, 285), 0, 39, 39),
        [
            new(15717, 0x27D1, "Augurium", new FanTemplate(12, MathF.PI * 2 / 3)),
            new(15716, 0x27D1, "BeastlyRoar", null, "全屏伤害"),
            new(15715, 0x27D1, "ChargeEradicated", null, "四人分摊"),
        ], [870, 6497, 15712, 15713, 15718, 15719, 15720, 15714, 15724, 15711, 15721, 15722, 16842]),
        new(0x2803, "Mithridates", p => TwinningArenas.Mithridates(p.Y),
        [
            new(15853, 0x2803, "ThunderBeam", null, "死刑"),
            new(15852, 0x233C, "Laserblade", RayRectangleTemplate.Symmetric(8, 50)),
            new(15855, 0x233C, "AllaganThunder", new CircleTemplate(5), PartyAoe: "分散"),
        ], [870, 15856, 15857, 15851, 15854]),
        new(0x2805, "The Tycoon", p => TwinningArenas.Tycoon(p.Y),
        [
            new(15866, 0x233C, "HighGravity", new CircleTemplate(8)),
            new(15867, 0x2805, "RailCannon", null, "死刑"),
            new(17306, 0x233C, "ShatteredCrystal", new CircleTemplate(5), PartyAoe: "分散"),
            new(15868, 0x2805, "HighTensionDischarger", null, "全屏伤害"),
        ], [15869, 15864, 15862, 15863, 15859, 15861, 15858, 15865, 15884]),
    ];
}
