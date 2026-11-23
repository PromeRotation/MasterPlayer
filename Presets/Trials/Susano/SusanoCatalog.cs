using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class SusanoCatalog
{
    internal const uint TERRITORY = 674;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x1AF7, "Susano", p => new CircleGeometry(new(0, p.Y, 0), 20),
        [
            new(8222, 0, "RasenKaikyo", new CircleTemplate(6)),
            new(8223, 0, "YataNoKagami", null),
            new(8230, 0, "Ukehi", null, "全屏伤害"),
            new(9631, 0, "ThePartingClouds", RayRectangleTemplate.Symmetric(10, 65)),
            new(8588, 0, "AmeNoMurakumo", RayRectangleTemplate.Symmetric(6, 65)),
            new(8259, 0, "Shock", new CircleTemplate(6)),
            new(8232, 0, "SeaSplitter", RayRectangleTemplate.Symmetric(40, 41)),
            new(8233, 0, "SeaSplitter1", RayRectangleTemplate.Symmetric(40, 27)),
            new(8234, 0, "SeaSplitter2", RayRectangleTemplate.Symmetric(40, 27)),
            new(8235, 0, "SeaSplitter3", RayRectangleTemplate.Symmetric(40, 27)),
        ], [870, 8220, 8221, 8224, 9633, 9661, 8646, 8225, 8226, 8227, 8228, 8333, 8229, 8231, 9506])
    ];
}
