using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AnyderCatalog
{
    internal const uint TERRITORY = 841;
    internal static bool Owns(uint oid) => oid is 0x27AB or 0x27AC or 0x249E or 0x28DA;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x27AB, "Cladoselache and Doliodus", p => AnyderArenas.Sharks(p.Y),
        [
            new(15876, 0x27AB, "ProtolithicPuncture", null, "死刑"),
            new(15876, 0x27AC, "ProtolithicPuncture", null, "死刑"),
            new(15881, 0x27AC, "PelagicCleaver", new FanTemplate(40, MathF.PI / 3)),
            new(15883, 0x27AC, "PelagicCleaverLong", new FanTemplate(50, MathF.PI / 3)),
            new(15880, 0x27AB, "TidalGuillotine", new CircleTemplate(13)),
            new(15882, 0x27AB, "TidalGuillotineLong", new CircleTemplate(13)),
            new(15877, 0x27AB, "AquaticLance", new CircleTemplate(8), PartyAoe: "分散"),
            new(15877, 0x27AC, "AquaticLance", new CircleTemplate(8), PartyAoe: "分散"),
            new(15878, 0x27AB, "MarineMayhem", null, "全屏伤害"),
            new(15878, 0x27AC, "MarineMayhem", null, "全屏伤害"),
        ], [872, 16241, 15879], [0x27AC]),
        new(0x249E, "Morbol Marquis", p => AnyderArenas.Morbol(p.Y),
        [
            new(15893, 0x233C, "SapShower", new CircleTemplate(8), PartyAoe: "分散"),
            new(15895, 0x249E, "ArborStorm", null, "全屏伤害"),
            new(15888, 0x249E, "ExtensibleTendrils", new DungeonCrossTemplate(25, 3)),
            new(15891, 0x249E, "Blossom", null, "花色改变可站立区域"),
        ], [872, 15892, 15894, 15889, 15890]),
        new(0x28DA, "Quetzalcoatl", p => AnyderArenas.Quetzalcoatl(p.Y),
        [
            new(15907, 0x28DA, "Shockbolt", null, "死刑"),
            new(15908, 0x28DA, "Thunderbolt", null, "全屏伤害"),
            new(15900, 0x233C, "ThunderstormAOE", new CircleTemplate(5)),
            new(15899, 0x233C, "ThunderstormSpread", new CircleTemplate(5), PartyAoe: "分散"),
            new(15906, 0x233C, "ShockingPlumage", new FanTemplate(40, MathF.PI / 3)),
            new(15903, 0x28DA, "WindingCurrent", new DonutTemplate(5, 40)),
        ], [872, 15898, 15901, 17170, 15905, 15902, 15904]),
    ];
}
