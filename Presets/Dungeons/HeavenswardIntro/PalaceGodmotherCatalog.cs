using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static partial class HeavenswardIntroCatalog
{
    private static DungeonDefinition GodmotherDefinition() => new(0x1817, "The Godmother",
        p => PalaceArena(new(-300, p.Y, -236), 24.5f, 24, new(-298.763f, -261.119f), 1.55f),
    [
        new(7105, 0, "Burst", null, "Kill the Grey Bomb! or take 80% of your Max HP"),
        new(7104, 0, "Hypothermal Combustion", new CircleTemplate(7.2f)),
        new(7102, 0, "Massive Burst", null, "Knock the Giddy bomb into the boss and let it explode on the boss. \n or else take 99% damage!"),
        new(7101, 0, "Sap", new CircleTemplate(8)),
        new(7106, 0, "Self Destruct", null),
    ], []);
}
