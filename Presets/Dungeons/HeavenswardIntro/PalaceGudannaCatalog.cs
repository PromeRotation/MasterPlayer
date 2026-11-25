using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static partial class HeavenswardIntroCatalog
{
    private static DungeonDefinition GudannaDefinition() => new(0x1816, "Gudanna",
        p => PalaceArena(new(-300, p.Y, -220), 24.94f, 48, new(-300, -245.68f), 1.55f),
    [
        new(7096, 0, "Charybdis", new CircleTemplate(6)),
        new(7098, 0, "Trounce", new FanTemplate(51.6f, MathF.PI / 3)),
        new(7099, 0, "Ecliptic Meteor", null, "Kill him before he kills you! 80% max HP damage incoming!"),
        new(7095, 0, "Thunderbolt", new FanTemplate(16.6f, 2 * MathF.PI / 3)),
    ], []);
}
