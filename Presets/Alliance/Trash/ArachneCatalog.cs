using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal static class ArachneCatalog
{
    internal static readonly AllianceArenaGeometry Ground = (AllianceArenaGeometry)ArachneArenas.Ground(0);
    internal static readonly AllianceArenaGeometry Web = (AllianceArenaGeometry)ArachneArenas.Web(0);
    internal static readonly BozjaDefinition Definition = new("A21ArachneEve", "阿剌克涅", 0x1637, 0,
        ArachneArenas.GroundCenter, Ground,
        [
            new("SpiderWeb", [], BozjaRuleKind.ArachneArena),
            new("DarkSpike", [6179], BozjaRuleKind.Tankbuster, GlobalHint: true),
            new("SilkenSpray", [6180], BozjaRuleKind.Aoe, new FanTemplate(24.5f, MathF.PI / 3)),
            new("ShadowBurst", [6200], BozjaRuleKind.Stack, new CircleTemplate(6), MinStackSize: 8),
            new("SpiderThread", [6201], BozjaRuleKind.Spread, new CircleTemplate(6)),
            new("FrondAffeared", [6202], BozjaRuleKind.CastGaze),
            new("TheWidowsEmbrace", [6280], BozjaRuleKind.AllianceKnockback,
                KnockbackDistance: 18, KnockbackPull: true, KnockbackStopAtWall: true),
            new("TheWidowsKiss", [6210], BozjaRuleKind.AllianceKnockback,
                KnockbackDistance: 4, KnockbackPull: true, KnockbackStopAtWall: true),
            new("Pitfall", [6181], BozjaRuleKind.Aoe, new CircleTemplate(20)),
            new("Tremblor", [6199, 6198, 6197], BozjaRuleKind.Earthbreaker),
        ], PullRadius: float.PositiveInfinity, SampleRadius: ArachneArenas.GroundRadius, Territory: 556);
}
