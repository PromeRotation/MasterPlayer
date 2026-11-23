using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class YojimboCatalog
{
    internal const uint TERRITORY = 806;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x25FD, "Yojimbo", p => new RectangleGeometry(new(100, p.Y, 100), 0, 40, 40),
        [
            new(14376, 0, "MettaGiri", null, "全屏伤害"),
            new(14386, 0, "Yukikaze2", RayRectangleTemplate.Symmetric(4, 44.5f)),
            new(14378, 0, "Fragility", null),
            new(14388, 0, "Kasha2", null),
            new(14397, 0, "AmeNoMurakumo", null, "全屏伤害"),
            new(14400, 0, "ElectrogeneticForce2", null),
            new(14403, 0, "Masamune", null),
            new(14404, 0, "ZanmaZanmai", null, "全屏伤害"),
            new(14405, 0, "EpicStormsplitter", null),
            new(14409, 0, "Seasplitter1", null),
        ], [870, 12766, 14382, 14394, 14392, 14390, 14399, 14401, 14384, 14387, 14396, 14398,
            14402, 14377, 14385, 14389, 14410, 14395, 14379, 14380, 14381, 14375, 14383, 14406, 14407, 14408, 14393])
    ];
}
