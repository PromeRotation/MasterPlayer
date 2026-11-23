using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class SkydeepCatalog
{
    internal const uint TERRITORY = 1194;
    internal static bool Owns(uint oid) => oid is 0x41D3 or 0x4184 or 0x41C7;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x41D3, "FeatherRay", p => new RectangleGeometry(new(-105, p.Y, -160), 0, 31, 31),
        [
            new(36739, 0, "Immersion", null, "全屏伤害"),
            new(36728, 0, "WorrisomeWave", new FanTemplate(24, MathF.PI / 6)),
            new(36733, 0, "HydroRing", null),
            new(36732, 0, "BlowingBubbles", null),
            new(38787, 0, "TroubleBubbles", null),
            new(36736, 0, "RollingCurrentWest", null),
            new(36737, 0, "RollingCurrentEast", null),
            new(36738, 0, "Burst", null),
        ], [872, 36727, 36729, 36734, 36735, 38185]),
        new(0x4184, "Firearms", p => new RectangleGeometry(new(-85, p.Y, -155), 0, 49, 49),
        [
            new(36448, 0, "DynamicDominance", null, "全屏伤害"),
            new(36445, 0, "ThunderlightBurst", new CircleTemplate(35)),
            new(38581, 0, "ThunderlightBurst1", RayRectangleTemplate.Symmetric(8, 42)),
            new(38582, 0, "ThunderlightBurst2", RayRectangleTemplate.Symmetric(8, 49)),
            new(38583, 0, "ThunderlightBurst3", RayRectangleTemplate.Symmetric(8, 35)),
            new(38584, 0, "ThunderlightBurst4", RayRectangleTemplate.Symmetric(8, 36)),
            new(38660, 0, "Artillery1", RayRectangleTemplate.Symmetric(10, 10)),
            new(38661, 0, "Artillery2", RayRectangleTemplate.Symmetric(10, 10)),
            new(38662, 0, "Artillery3", RayRectangleTemplate.Symmetric(10, 10)),
            new(38663, 0, "Artillery4", RayRectangleTemplate.Symmetric(10, 10)),
            new(36447, 0, "Pummel", null, "单体伤害"),
            new(36450, 0, "ThunderlightFlurry", new CircleTemplate(6), PartyAoe: "分散"),
        ], [872, 36451, 39139, 36443, 36442, 39000]),
        new(0x41C7, "Maulskull", p => new RectangleGeometry(new(100, p.Y, -430), 0, 40, 40),
        [
            new(36670, 0, "Stonecarver1", RayRectangleTemplate.Symmetric(20, 40)),
            new(36671, 0, "Stonecarver2", RayRectangleTemplate.Symmetric(20, 40)),
            new(36696, 0, "Stonecarver3", RayRectangleTemplate.Symmetric(20, 40)),
            new(36697, 0, "Stonecarver4", RayRectangleTemplate.Symmetric(20, 40)),
            new(36677, 0, "Impact1", null),
            new(36667, 0, "Impact2", null),
            new(36707, 0, "Impact3", null),
            new(36676, 0, "Skullcrush1", new CircleTemplate(10)),
            new(36666, 0, "Skullcrush2", new CircleTemplate(10)),
            new(36706, 0, "ColossalImpact", new CircleTemplate(10)),
            new(36709, 0, "DestructiveHeat", new CircleTemplate(6), PartyAoe: "分散"),
            new(36679, 0, "MaulworkFirstCenter", null),
            new(36680, 0, "MaulworkSecondCenter", null),
            new(36681, 0, "MaulworkFirstSides", null),
            new(36682, 0, "MaulworkSecondSides", null),
            new(36684, 0, "ShatterCenter", null),
            new(36685, 0, "ShatterLeft", null),
            new(36686, 0, "ShatterRight", null),
            new(36683, 0, "Landing", new CircleTemplate(8)),
            new(36688, 0, "DeepThunder1", null),
            new(36689, 0, "DeepThunder2", null),
            new(39122, 0, "WroughtFire", null),
            new(36710, 0, "BuildingHeat", new CircleTemplate(6), PartyAoe: "分摊"),
            new(36711, 0, "Ashlayer", null),
        ], [36678, 36668, 36669, 36672, 36673, 36699, 36700, 36674, 36675, 38664, 36708,
            36687, 36691, 36692, 36690, 36693, 36694, 36695, 39121, 36704, 36705, 36712]),
    ];
}
