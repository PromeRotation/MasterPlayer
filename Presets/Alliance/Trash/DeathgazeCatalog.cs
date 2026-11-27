using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal static class DeathgazeCatalog
{
    internal static readonly BozjaDefinition Definition = new("A31DeathgazeHollow", "虚空死亡凝视", 0x190F, 0,
        new(300, 0, 410), new RectangleGeometry(new(300, 0, 410), 0, 60, 30),
        [
            new("DarkII", [7318], BozjaRuleKind.Aoe, new FanTemplate(50, MathF.PI / 3)),
            new("VoidDeath", [7312], BozjaRuleKind.Aoe, new CircleTemplate(10)),
            new("VoidAeroII", [7308], BozjaRuleKind.AllianceWindSpread),
            new("VoidBlizzardIIIAOE", [7306], BozjaRuleKind.Aoe, new FanTemplate(60, MathF.PI / 9)),
            new("VoidAeroIVKB1", [7292], BozjaRuleKind.AllianceKnockback,
                KnockbackDistance: 37, KnockbackStopAtWall: true, KnockbackSide: -1),
            new("VoidAeroIVKB2", [7293], BozjaRuleKind.AllianceKnockback,
                KnockbackDistance: 37, KnockbackStopAtWall: true, KnockbackSide: 1),
            new("Unknown3", [7310], BozjaRuleKind.AllianceKnockback, KnockbackDistance: 20, KnockbackStopAtWall: true),
            new("VoidDeathKB2", [7314], BozjaRuleKind.AllianceKnockback,
                KnockbackDistance: 15, KnockbackPull: true, KnockbackStopAtWall: true),
            new("VoidDeathKB", [7313], BozjaRuleKind.AllianceKnockback,
                KnockbackDistance: 40, KnockbackPull: true, KnockbackStopAtWall: true),
        ], PullRadius: float.PositiveInfinity, SampleRadius: 30, Territory: 627);
}
