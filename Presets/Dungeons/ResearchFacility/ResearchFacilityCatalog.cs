using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ResearchFacilityCatalog
{
    internal const uint TERRITORY = 1110;

    internal static DungeonDefinition[] Create() =>
    [
        new(0xE97, "Regula van Hydrus", p => ResearchFacilityArenas.Regula(p.Y),
        [
            new(4322, 0xE99, "AetherochemicalGrenado", new CircleTemplate(8)),
            new(4321, 0xE98, "AetherochemicalLaser", RayRectangleTemplate.Symmetric(4, 50)),
            new(4318, 0x1B2, "Judgment", new CircleTemplate(8)),
            new(4323, 0xE98, "SelfDetonate", null, "全屏伤害"),
            new(4323, 0xE99, "SelfDetonate", null, "全屏伤害"),
            new(4315, 0xE97, "MagitekSlug", RayRectangleTemplate.Symmetric(4, 61.65f)),
            new(4316, 0xE97, "MagitekSpread", new FanTemplate(31.65f, MathF.PI * 4 / 3)),
        ], [870, 4314, 4317, 4320, 4319]),
        new(0xE9A, "Harmachis", p => ResearchFacilityArenas.Harmachis(p.Y),
        [
            new(4325, 0x1B2, "Paradox", new CircleTemplate(5)),
            new(4331, 0xE9A, "Petrifaction", null, "背对视线"),
            new(4326, 0xE9A, "Ka", new FanTemplate(45, MathF.PI / 3)),
        ], [870, 4334, 4335, 4771, 4327, 4332, 4336, 4329, 4333, 4324, 4330, 3790, 3792, 4328, 5007]),
        new(0x3DA4, "Lahabrea and Igeyorhm", p => ResearchFacilityArenas.Lahabrea(p.Y),
        [
            new(31885, 0x3DA4, "ShadowFlare", null, "全屏伤害"),
            new(31885, 0x3DA3, "ShadowFlare", null, "全屏伤害"),
            new(32790, 0x3DA4, "GripOfNight", new FanTemplate(40, MathF.PI * 5 / 6)),
            new(32688, 0x233C, "DarkFireII", new CircleTemplate(6), PartyAoe: "分散"),
        ], [32818, 32791, 32686, 32685, 31878, 31879, 31881, 31882, 33019, 32687, 31892, 31891,
            33029, 31880, 31888, 31886, 31887, 31889, 31890, 33020]),
        new(0x3DA7, "Ascian Prime", p => ResearchFacilityArenas.AscianPrime(p.Y),
        [
            new(31909, 0x233C, "AncientEruption", new CircleTemplate(5)),
            new(33024, 0x233C, "Annihilation", null, "全屏伤害"),
            new(31923, 0x233C, "ChillingCross", new DungeonCrossTemplate(40, 2.5f)),
            new(31924, 0x233C, "ChillingCross", new DungeonCrossTemplate(40, 2.5f)),
            new(31915, 0x233C, "DarkBlizzardIII", new FanTemplate(41, MathF.PI / 9)),
            new(31916, 0x233C, "DarkBlizzardIII", new FanTemplate(41, MathF.PI / 9)),
            new(31917, 0x233C, "DarkBlizzardIII", new FanTemplate(41, MathF.PI / 9)),
            new(31918, 0x233C, "DarkBlizzardIII", new FanTemplate(41, MathF.PI / 9)),
            new(31919, 0x233C, "DarkBlizzardIII", new FanTemplate(41, MathF.PI / 9)),
            new(31921, 0x233C, "DarkFireII", new CircleTemplate(6), PartyAoe: "分散"),
            new(31911, 0x3DA7, "HeightOfChaos", new CircleTemplate(5), "死刑", "分散"),
            new(31910, 0x3DA7, "ShadowFlare", null, "全屏伤害"),
            new(31925, 0x3DAA, "ShadowFlareLBPhase", null, "两次全屏伤害"),
            new(31900, 0x3DA7, "UniversalManipulation", null, "全屏伤害及连续减益"),
            // These casts consume the earlier star forecasts rather than creating duplicate AOEs.
            new(33021, 0x233C, "CircleOfIcePrime", null),
            new(33022, 0x233C, "FireSpherePrime", null),
        ], [871, 31901, 31903, 31908, 31904, 31927, 31928, 33025, 31912, 31913, 31905, 31922,
            31898, 31899, 31896, 31897, 31914, 31920, 31894, 31907, 32126, 31906, 32555, 31895, 31926, 31419, 33044]),
        .. ResearchTrashCatalog.Create(),
    ];
}
