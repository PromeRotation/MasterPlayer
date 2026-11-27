using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal static class OzmaCatalog
{
    internal static readonly BozjaDefinition Definition = new("A24Ozma", "奥兹玛", 0x1560, 0,
        OzmaArenas.Initial(0).Position, OzmaArenas.Initial(0),
        [
            new("ArenaChanges", [], BozjaRuleKind.OzmaArena),
            new("MeteorImpact", [6453], BozjaRuleKind.Aoe, new CircleTemplate(20)),
            new("HolyKB", [6190], BozjaRuleKind.AllianceKnockback, KnockbackDistance: 3),
            new("Holy", [6190], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("ExecrationAOE", [6185], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(10, 40.5f)),
            new("AccelerationBomb", [], BozjaRuleKind.OzmaBomb),
        ], PullRadius: float.PositiveInfinity, SampleRadius: OzmaArenas.InitialRadius, Territory: 556);
}
