using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class GarudaExtremeCatalog
{
    internal const uint TERRITORY = 297;

    internal static DungeonDefinition[] Create() =>
    [
        new(0xF5, "Garuda Extreme", p => new CircleGeometry(new(0, p.Y, 0), 22),
        [
            new(1382, 0, "Slipstream", new FanTemplate(11.7f, MathF.PI / 2)),
            new(1549, 0, "FrictionAdds", new CircleTemplate(5)),
            new(1550, 0, "FeatherRain", new CircleTemplate(3)),
            new(1554, 0, "AerialBlast", null, "全屏伤害"),
            new(1384, 0, "MistralShriek", null, "全屏伤害"),
            new(1555, 0, "Gigastorm", new CircleTemplate(6.5f)),
            new(1777, 0, "GreatWhirlwind", new CircleTemplate(8)),
            new(1671, 0, "EyeOfTheStorm", null),
            new(1761, 0, "MistralSong", null),
        ], []),
    ];
}
