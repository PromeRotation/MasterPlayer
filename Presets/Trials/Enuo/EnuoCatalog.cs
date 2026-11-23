using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class EnuoCatalog
{
    internal const uint TERRITORY = 1361;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x4DB9, "Enuo", p => new CircleGeometry(new(100, p.Y, 100), 20),
        [
            new(49971, 0, "Meteorain", null, "全屏伤害"),
            new(49928, 0, "Almagest", null, "全屏伤害"),
            new(49933, 0, "NaughtGrowsAOE", new CircleTemplate(40)),
            new(49953, 0, "GazeOfTheVoidCones", new FanTemplate(20, MathF.PI / 4)),
            new(49952, 0, "GazeOfTheVoidCones", new FanTemplate(20, MathF.PI / 4)),
            new(49966, 0, "DeepFreeze", null),
            new(49968, 0, "ShroudedHoly", null),
            new(49964, 0, "MeltdownSpread", null),
            new(49963, 0, "MeltdownAOE", new CircleTemplate(5)),
            new(49946, 0, "SilentTorrentSmall", new PtDonutSectorTemplate(17, 19, 20 * MathF.PI / 180)),
            new(49947, 0, "SilentTorrentMedium", new PtDonutSectorTemplate(17, 19, 40 * MathF.PI / 180)),
            new(49948, 0, "SilentTorrentLarge", new PtDonutSectorTemplate(17, 19, 60 * MathF.PI / 180)),
            new(49949, 0, "VacuumCircle", new CircleTemplate(7)),
            new(50667, 0, "EmptyShadow", new CircleTemplate(10)),
            new(49981, 0, "LoomingEmptiness", new CircleTemplate(42)),
            new(49958, 0, "Nothingness", RayRectangleTemplate.Symmetric(4, 100)),
            new(49959, 0, "LightlessWorld", null),
            new(49934, 0, "NaughtGrowsSmall", new CircleTemplate(12)),
        ],
        [49936, 49927, 49931, 49929, 49930, 49950, 49965, 49939, 48474, 49940, 49941,
            49967, 49962, 49942, 49944, 49943, 49945, 49954, 49955, 49956, 49957, 49938,
            49960, 49961, 50775, 870, 50796, 50505, 50776, 50777, 50798, 49932, 50797,
            50778, 50779, 49969, 49970, 49951]),
    ];
}
