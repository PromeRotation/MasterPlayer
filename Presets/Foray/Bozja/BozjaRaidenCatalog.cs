using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal static class BozjaRaidenCatalog
{
    internal static readonly BozjaDefinition Definition = new("BA2Raiden", "莱丁", 0x2605, 0,
        BaldesionArenas.RaidenStarting(0).Position, BaldesionArenas.RaidenStarting(0),
        [
            new("ArenaChange", [14463], BozjaRuleKind.RaidenArena, new DonutTemplate(30, 35)),
            new("SpiritsOfTheFallen", [14458], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("Shingan", [14459], BozjaRuleKind.Tankbuster, GlobalHint: true),
            new("AmeNoSakahoko", [14441], BozjaRuleKind.Aoe, new CircleTemplate(25)),
            new("WhirlingZantetsuken", [14442], BozjaRuleKind.Aoe, new DonutTemplate(5, 60)),
            new("Shock", [14445], BozjaRuleKind.Aoe, new CircleTemplate(8)),
            new("LateralZantetsuken", [14443, 14444], BozjaRuleKind.Aoe,
                RayRectangleTemplate.Symmetric(39, 75.4f), GroupedFinish: true),
            new("LancingBlowSpread", [], BozjaRuleKind.RaidenSpread, new CircleTemplate(10)),
            new("LancingBlowAOE", [14455], BozjaRuleKind.RaidenLancing, new CircleTemplate(10)),
            new("CloudToGround", [14449, 14450], BozjaRuleKind.Exaflare, new CircleTemplate(6)),
            new("BitterBarbs", [14452], BozjaRuleKind.RaidenChains),
            new("Levinwhorl", [14447], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("BoomingLament", [14461], BozjaRuleKind.Aoe, new CircleTemplate(10)),
            new("SilentLevin", [14451], BozjaRuleKind.Aoe, new CircleTemplate(5)),
            new("ForHonor", [14460], BozjaRuleKind.Aoe, new CircleTemplate(11.4f)),
            new("UltimateZantetsuken", [14456], BozjaRuleKind.TimedCastHint, Hint: "Enrage, kill the adds!"),
        ], PullRadius: float.PositiveInfinity, SampleRadius: BaldesionArenas.RaidenStartingRadius, Territory: 827);
}
