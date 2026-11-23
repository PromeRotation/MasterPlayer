using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class SohmAlCatalog
{
    internal const uint TERRITORY = 1064;

    internal static DungeonDefinition[] Create() =>
    [
        new(0xE8F, "Raskovnik", p => SohmAlArenas.Raskovnik(p.Y),
        [
            new(3799, 0x1B2, "AcidRain", new CircleTemplate(6)),
            new(3797, 0xE8F, "Phytobeam", RayRectangleTemplate.Symmetric(12, 48.68f)),
            new(5010, 0xE8F, "FlowerDevour", new CircleTemplate(8)),
            new(3798, 0xE8F, "Leafstorm", null, "全屏伤害"),
        ], [3793, 3794, 5013, 5009, 5011, 5012]),
        new(0xE91, "Myath", p => SohmAlArenas.Myath(p.Y),
        [
            new(3804, 0xE91, "RazorScales", new FanTemplate(64.9f, MathF.PI / 3)),
            new(3810, 0xE91, "PrimordialRoar", null, "全屏伤害"),
            new(3808, 0xE91, "MadDashSpread", new CircleTemplate(6), PartyAoe: "分散"),
            new(3809, 0xE91, "MadDashStack", new CircleTemplate(6), PartyAoe: "分摊"),
        ], [4994, 3803, 3805, 4995]),
        new(0xE96, "Tioman", p => SohmAlArenas.Tioman(p.Y),
        [
            new(3813, 0xE96, "ChaosBlastCircle", new CircleTemplate(2)),
            new(3819, 0x1B2, "ChaosBlastRect", RayRectangleTemplate.Symmetric(4, 50.5f)),
            new(3816, 0x1B2, "Comet", new CircleTemplate(4)),
            new(4999, 0x1B2, "MeteorImpact", new CircleTemplate(20)),
            new(3818, 0x1B2, "Heavensfall", new CircleTemplate(5)),
            new(3812, 0xE96, "DarkStar", null, "全屏伤害"),
        ], [3811, 3814, 4997, 3815, 3817]),
    ];
}
