using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static partial class HeavenswardIntroCatalog
{
    private static DungeonDefinition PalacePlantDefinition(uint territory)
    {
        var later = territory == KIRTIMUKHA;
        List<DungeonCastRule> rules =
        [
            new(later ? 7134u : 6422u, 0, "Acid Mist", new CircleTemplate(9.6f)),
            new(later ? 7135u : 6423u, 0, "Gold Dust", new CircleTemplate(8)),
            new(later ? 7136u : 6424u, 0, "Leafstorm", null, "Raidwide"),
            new(later ? 7137u : 6425u, 0, "Rotten Stench", RayRectangleTemplate.Symmetric(12, 48.6f)),
        ];
        if (later) rules.Add(new(919, 0, "Final Sting", null, "Final sting is being cast! \nKill the add or take 98% of your hp!"));
        return new(later ? 0x1819u : 0x169Fu, later ? "Kirtimukha" : "Spurge",
            p => later ? AlfardArena(p.Y) : PalaceArena(new(-300, p.Y, -236), 24.5f, 24, new(-298.763f, -261.119f), 1.55f),
            [.. rules], []);
    }
}
