using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class MistwakeCatalog
{
    internal const uint TERRITORY = 1314;
    internal static bool Owns(uint oid) => oid is 0x4841 or 0x4A77 or 0x4A65;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x4841, "Treno Catoblepas", p => MistwakeRocks.Build(p.Y, new HashSet<int>()),
        [
            new(43327, 0, "Earthquake", null, "全屏伤害"),
            new(43329, 0, "ThunderIII", null),
            new(43330, 0, "BedevilingLight", null),
            new(43332, 0, "ThunderIIAOE", new CircleTemplate(5)),
            new(43333, 0, "ThunderIISpread", new CircleTemplate(5), PartyAoe: "分散"),
            new(43335, 0, "Petribreath", new FanTemplate(30, MathF.PI * 2 / 3)),
        ], [43328, 43331, 43334, 44825]),
        new(0x4A77, "Amdusias", p => MistwakeArenas.Amdusias(p.Y),
        [
            new(45337, 0, "ThunderclapConcerto", new FanTemplate(40, MathF.PI * 5 / 3)),
            new(45342, 0, "ThunderclapConcerto", new FanTemplate(40, MathF.PI * 5 / 3)),
            new(45345, 0, "BioII", null, "全屏伤害"),
            new(45351, 0, "ThunderIV", null, "全屏伤害"),
            new(45356, 0, "Shockbolt", null, "死刑"),
            new(45343, 0, "Thunder", new CircleTemplate(5), PartyAoe: "分散"),
            new(45353, 0, "ThunderIII", new CircleTemplate(6), PartyAoe: "分摊"),
            new(45348, 0, "GallopingThunderTelegraph", null),
            new(45349, 0, "Burst", null),
        ], [46839, 45357, 45333, 45335, 45336, 45341, 45344, 45346, 45347, 45350, 45355, 45352, 45354]),
        new(0x4A65, "Thundergust Griffin", p => MistwakeArenas.Griffin(p.Y),
        [
            new(45291, 0, "Thunderspark", null, "全屏伤害"),
            new(45293, 0, "LightningBoltAOE", new CircleTemplate(5)),
            new(46856, 0, "LightningBoltSpread", new CircleTemplate(5), PartyAoe: "分散"),
            new(45296, 0, "Thunderbolt", RayRectangleTemplate.Symmetric(6, 92)),
            new(45297, 0, "Thunderbolt", RayRectangleTemplate.Symmetric(6, 92)),
            new(45298, 0, "Thunderbolt", RayRectangleTemplate.Symmetric(6, 92)),
            new(45305, 0, "GoldenTalons", null, "死刑"),
            new(45302, 0, "Rush", RayRectangleTemplate.Symmetric(10, 40)),
            new(45304, 0, "ElectrogeneticForce", RayRectangleTemplate.Symmetric(18, 40)),
            new(45299, 0, "StormSurge", null),
            new(47022, 0, "ElectrifyingFlight", RayRectangleTemplate.Symmetric(60, 30)),
        ], [45307, 46852, 45294, 45292, 45295, 46944, 46943, 46945, 45301, 45300]),
    ];
}
