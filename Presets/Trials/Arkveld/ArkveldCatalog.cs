using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ArkveldCatalog
{
    internal const uint TERRITORY = 1300;
    internal static DungeonDefinition[] Create()
    {
        var rules = new List<DungeonCastRule>();
        foreach (var action in new[] { 43886u, 45201u, 43870u, 44790u, 44791u })
            rules.Add(new(action, 0, "RoarForgedFury", null, "全屏伤害"));
        foreach (var action in new[] { 43830u, 43831u, 45050u, 45051u, 45054u, 45055u, 45057u, 45058u })
            rules.Add(new(action, 0, "ChainbladeBlow", RayRectangleTemplate.Symmetric(4, 40)));
        foreach (var action in new[] { 43832u, 43833u, 45056u, 45059u })
            rules.Add(new(action, 0, "WyvernsRadianceCleave", RayRectangleTemplate.Symmetric(28, 80)));
        rules.Add(new(43836, 0, "GuardianResonance", RayRectangleTemplate.Symmetric(16, 40)));
        foreach (var action in new[] { 43849u, 45061u })
            rules.Add(new(action, 0, "WyvernsOuroblade", new FanTemplate(40, MathF.PI)));
        foreach (var action in new[] { 45064u, 44804u, 45049u, 45048u })
            rules.Add(new(action, 0, "SteeltailWyvernsWeal", RayRectangleTemplate.Symmetric(6, 60)));
        rules.AddRange([
            new(43842, 0, "Rush", null),
            new(43844, 0, "Concentric1", new CircleTemplate(8)),
            new(43845, 0, "Concentric2", new DonutTemplate(8, 14)),
            new(43846, 0, "Concentric3", new DonutTemplate(14, 20)),
            new(43847, 0, "Concentric4", new DonutTemplate(20, 26)),
            new(43857, 0, "CrackedCrystalSmall", new CircleTemplate(6)),
            new(43858, 0, "CrackedCrystalLarge", new CircleTemplate(12)),
            new(43853, 0, "ResonanceTowerSmall", null),
            new(43854, 0, "ResonanceTowerLarge", null),
            new(43866, 0, "WildEnergy", null),
            new(43860, 0, "WyvernsVengeance", null),
            new(45046, 0, "WyvernsWealLeft", null),
            new(45047, 0, "WyvernsWealRight", null),
        ]);
        return [new(0x48E2, "Arkveld", p => new CircleGeometry(new Vector3(100, p.Y, 100), 20), rules.ToArray(),
            [43828, 43829, 45175, 43834, 43835, 45123, 43876, 43877, 43878, 43837, 43838, 45076,
                43839, 43840, 43841, 45075, 43843, 43883, 43884, 44811, 43848, 43827, 43856, 43852,
                43859, 43861, 44807, 44808, 43869, 43875, 43873, 45068, 45071, 45074, 45073,
                45072, 45052, 45067, 45069, 45124, 45070, 45053, 43879, 43880, 43881, 43882,
                45060, 43855])];
    }
}
