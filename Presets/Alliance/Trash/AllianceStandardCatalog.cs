// Generated from the explicitly reviewed seven-module manifest.
using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal static class AllianceStandardCatalog
{
    internal static readonly BozjaDefinition[] Definitions =
    [
        Create("A13Cuchulainn", "丘库雷因", 0x1413, 508,
            new CircleGeometry(new(288f, 0, 138.5f), 29.5f), 29.5f,
        [
            new("CorrosiveBile1", [5174], BozjaRuleKind.Aoe, new FanTemplate(25f, 90f * (MathF.PI / 180))),
            new("FlailingTentacles2", [5179], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(7f, 32.5f)),
            new("Beckon", [5184], BozjaRuleKind.Aoe, new FanTemplate(36.875f, 60f * (MathF.PI / 180))),
            new("BileBelow", [5181], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("Pestilence", [5188], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("BlackLung", [5182], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("GrandCorruption", [5192], BozjaRuleKind.Raidwide, GlobalHint: true),
        ]),
        Create("A22Forgall", "弗加尔", 0x1627, 556,
            AllianceStandardArenas.Forgall(0), AllianceStandardArenas.ForgallRadius,
        [
            new("BrandOfTheFallen", [6092], BozjaRuleKind.Stack, new CircleTemplate(6f), MinStackSize: 8),
            new("MegiddoFlame2", [6081], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(8f, 52.6f)),
            new("DarkEruption2", [6083], BozjaRuleKind.Aoe, new CircleTemplate(6f)),
            new("MortalRay", [6100], BozjaRuleKind.Aoe, new FanTemplate(20f, 45f * (MathF.PI / 180))),
            new("Mow", [6098], BozjaRuleKind.Aoe, new FanTemplate(13.8f, 120f * (MathF.PI / 180))),
            new("TailDrive", [6097], BozjaRuleKind.Aoe, new FanTemplate(30f, 90f * (MathF.PI / 180))),
        ]),
        Create("A23Headstone", "石面", 0x166F, 556,
            AllianceStandardArenas.Headstone(0), AllianceStandardArenas.HeadstoneRadius,
        [
            new("TremblingEpigraph", [6229], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("FlaringEpigraph", [6230], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("BigBurst", [6233], BozjaRuleKind.Raidwide, GlobalHint: true),
        ]),
        Create("A32FerdiadHollow", "虚空弗迪亚", 0x1928, 627,
            new CircleGeometry(new(-350f, 0, 225f), 30f), 30f,
        [
            new("Blackbolt", [7341], BozjaRuleKind.Stack, new CircleTemplate(6f), MinStackSize: 8),
            new("Blackfire2", [7339], BozjaRuleKind.Aoe, new CircleTemplate(7f)),
            new("JestersJig1", [7332], BozjaRuleKind.Aoe, new CircleTemplate(9f)),
            new("JestersReap", [7745], BozjaRuleKind.Aoe, new FanTemplate(13.4f, 120f * (MathF.PI / 180))),
            new("JestersReward", [7331], BozjaRuleKind.Aoe, new FanTemplate(31.4f, 180f * (MathF.PI / 180))),
            new("JongleursX", [7320], BozjaRuleKind.Tankbuster, GlobalHint: true),
            new("JugglingSphere", [7327], BozjaRuleKind.Charge, RayRectangleTemplate.Symmetric(6f, 1f)),
            new("JugglingSphere2", [7328], BozjaRuleKind.Charge, RayRectangleTemplate.Symmetric(6f, 1f)),
            new("AtmosAOE1", [7241], BozjaRuleKind.Aoe, new CircleTemplate(20f)),
            new("AtmosAOE2", [7324], BozjaRuleKind.Aoe, new CircleTemplate(20f)),
            new("AtmosDonut", [7325], BozjaRuleKind.Aoe, new DonutTemplate(6f, 20f)),
            new("PetrifyingEye", [7779], BozjaRuleKind.CastGaze),
            new("Flameflow1", [7335], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("Flameflow2", [7336], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("Flameflow3", [7337], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("Unknown4", [7326], BozjaRuleKind.Charge, RayRectangleTemplate.Symmetric(6f, 1f)),
            new("Unknown6", [7766], BozjaRuleKind.Charge, RayRectangleTemplate.Symmetric(6f, 1f)),
        ]),
        Create("A33ProtoUltima", "究极神兵原型", 0x1967, 627,
            new CircleGeometry(new(-350f, 0, -50f), 30f), 30f,
        [
            new("WreckingBall", [4557], BozjaRuleKind.Aoe, new CircleTemplate(8f)),
            new("AetherochemicalFlare", [7762], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("AetherochemicalLaser1", [7574], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(8f, 71f)),
            new("AetherochemicalLaser2", [7575], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(8f, 71f)),
            new("AetherochemicalLaser3", [7576], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(8f, 71f)),
            new("CitadelBuster2", [7595], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(10f, 65.5f)),
            new("FlareStar", [7588], BozjaRuleKind.Aoe, new CircleTemplate(31f)),
            new("Rotoswipe", [4556], BozjaRuleKind.Aoe, new FanTemplate(11f, 120f * (MathF.PI / 180))),
        ]),
        Create("A35Diabolos", "虚空迪亚波罗斯", 0x1907, 627,
            new CircleGeometry(new(-350f, 0, -445f), 35f), 35f,
        [
            new("Nightmare", [7182], BozjaRuleKind.CastGaze),
            new("NightTerror", [7187], BozjaRuleKind.Stack, new CircleTemplate(10f), MinStackSize: 8),
            new("RuinousOmen1", [7184], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("RuinousOmen2", [7185], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("UltimateTerror", [7186], BozjaRuleKind.Aoe, new DonutTemplate(5f, 19.5f)),
        ]),
        Create("A30Trash", "2P：装备飞行装置", 0x3193, 966,
            new CircleGeometry(new(755f, 0, -749.40625f), 24.5f), 24.5f,
        [
            new("LightfastBlade", [23550], BozjaRuleKind.Aoe, new FanTemplate(48f, 180f * (MathF.PI / 180)), MaxCasts: 2, MaxDangerColor: 1),
            new("ManeuverStandardLaser", [23551], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(5f, 52f)),
            new("WhirlingAssault", [23547], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(4f, 40f)),
            new("BalancedEdge", [23546], BozjaRuleKind.Aoe, new CircleTemplate(5f)),
        ]),
    ];

    private static BozjaDefinition Create(string key, string name, uint oid, uint territory,
        IGeometryShape arena, float radius, BozjaRule[] rules)
        => new(key, name, oid, 0, arena.Position, arena, rules, PullRadius: float.PositiveInfinity,
            SampleRadius: radius, Territory: territory);
}
