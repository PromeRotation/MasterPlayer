using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class IfritExtremeCatalog
{
    internal const uint TERRITORY = 295;

    internal static DungeonDefinition[] Create() =>
    [
        new(0xD3, "Ifrit Extreme", p => ArrTrialCatalog.IfritArena(p.Y),
        [
            new(1529, 0, "InfernoHowl", null),
            new(1676, 0, "Eruption", null),
            new(1677, 0, "EruptionAOE", new CircleTemplate(8)),
            new(1536, 0, "Hellfire", null),
            new(1356, 0, "RadiantPlume", null),
            new(1359, 0, "RadiantPlumeAOE", new CircleTemplate(8)),
            new(1532, 0, "CrimsonCyclone", RayRectangleTemplate.Symmetric(18, 49)),
        ], []),
    ];
}
