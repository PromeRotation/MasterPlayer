// Generated from the explicitly reviewed three-module knockback manifest.
using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal static class AllianceKnockbackCatalog
{
    internal static readonly BozjaDefinition[] Definitions =
    [
        Create("A11Cetus", "刻托斯", 0x13F7, 508,
            new CircleGeometry(new(-288f, 0, 0f), 30f), 30f,
        [
            new("ElectricSwipe", [5156], BozjaRuleKind.Aoe, new FanTemplate(25f, 60f * (MathF.PI / 180))),
            new("BodySlam", [5158], BozjaRuleKind.Aoe, new CircleTemplate(10f)),
            new("Immersion", [5159], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("ElectricWhorl", [5161], BozjaRuleKind.Aoe, new DonutTemplate(7f, 60f)),
            new("ExpulsionAOE", [5160], BozjaRuleKind.Aoe, new CircleTemplate(14f)),
            new("ExpulsionKnockback", [5160], BozjaRuleKind.AllianceKnockback, KnockbackDistance: 30f, KnockbackStopAtWall: true),
            new("BiteAndRun", [5166], BozjaRuleKind.Charge, RayRectangleTemplate.Symmetric(5f, 1f)),
        ]),
        Create("A14Echidna", "艾奇德娜", 0x13F3, 508,
            new CircleGeometry(new(288f, 0, -126f), 29.5f), 29.5f,
        [
            new("SickleStrike", [5153], BozjaRuleKind.Tankbuster, GlobalHint: true),
            new("SickleSlash1", [5143], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(60f, 18.5f)),
            new("SickleSlash2", [5341], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(60f, 18.5f)),
            new("AbyssalReaper", [5144], BozjaRuleKind.Aoe, new CircleTemplate(14f)),
            new("AbyssalReaperKnockback", [5144], BozjaRuleKind.AllianceKnockback, KnockbackDistance: 5f, KnockbackStopAtWall: true),
            new("Petrifaction1", [5154], BozjaRuleKind.CastGaze),
            new("Petrifaction2", [5374], BozjaRuleKind.CastGaze),
            new("Gehenna", [5152], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("BloodyHarvest", [5147], BozjaRuleKind.Aoe, new CircleTemplate(12f)),
            new("Deathstrike", [5195], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(6f, 62f)),
            new("FlameWreath", [5149], BozjaRuleKind.Aoe, new CircleTemplate(18f)),
            new("SerpentineStrike", [5150], BozjaRuleKind.Aoe, new CircleTemplate(20f)),
        ]),
        Create("A25Calofisteri", "卡洛菲斯提莉", 0x1661, 556,
            AllianceStandardArenas.Calofisteri(0), AllianceStandardArenas.CalofisteriRadius,
        [
            new("AuraBurst", [6177], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("DepthCharge", [6176], BozjaRuleKind.Charge, RayRectangleTemplate.Symmetric(10f, 1f)),
            new("Extension2", [6163], BozjaRuleKind.Aoe, new CircleTemplate(6f)),
            new("FeintParticleBeam1", [6439], BozjaRuleKind.Aoe, new CircleTemplate(3f)),
            new("Penetration", [6178], BozjaRuleKind.AllianceKnockback, KnockbackDistance: 50f, KnockbackPull: true),
            new("Graft", [6172], BozjaRuleKind.Aoe, new CircleTemplate(5f)),
            new("Haircut1", [6155], BozjaRuleKind.Aoe, new FanTemplate(65.5f, 180f * (MathF.PI / 180))),
            new("Haircut2", [6159], BozjaRuleKind.Aoe, new FanTemplate(65.5f, 180f * (MathF.PI / 180))),
            new("SplitEnd1", [6156], BozjaRuleKind.Aoe, new FanTemplate(25.5f, 45f * (MathF.PI / 180))),
            new("SplitEnd2", [6160], BozjaRuleKind.Aoe, new FanTemplate(25.5f, 45f * (MathF.PI / 180))),
        ]),
    ];

    private static BozjaDefinition Create(string key, string name, uint oid, uint territory,
        IGeometryShape arena, float radius, BozjaRule[] rules)
        => new(key, name, oid, 0, arena.Position, arena, rules, PullRadius: float.PositiveInfinity,
            SampleRadius: radius, Territory: territory);
}
