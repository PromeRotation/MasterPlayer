using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal static class BozjaArtCatalog
{
    internal const uint TERRITORY = 827;
    internal static readonly BozjaDefinition Definition = new("BA1Art", "亚特", 0x265A, 0,
        BaldesionArenas.Art(0).Position, BaldesionArenas.Art(0),
        [
            new("Thricecull", [14644], BozjaRuleKind.Tankbuster, GlobalHint: true),
            new("AcallamNaSenorach", [14645], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("LegendMythSpinnerCarver", [14632, 14633, 14631, 14634, 14635], BozjaRuleKind.ArtLegend),
            new("DefilersDeserts", [14643], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(8, 35.5f)),
            new("DefilersDesertsPredict", [14642, 14643], BozjaRuleKind.ArtPredict),
            new("Pitfall", [14639], BozjaRuleKind.Aoe, new CircleTemplate(20)),
            new("LegendaryGeasAOE", [14642], BozjaRuleKind.Aoe, new CircleTemplate(8)),
            new("LegendaryGeasStay", [], BozjaRuleKind.ArtStay),
            new("GloryUnearthed", [14636, 14637], BozjaRuleKind.ArtChase, new CircleTemplate(10)),
            new("PiercingDark", [14641], BozjaRuleKind.Spread, new CircleTemplate(6)),
        ], PullRadius: 100, SampleRadius: BaldesionArenas.ArtRadius, Territory: TERRITORY);
}
