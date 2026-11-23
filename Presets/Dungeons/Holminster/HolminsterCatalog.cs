using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class HolminsterCatalog
{
    internal const uint TERRITORY = 837;
    internal static bool Owns(uint oid) => oid is 0x278A or 0x278B or 0x278C;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x278A, "Forgiven Dissonance", p => HolminsterArenas.Dissonance(p.Y),
        [
            new(15813, 0x278A, "ThePathOfLight", null, "全屏伤害"),
            new(15816, 0x278A, "GibbetCage", new CircleTemplate(8)),
            new(15814, 0x278A, "Thumbscrew", null),
            new(15822, 0x2896, "HereticsFork", new DungeonCrossTemplate(40, 3)),
            new(15819, 0x2896, "LightShot", RayRectangleTemplate.Symmetric(4, 40)),
            new(15815, 0x278A, "WoodenHorse", new FanTemplate(40, MathF.PI / 2)),
            new(15812, 0x278A, "Pillory", null, "死刑"),
        ], [870, 15817]),
        new(0x278B, "Tesleen the Forgiven", p => HolminsterArenas.Tesleen(p.Y),
        [
            new(15823, 0x278B, "TheTickler", null, "死刑"),
            new(15824, 0x278B, "ScoldsBridle", null, "全屏伤害"),
            new(15829, 0x278B, "FeveredFlagellationVisual", null),
            new(15828, 0x233C, "HolyWater", null),
        ], [870, 15830, 15826, 15827, 15825]),
        new(0x278C, "Philia", p => HolminsterArenas.Philia(p.Y),
        [
            new(15832, 0x278C, "ScavengersDaughter", null, "全屏伤害"),
            new(15831, 0x278C, "HeadCrusher", null, "死刑"),
            new(16777, 0x278C, "Pendulum", null),
            new(16778, 0x233C, "PendulumAOE", new CircleTemplate(15)),
            new(17052, 0x278C, "ChainDown", null),
            new(15848, 0x278C, "Aethersup", null),
            new(15846, 0x278C, "RightKnout", new FanTemplate(24, MathF.PI * 7 / 6)),
            new(15847, 0x278C, "LeftKnout", new FanTemplate(24, MathF.PI * 7 / 6)),
            new(16769, 0x233C, "Taphephobia", new CircleTemplate(6), PartyAoe: "分散"),
            new(15834, 0x278C, "FierceBeatingRotationVisual", null),
            new(15837, 0x233C, "FierceBeatingExaFirst", null),
            new(15841, 0x233C, "CatONineTails", null),
        ], [872, 16790, 15833, 15849, 15842, 15844, 17232, 15845, 15836, 15835, 15838, 15839, 15840]),
    ];
}
