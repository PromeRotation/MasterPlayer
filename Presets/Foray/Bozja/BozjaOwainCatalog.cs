using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal static class BozjaOwainCatalog
{
    internal static readonly BozjaDefinition Definition = new("BA1Owain", "欧文", 0x265D, 0,
        BaldesionArenas.Owain(0).Position, BaldesionArenas.Owain(0),
        [
            new("Thricecull", [14661], BozjaRuleKind.Tankbuster, GlobalHint: true),
            new("AcallamNaSenorach", [14662], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("ElementalMagicks", [14650, 14651, 14652, 14653], BozjaRuleKind.OwainElemental, new CircleTemplate(13)),
            new("PiercingLight1", [14655], BozjaRuleKind.Spread, new CircleTemplate(6)),
            new("PiercingLight2", [14660], BozjaRuleKind.Spread, new CircleTemplate(6)),
            new("Pitfall", [14669], BozjaRuleKind.Aoe, new CircleTemplate(20)),
            new("Spiritcull", [], BozjaRuleKind.OwainSpirit, new CircleTemplate(1.5f)),
            new("IvoryPalm", [], BozjaRuleKind.OwainGaze),
            new("IvoryPalmExplosion", [14658], BozjaRuleKind.OwainCastHint),
            new("EurekanAero", [14657], BozjaRuleKind.OwainCleave, new FanTemplate(6, 2 * (60f * (MathF.PI / 180f)))),
            new("LegendaryImbas", [14656], BozjaRuleKind.Raidwide, GlobalHint: true),
        ], PullRadius: 100, SampleRadius: BaldesionArenas.OwainRadius, Territory: 827);
}
