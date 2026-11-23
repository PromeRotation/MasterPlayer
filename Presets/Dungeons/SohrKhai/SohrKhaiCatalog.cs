using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class SohrKhaiCatalog
{
    internal const uint TERRITORY = 1112;
    internal static bool IsTrash(uint oid) => oid is 0x1604 or 0x160E or 0x1612;
    internal static bool Owns(uint oid) => IsTrash(oid) || oid is 0x15FB or 0x155C or 0x3D17;
    internal static bool IsMoogle(uint oid) => oid is 0x15FC or 0x15FD or 0x15FE or 0x15FF or 0x1600 or 0x1601 or 0x1602;
    internal static bool InGroup(uint primary, uint oid) => primary switch
    {
        0x1604 => oid is 0x1604 or 0x1605 or 0x1607 or 0x1609 or 0x1608 or 0x1606,
        0x160E => oid is 0x160E or 0x160C or 0x160A or 0x160F,
        0x1612 => oid is 0x1612 or 0x1611 or 0x1613,
        _ => false,
    };

    internal static DungeonDefinition[] Create() =>
    [
        new(0x1604, "Cloud Gardener", p => p.Z > 80 ? SohrKhaiArenas.GardenFirst(p.Y) : SohrKhaiArenas.GardenSecond(p.Y),
        [
            new(4700, 0x1606, "RiseAndFall", new FanTemplate(9, MathF.PI * 1.5f)),
            new(6237, 0x1605, "TightTornado", RayRectangleTemplate.Symmetric(4, 18)),
            new(6236, 0x1604, "DarkBlizzardIII", new CircleTemplate(5)),
        ], [872, 344, 4502, 1911, 1912]),
        new(0x15FB, "Chieftain Moglin", p => SohrKhaiArenas.Moglin(p.Y),
        [
            new(4429, 0x15FD, "HundredKuponzeSwipe", new FanTemplate(20.9f, MathF.PI / 2)),
            new(4428, 0x1600, "PomFlare", new CircleTemplate(20.9f)),
            new(4425, 0x15FC, "SpinningMogshield", new CircleTemplate(6.9f)),
            new(6020, 0x15FB, "PomHoly", null, "全屏伤害"),
            new(6017, 0x15FB, "DemoniacalMogcane", null),
            new(6016, 0x1B2, "PomPraise", null),
            new(6015, 0x15FB, "PomPraiseVisual", null, "将倒地莫古力推出复活圈"),
        ], [6019, 6018, 6212]),
        new(0x160E, "Kargas", p => SohrKhaiArenas.Kargas(p.Y),
        [
            new(6107, 0x160C, "BreathWing", null, "全屏伤害"),
            new(2478, 0x160F, "WingsOfWoe", new CircleTemplate(6)),
        ], [870, 871, 4690]),
        new(0x155C, "Poqhiraj", p => SohrKhaiPoqhirajArena.Create(0, p.Y),
        [
            new(5778, 0x1B2, "GallopAOE", RayRectangleTemplate.Symmetric(2, 40.5f)),
            new(5823, 0x1B2, "GallopKB", null, "向前击退30码，利用完整墙段"),
            new(6240, 0x1B2, "Touchdown", new CircleTemplate(25)),
            new(6011, 0x155C, "BurningBright", null, "直线诱导避开队友及墙"),
            new(6010, 0x155D, "LightningBolt", null),
        ], [872, 6013, 6012, 5777, 6009]),
        new(0x1612, "Blizzard Dragon", p => SohrKhaiArenas.BlizzardFirst(p.Y),
        [
            new(6241, 0x1636, "Cauterize", RayRectangleTemplate.Symmetric(20, 53)),
            new(4724, 0x1613, "Fireball", new CircleTemplate(4)),
            new(4794, 0x1611, "SheetOfIce", new CircleTemplate(5)),
        ], [872, 870, 564]),
        new(0x3D17, "Hraesvelgr", p => SohrKhaiArenas.Hraesvelgr(p.Y),
        [
            new(32136, 0x3D17, "HallowedWingsLeft", RayRectangleTemplate.Symmetric(22, 50)),
            new(32137, 0x3D17, "HallowedWingsRight", RayRectangleTemplate.Symmetric(22, 50)),
            new(32131, 0x3D17, "HallowedDive", RayRectangleTemplate.Symmetric(20, 40)),
            new(32134, 0x3D17, "FrigidDive", RayRectangleTemplate.Symmetric(20, 40)),
            new(32135, 0x233C, "FrostedOrb", new CircleTemplate(6)),
            new(32141, 0x3D17, "Wyrmclaw", null, "死刑"),
            new(32127, 0x3D17, "HolyStorm", null, "全屏伤害"),
            new(32128, 0x3D17, "DiamondStorm", null, "全屏伤害"),
            new(32132, 0x3D17, "AkhMorn", null),
            new(32129, 0x233C, "HolyOrb", null),
        ], [32142, 32130, 32133, 32138, 32139]),
    ];
}
