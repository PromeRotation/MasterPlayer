using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal static class BozjaPyromancerCatalog
{
    internal static readonly BozjaRule ThermalShock = new("ThermalShock", [21022], BozjaRuleKind.Aoe, new CircleTemplate(20));

    internal static readonly BozjaDefinition Definition = new("PyromancerSupreme", "最强的火焰法师",
        0x2EF7, 0, Vector3.Zero, new CircleGeometry(Vector3.Zero, 30),
        [
            new("PyreticEruptionPyroscatter", [20929, 20930], BozjaRuleKind.Aoe, new CircleTemplate(8), GroupedFinish: true),
            new("TridirectionalFlame", [20927], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(8, 60)),
            new("Pyroburst", [20931], BozjaRuleKind.Aoe, new CircleTemplate(10)),
            new("GrandCrossflame", [20928], BozjaRuleKind.Aoe, new DungeonCrossTemplate(40, 9)),
            new("Firestarter", [20926], BozjaRuleKind.Aoe, new DonutTemplate(6, 40)),
            new("GarleanFireSnowsOfBozja", [21246, 21274], BozjaRuleKind.Aoe, new CircleTemplate(5), GroupedFinish: true),
        ], FateId: 1616);
}
