using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AeryCatalog
{
    internal const uint TERRITORY = 1065;

    internal static DungeonDefinition[] Create() =>
    [
        new(0xEA6, "Rangda", p => AeryArenas.Rangda(p.Y),
        [
            new(3889, 0xEA6, "ElectricCachexia", new DonutTemplate(8, 60)),
            new(3893, 0xEA6, "LightningBolt", new CircleTemplate(3)),
            new(3890, 0xEA6, "ElectrocutionVisual", null, "分散诱导电击射线"),
        ], [3887, 3888, 3892, 3891, 3894]),
        new(0x3970, "Gyascutus", p => AeryArenas.GyascutusStarting(p.Y),
        [
            new(30181, 0x3970, "InflammableFumes", null, "场地即将缩小"),
            new(30184, 0x233C, "Burst", new CircleTemplate(10)),
            new(31233, 0x3970, "DeafeningBellow", null, "全屏伤害"),
            new(30191, 0x3970, "ProximityPyre", new CircleTemplate(12)),
            new(30190, 0x3970, "AshenOuroboros", new DonutTemplate(11, 20)),
            new(31234, 0x3970, "BodySlam", null, "击退10码：靠近场心"),
            new(30193, 0x3970, "CripplingBlow", null, "死刑"),
        ], [31232, 30183]),
        new(0x39CA, "Nidhogg", p => AeryArenas.Nidhogg(p.Y),
        [
            new(30206, 0x39CA, "DeafeningBellow", null, "全屏伤害"),
            new(30197, 0x233C, "HotTail", RayRectangleTemplate.Symmetric(16, 68)),
            new(30195, 0x233C, "HotWing", RayRectangleTemplate.Symmetric(68, 30)),
            new(30202, 0x233C, "HorridRoarSpread", new CircleTemplate(6), PartyAoe: "分散"),
            new(30200, 0x233C, "HorridRoar", new CircleTemplate(6)),
            new(30198, 0x39CA, "Cauterize", RayRectangleTemplate.Symmetric(22, 80)),
            new(30205, 0x39CA, "TheScarletPrice", null, "死刑"),
            new(30207, 0x39CA, "Massacre", null, "全屏伤害"),
            new(30224, 0x39CA, "HorridBlaze", new CircleTemplate(6), PartyAoe: "分摊"),
            new(30204, 0x39CB, "SableWeave", null, "优先打破黑锁救人"),
            new(30209, 0x39CC, "Roast", RayRectangleTemplate.Symmetric(8, 30)),
        ], [30196, 30194, 30201, 30199, 30203]),
    ];
}
