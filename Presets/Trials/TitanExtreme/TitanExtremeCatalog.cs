using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class TitanExtremeCatalog
{
    internal const uint TERRITORY = 296;

    internal static DungeonDefinition[] Create() =>
    [
        new(0xF8, "Titan Extreme", p => new CircleGeometry(new(0, p.Y, 0), 25),
        [
            new(1466, 0, "Upheaval", null),
            new(1467, 0, "LandslideBoss", null),
            new(1468, 0, "LandslideHelper", null),
            new(1475, 0, "LandslideGaoler", null),
            new(1469, 0, "WeightOfTheLand", null),
            new(1470, 0, "WeightOfTheLandAOE", new CircleTemplate(6)),
            new(1471, 0, "Burst", null),
            new(1472, 0, "Geocrush", null),
            new(1474, 0, "Enrage", null),
            new(1477, 0, "GraniteSepulchre", null),
        ], []),
    ];
}
