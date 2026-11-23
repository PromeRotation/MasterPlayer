using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class QueenEternalCatalog
{
    internal const uint TERRITORY = 1202;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x41C0, "QueenEternal", p => new RectangleGeometry(new(100, p.Y, 100), 0, 40, 40),
        [
            new(36638, 0, "LegitimateForceLL", null),
            new(36639, 0, "LegitimateForceLR", null),
            new(36640, 0, "LegitimateForceRR", null),
            new(36641, 0, "LegitimateForceRL", null),
            new(36602, 0, "ProsecutionOfWar", null, "死刑"),
            new(36606, 0, "VirtualShift1", null, "全屏伤害"),
            new(36607, 0, "VirtualShift2", null, "全屏伤害"),
            new(36608, 0, "VirtualShift3", null, "全屏伤害"),
            new(36603, 0, "RoyalDomain", null, "全屏伤害"),
            new(39531, 0, "AbsoluteAuthorityRaidwide", null, "全屏伤害"),
            new(36633, 0, "BrutalCrown", new DonutTemplate(5, 60)),
            new(40059, 0, "DynasticDiadem", new DonutTemplate(6, 70)),
            new(36632, 0, "WaltzOfTheRegalia", RayRectangleTemplate.Symmetric(4, 14)),
            new(36622, 0, "AbsoluteAuthorityCircle", new CircleTemplate(8)),
            new(36647, 0, "RoyalBanishment", new FanTemplate(100, MathF.PI / 6)),
            new(36610, 0, "Downburst", null, "全屏伤害"),
            new(36612, 0, "PowerfulGust", null, "全屏伤害"),
        ],
        [36656, 36642, 36643, 36604, 36605, 36657, 36658, 36659, 36629, 36631, 36630, 36634,
            36609, 36611, 36613, 36614, 36615, 36616, 36617, 36618, 36619, 36620, 36621,
            36627, 36628, 36624, 39518, 39519, 39520, 36636, 36637, 39134, 40058, 36644, 36645]),
    ];
}
