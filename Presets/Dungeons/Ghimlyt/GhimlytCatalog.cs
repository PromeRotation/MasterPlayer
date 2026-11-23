using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class GhimlytCatalog
{
    internal const uint TERRITORY = 1174;
    internal static bool Owns(uint oid) => oid is 0x2526 or 0x2528 or 0x2527 or 0x25DA or 0x2515 or 0x25B0 or 0x25AF;
    internal static bool IsCrosshatch(uint action) => action is 14411 or 14412 or 14413 or 14115 or 14116 or 14697 or 14698 or 14699;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x2526, "Schola Colossus", p => GhimlytArenas.Schola(p.Y),
        [
            new(14468, 0x233C, "HomingLaser", new CircleTemplate(6), PartyAoe: "分散"),
            new(14965, 0x2526, "GrandStrike", RayRectangleTemplate.Symmetric(4, 48.2f)),
        ], [870, 14467]),
        new(0x2528, "Schola Mark II Colossus", p => GhimlytArenas.ScholaII(p.Y),
        [
            new(15042, 0x233C, "MagitekMissile", new CircleTemplate(15)),
            new(14966, 0x2528, "Exhaust", RayRectangleTemplate.Symmetric(10, 43.2f)),
            new(14967, 0x2527, "GrandSword", new FanTemplate(18.4f, MathF.PI * 2 / 3)),
            new(14574, 0x2527, "SelfDetonate", null, "狂暴：尽快击破"),
            new(14470, 0x2527, "UnbreakableCermetBlade", null, "进入护盾"),
        ], [870, 15041, 14472], [0x2527]),
        new(0x25DA, "Mark III-B Magitek Colossus", p => GhimlytArenas.Colossus(p.Y),
        [
            new(14190, 0x25DA, "JarringBlow", null, "死刑"),
            new(14194, 0x233C, "WildFireBeam", new CircleTemplate(6), PartyAoe: "分散"),
            new(14191, 0x25DA, "MagitekRay", new CircleTemplate(6), PartyAoe: "分摊"),
            new(14196, 0x25DA, "MagitekSlashFirst", null),
            new(14192, 0x25DA, "Exhaust", RayRectangleTemplate.Symmetric(10, 43.5f)),
            new(14195, 0x25DA, "CeruleumVent", null, "全屏伤害"),
        ], [870, 14193, 14197, 14670, 14671]),
        new(0x2515, "Prometheus", p => GhimlytArenas.PrometheusStarting(p.Y),
        [
            new(13397, 0x2515, "Nitrospin", null, "全屏伤害"),
            new(13402, 0x2515, "NeedleGun", new FanTemplate(47.8f, MathF.PI / 2)),
            new(13398, 0x2515, "OilShower", new FanTemplate(47.8f, MathF.PI * 1.5f)),
            new(13401, 0x2515, "UnbreakableCermetDrill", null, "死刑"),
            new(13404, 0x233C, "FreezingMissile", new CircleTemplate(8)),
        ], [15046, 13399, 13400, 13403]),
        Duo(),
    ];

    private static DungeonDefinition Duo()
    {
        List<DungeonCastRule> casts =
        [
            new(14123, 0x25AF, "Heirsbane", null, "单体伤害"),
            new(14122, 0x25B0, "DeltaTrance", null, "死刑"),
            new(14121, 0x25AF, "Innocence", null, "死刑"),
            new(14120, 0x25B0, "ArtificialPlasmaAnnia", null, "全屏伤害"),
            new(14119, 0x25AF, "ArtificialPlasmaJulia", null, "全屏伤害"),
            new(15488, 0x25B5, "Crossbones", null, "冲锋击退：避开轰炸落点"),
            new(14124, 0x25B0, "AngrySalamander", RayRectangleTemplate.Symmetric(6, 45.6f)),
            new(14097, 0x233C, "Bombardment", new CircleTemplate(10)),
            new(14131, 0x25B5, "Quaternity", RayRectangleTemplate.Symmetric(4, 41)),
            new(14729, 0x25B5, "Quaternity", RayRectangleTemplate.Symmetric(4, 26)),
            new(14098, 0x25B0, "StunningSweep", new CircleTemplate(6.6f)),
            new(14126, 0x233C, "MissileImpact", new CircleTemplate(6)),
            new(14108, 0x233C, "CoveringFire", new CircleTemplate(8), PartyAoe: "分散"),
            new(14129, 0x25AF, "ImperialAuthorityJulia", null, "狂暴：同时击破两名首领"),
            new(14130, 0x25B0, "ImperialAuthorityAnnia", null, "狂暴：同时击破两名首领"),
        ];
        foreach (var action in new uint[] { 14411, 14412, 14413, 14115, 14116, 14697, 14698, 14699 })
            casts.Add(new(action, 0x233C, "CrosshatchTelegraph", null));
        return new(0x25B0, "Soranus Duo", p => GhimlytArenas.Duo(p.Y), casts.ToArray(),
            [872, 870, 14095, 14094, 14099, 14100, 14778, 14096, 14113, 14114, 14118, 14117, 14102,
                14103, 14104, 14105, 14106, 14125, 14107, 14127, 14128], [0x25AF]);
    }
}
