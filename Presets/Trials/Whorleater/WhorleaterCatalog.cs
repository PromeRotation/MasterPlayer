using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class WhorleaterCatalog
{
    internal const uint TERRITORY = 281;
    internal static DungeonDefinition[] Create() =>
    [
        new(0xA67, "Whorleater Hard", p => new RectangleGeometry(new(0, p.Y, 0), 0, 29, 39),
        [
            new(1873, 0, "GrandFall", new CircleTemplate(8)),
            new(1864, 0, "Hydroshot", null),
            new(1865, 0, "Dreadstorm", null),
        ], [1853, 870, 1860, 1938, 1937, 2165, 2164, 1857, 1870, 1862, 1877, 1876, 1855, 1856,
            1859, 1858, 1868, 1867, 1869, 1861, 1871, 1872, 2214, 1863, 1875])
    ];
}
