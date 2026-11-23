using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AetherfontCatalog
{
    internal const uint TERRITORY = 1126;
    internal static bool IsTrash(uint oid) => oid is 0x3EC7 or 0x3EC9 or 0x3ECB or 0x3ECD or 0x3ED1;
    internal static bool Owns(uint oid) => IsTrash(oid) || oid is 0x3EEB or 0x3EEA or 0x3EAA;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x3EC7, "HaamOtodus", p => AetherfontArenas.Otodus(p.Y),
        [
            new(33992, 0, "AquaticLance", new CircleTemplate(8)),
            new(33991, 0, "Icestorm", new FanTemplate(16, MathF.PI / 2)),
        ], [872, 871, 33983], [0x3EC6, 0x3F5E, 0x3F5F, 0x3EC8]),
        new(0x3EC9, "HaamHalonid", p => AetherfontArenas.Halonid(p.Y),
        [
            new(33991, 0, "Icestorm", new FanTemplate(16, MathF.PI / 2)),
        ], [872, 871], [0x3EC8, 0x3EC6]),
        new(0x3EEB, "Lyngbakr", p => AetherfontArenas.Lyngbakr(p.Y),
        [
            new(33335, 0, "BodySlam", null, "全屏伤害"),
            new(33338, 0, "Upsweep", null, "全屏伤害"),
            new(33345, 0, "SonicBloop", null, "死刑"),
            new(33340, 0, "ExplosiveFrequency", new CircleTemplate(15)),
            new(33339, 0, "ResonantFrequency", new CircleTemplate(8)),
            new(33344, 0, "TidalBreath", new FanTemplate(40, MathF.PI)),
            new(33343, 0, "Tidalspout", new CircleTemplate(6), PartyAoe: "分摊"),
            new(33342, 0, "Waterspout", new CircleTemplate(5), PartyAoe: "分散"),
        ], [34517, 33341]),
        new(0x3ECB, "HaamTroll", p => AetherfontArenas.Troll(p.Y),
        [
            new(33994, 0, "Uppercut", new FanTemplate(11, MathF.PI * 2 / 3)),
        ], [872, 870, 871], [0x3ECC, 0x3ECA]),
        new(0x3ECD, "HaamCrystal", p => AetherfontArenas.Crystal(p.Y),
        [
            new(33995, 0, "HardHead", new FanTemplate(12, MathF.PI * 2 / 3)),
            new(33993, 0, "EarthenHeart", new CircleTemplate(6)),
        ], [872, 870], [0x3ECA, 0x3ECE]),
        new(0x3EEA, "Arkas", p => AetherfontArenas.Arkas(p.Y),
        [
            new(34605, 0, "BattleCry1", null, "全屏伤害"),
            new(33364, 0, "BattleCry2", null, "全屏伤害"),
            new(33615, 0, "ElectricEruption", null, "全屏伤害"),
            new(33367, 0, "Electrify", new CircleTemplate(10)),
            new(33360, 0, "LightningLeap1", new CircleTemplate(10)),
            new(34713, 0, "LightningLeap2", new CircleTemplate(10)),
            new(34321, 0, "LightningRampage1", new CircleTemplate(10)),
            new(34714, 0, "LightningRampage2", new CircleTemplate(10)),
            new(33362, 0, "SpinningClaw", new CircleTemplate(10)),
            new(33365, 0, "Shock", new CircleTemplate(6)),
            new(33363, 0, "SpunLightning", RayRectangleTemplate.Symmetric(8, 30)),
            new(33368, 0, "RipperClaw", null, "死刑"),
            new(33361, 0, "ForkedFissures", null),
        ], [870, 33366, 34712, 33358, 33359, 34318, 34319, 34320]),
        new(0x3ED1, "HaamForlorn", p => AetherfontArenas.Forlorn(p.Y),
        [
            new(33996, 0, "AetherialSpark", RayRectangleTemplate.Symmetric(4, 12)),
        ], [872, 972], [0x3ED0, 0x3ECF, 0x3ED2]),
        new(0x3EAA, "Octomammoth", p => AetherfontArenas.Octomammoth(p.Y),
        [
            new(33346, 0, "Wallop", RayRectangleTemplate.Symmetric(8, 22)),
            new(33348, 0, "Clearout", new FanTemplate(16, MathF.PI * 2 / 3)),
            new(34551, 0, "Breathstroke", new FanTemplate(35, MathF.PI)),
            new(33354, 0, "TidalBreath", new FanTemplate(35, MathF.PI)),
            new(33355, 0, "VividEyes", new DonutTemplate(20, 26)),
            new(34436, 0, "WaterDrop", new CircleTemplate(6), PartyAoe: "分散"),
            new(33356, 0, "TidalRoar", null, "全屏伤害"),
            new(33353, 0, "SalineSpit", new CircleTemplate(8)),
            new(33351, 0, "Telekinesis", new CircleTemplate(12)),
        ], [33357, 33347, 33352, 33349, 33350]),
    ];
}
