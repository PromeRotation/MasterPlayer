using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal static class BozjaCll1Catalog
{
    internal static readonly Vector3 BottomCenter = new(80, 0, -179.41f);
    internal static readonly Vector3 TopCenter = new(80, 0, -222);
    internal static readonly RectangleGeometry Bottom = new(BottomCenter, 0, 59.16f, 49.18f);
    internal static readonly RectangleGeometry Top = new(TopCenter, 0, 59, 29);
    internal static readonly BozjaDefinition Definition = new("CLL1Brionac4thLegionHelldiver", "布里欧纳克",
        0x2ECC, 0, BottomCenter, Bottom,
        [
            new("BossHealths", [], BozjaRuleKind.Cll1Custom),
            new("ChainCannon", [20985, 20986], BozjaRuleKind.Cll1Custom, RayRectangleTemplate.Symmetric(5, 60)),
            new("OrbsAOE", [20944, 20949, 20953, 20943, 20942, 20947], BozjaRuleKind.Cll1Custom),
            new("Magnetism", [20952, 20950], BozjaRuleKind.Cll1Custom),
            new("ElectricAnvil", [20957], BozjaRuleKind.Tankbuster, GlobalHint: true),
            new("Voltstream", [20955], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(10, 40), MaxCasts: 3),
            new("FalseThunder", [20943, 20942], BozjaRuleKind.Aoe, new FanTemplate(47, 2 * (65 * MathF.PI / 180)), GroupedFinish: true),
            new("WildCharges", [20971, 20977], BozjaRuleKind.Cll1Custom, Safe: true),
            new("LightningShower", [21445], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("CommandSuppressiveFormation", [20982], BozjaRuleKind.Charge, RayRectangleTemplate.Symmetric(6, 0)),
            new("SurfaceMissile", [20984], BozjaRuleKind.Aoe, new CircleTemplate(6)),
            new("MRVMissile", [20989], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("MagitekMissiles", [20991], BozjaRuleKind.Tankbuster, GlobalHint: true),
            new("InfraredBlast", [20974], BozjaRuleKind.Cll1Custom),
        ], PullRadius: float.PositiveInfinity, SampleRadius: 29.58f);
}
