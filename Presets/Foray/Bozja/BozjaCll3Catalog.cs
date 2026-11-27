using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal static class BozjaCll3Catalog
{
    internal static readonly IGeometryShape Starting = CastrumLacusArenas.Cll3Starting(0);
    internal static readonly IGeometryShape Reduced = CastrumLacusArenas.Cll3Default(0);
    internal static readonly BozjaDefinition Definition = new("CLL3Adrammelech", "阿德拉梅里克",
        0x2E01, 0, Starting.Position, Starting,
        [
            new("ArenaChange", [20374], BozjaRuleKind.Cll3Arena, new DonutTemplate(25, 30)),
            new("HolyIV", [20374], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("WaterIV1", [20556], BozjaRuleKind.Cll3Water),
            new("WaterIV3", [20356], BozjaRuleKind.Cll3Water),
            new("BlizzardIV", [21595, 20555], BozjaRuleKind.Cll3StayMove),
            new("FireIV", [21596, 20554], BozjaRuleKind.Cll3StayMove),
            new("Flare", [20373], BozjaRuleKind.Tankbuster, GlobalHint: true),
            new("BurstIITornado", [20363, 20368], BozjaRuleKind.Aoe, new CircleTemplate(6), GroupedFinish: true),
            new("Shock", [20366], BozjaRuleKind.Aoe, new CircleTemplate(35)),
            new("WarpedLight", [20365, 20558, 20559, 20560, 20561, 20562], BozjaRuleKind.Charge,
                RayRectangleTemplate.Symmetric(3, 0), GroupedFinish: true),
            new("ThunderIV", [20351, 20357], BozjaRuleKind.Aoe, new CircleTemplate(18), GroupedFinish: true),
            new("AeroIV", [20352, 20358], BozjaRuleKind.Aoe, new DonutTemplate(15, 30), GroupedFinish: true),
            new("Twister", [], BozjaRuleKind.Pheromones, new BozjaCapsuleTemplate(6.5f, 5), EnemyOid: 0x2E09),
            new("StoneIV", [20353, 20354, 20355, 20359, 20360, 20361], BozjaRuleKind.Earthbreaker),
        ], PullRadius: float.PositiveInfinity, SampleRadius: CastrumLacusArenas.Cll3StartingRadius);
}
