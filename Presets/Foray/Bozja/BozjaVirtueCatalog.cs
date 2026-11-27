using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal static class BozjaVirtueCatalog
{
    internal static readonly BozjaDefinition Definition = new("BA3AbsoluteVirtue", "绝对的美德", 0x25DC, 0,
        BaldesionArenas.Virtue(0).Position, BaldesionArenas.Virtue(0),
        [
            new("Meteor", [14233], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("MedusaJavelin", [14235], BozjaRuleKind.Aoe, new FanTemplate(65.4f, 90f * (MathF.PI / 180))),
            new("AuroralWind", [14234], BozjaRuleKind.CircleBait, new CircleTemplate(5)),
            new("ExplosiveImpulse", [14227, 14228], BozjaRuleKind.Aoe, new CircleTemplate(18), GroupedFinish: true),
            new("AernsWynavExplosion", [14676], BozjaRuleKind.TimedCastHint, Hint: "Aerns Wyvnav is enraging!"),
            new("BrightDarkAurora", [14217, 14218, 14230, 14231], BozjaRuleKind.VirtueAurora,
                RayRectangleTemplate.Symmetric(100, 30)),
            new("AstralUmbralRays", [14220, 14221, 14222, 14223], BozjaRuleKind.VirtueRays, new CircleTemplate(8)),
            new("BrightDarkAuroraExplosion", [], BozjaRuleKind.VirtueExplosion, new CircleTemplate(8)),
            new("DarkAuroraTether", [], BozjaRuleKind.Tower, new CircleTemplate(2), EnemyOid: 0x1EAA4A),
            new("BrightAuroraTether", [], BozjaRuleKind.Tower, new CircleTemplate(2), EnemyOid: 0x1EAA49),
            new("BrightDarkAuroraCounter", [14217, 14218, 14230, 14231], BozjaRuleKind.VirtueCounter),
            new("MeteorEnrageCounter", [14703], BozjaRuleKind.VirtueCounter),
        ], PullRadius: float.PositiveInfinity, SampleRadius: BaldesionArenas.VirtueRadius, Territory: 827);
}
