using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AlzadaalCatalog
{
    internal const uint TERRITORY = 1050;
    internal static bool Owns(uint oid) => oid is 0x3879 or 0x386C or 0x36C1;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x3879, "Ambujam", p => AlzadaalArenas.Ambujam(p.Y),
        [
            new(28512, 0x3879, "BigWave", null, "全屏伤害"),
            new(29556, 0x233C, "CorrosiveFountain", new CircleTemplate(8)),
            new(29467, 0x233C, "ToxicFountain", new CircleTemplate(8)),
            new(29158, 0x233C, "CorrosiveVenom", null),
            new(28508, 0x233C, "ToxinShower", null),
        ], [872, 29466, 28501, 28505, 29157, 28507, 28502, 28506]),
        new(0x386C, "ArmoredChariot", p => new RectangleGeometry(new(0, p.Y, -182), 0, 39, 39),
        [
            new(28441, 0x386C, "ArticulatedBits", null),
            new(28446, 0x386C, "DiffusionRay", null, "全屏伤害"),
            new(29555, 0x233C, "GravitonCannon", new CircleTemplate(6), PartyAoe: "分散"),
            new(28447, 0x386C, "RailCannon", null, "死刑"),
        ], [29132, 28442, 28443, 28444, 28445, 28454, 28455, 28448, 28449, 28450, 28451, 28452, 28453, 28456, 28457]),
        new(0x36C1, "Kapikulu", p => new RectangleGeometry(new(110, p.Y, -68), 0, 39, 49),
        [
            new(28520, 0x36C1, "BastingBlade", RayRectangleTemplate.Symmetric(15, 60)),
            new(28528, 0x36C1, "BillowingBolts", null, "全屏伤害"),
            new(28530, 0x36C1, "CrewelSlice", null, "死刑"),
            new(28527, 0x233C, "MagnitudeOpus", new CircleTemplate(6), PartyAoe: "分摊"),
            new(28523, 0x233C, "ManaExplosion", null),
            new(28519, 0x233C, "Traps", RayRectangleTemplate.Symmetric(6, 6)),
            new(28529, 0x233C, "BorderChange", RayRectangleTemplate.Symmetric(40, 5)),
            new(28525, 0x233C, "RotaryGale", new CircleTemplate(5), PartyAoe: "分散"),
            new(28515, 0x36C1, "SpinOut", null, "施加旋转状态"),
        ], [870, 28526, 28522, 28516, 28517, 28518, 28514, 28521, 28524]),
    ];
}
