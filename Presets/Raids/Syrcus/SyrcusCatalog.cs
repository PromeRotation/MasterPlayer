using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class SyrcusCatalog
{
    internal const uint TERRITORY = 372;
    internal static DungeonDefinition[] Create() =>
    [
        new(0xBC1, "Scylla", p => new CircleGeometry(new Vector3(0, p.Y, -192), 35),
        [
            new(2321, 0, "Topple", new CircleTemplate(6.75f)),
            new(2330, 0, "SearingChain", RayRectangleTemplate.Symmetric(4, 61)),
            new(2331, 0, "InfiniteAnguish", new DonutTemplate(6, 12)),
            new(2317, 0, "AncientFlare", null, "全屏伤害"),
        ], [872, 2319, 2329, 2324, 2323, 2322]),
        new(0xBD7, "Glasya Labolas", p => new CircleGeometry(new Vector3(0, p.Y, -200), 35),
        [
            new(2348, 0, "Aura", new CircleTemplate(8)),
            new(2353, 0, "VileUtterance", new FanTemplate(30, MathF.PI / 4)),
        ], [872, 2346, 2349]),
        new(0xBE0, "Amon", p => new CircleGeometry(new Vector3(0, p.Y, -200), 30),
        [
            new(2382, 0, "BlizzagaForte", new CircleTemplate(10)),
            new(1875, 0, "Darkness", new FanTemplate(6, MathF.PI / 4)),
            new(2441, 0, "CurtainCall", null),
            new(2384, 0, "ThundagaForte1", new CircleTemplate(6)),
            new(2383, 0, "ThundagaForte2", new CircleTemplate(6)),
        ], [872, 2379, 2380, 2371, 2386, 2374, 2385, 870, 2372, 2377, 2378]),
        new(0xBDB, "Xande", p => new CircleGeometry(new Vector3(-400, p.Y, -200), 35),
        [
            new(2355, 0, "KnucklePress", new CircleTemplate(10)),
            new(2357, 0, "BurningRave1", new CircleTemplate(8)),
            new(2356, 0, "BurningRave2", new CircleTemplate(8)),
            new(2359, 0, "AncientQuake", null, "全屏伤害"),
            new(2361, 0, "AncientQuaga", null, "全屏伤害"),
            new(2358, 0, "AuraCannon", RayRectangleTemplate.Symmetric(10, 60)),
        ], [872, 2354, 2366, 2365, 2367]),
    ];
}
