using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class HeroesCatalog
{
    internal const uint TERRITORY = 916;
    internal static bool Owns(uint oid) => oid is 0x2DEC or 0x2DF1 or 0x2EFD;
    internal static readonly uint[] BURSTS = [20322, 21429, 21430, 21431, 20324, 21432, 21433, 21434];
    internal static DungeonDefinition[] Create() =>
    [
        new(0x2DEC, "Spectral Thief", p => new RectangleGeometry(new(-680, p.Y, 450), 0, 39, 39),
        [
            new(20427, 0x2DEC, "SpectralDream", null, "死刑"),
            new(20428, 0x2DEC, "SpectralWhirlwind", null, "全屏伤害"),
            new(21455, 0x233C, "SpectralGust", new CircleTemplate(5), PartyAoe: "分散"),
            new(20439, 0x2E71, "CowardsCunning", RayRectangleTemplate.Symmetric(2, 60)),
        ], [870, 20437, 20501, 21454, 20438, 20435, 20436, 20429, 20430, 20431, 20432, 20434, 20433, 20577, 20578]),
        new(0x2DF1, "Spectral Necromancer", p => HeroesArenas.Necromancer(p.Y),
        [
            new(20321, 0x2DF1, "AbsoluteDarkII", new FanTemplate(40, 2 * MathF.PI / 3)),
            new(20318, 0x2DF1, "TwistedTouch", null, "死刑"),
            new(20320, 0x2DF1, "ChaosStorm", null, "全屏伤害"),
            new(20388, 0x233C, "PainMire", new CircleTemplate(9)),
            new(20317, 0x233C, "DarkDeluge", new CircleTemplate(5)),
            .. BURSTS.Select((action, i) => new DungeonCastRule(action, (uint)(0x2DF2 + i), "Burst", null)),
        ], [6499, 20305, 20311, 20312, 20313, 20314, 20387, 20323, 20316]),
        new(0x2EFD, "Spectral Berserker", p => new RectangleGeometry(new(750, p.Y, 482), 0, 45, 45),
        [
            new(21004, 0x2EFD, "BeastlyFury", null, "全屏伤害"),
            new(21000, 0x2EFD, "WildAnguish", null, "分摊或各自接碎石"),
            new(20995, 0x233C, "WildRage", new CircleTemplate(8)),
            new(20996, 0x233C, "WildRageKnockback", null, "全屏伤害与击退"),
            new(20999, 0x233C, "WildRampage", null, "进入坑洞"),
            new(21002, 0x2EFD, "RagingSliceFirst", RayRectangleTemplate.Symmetric(6, 50)),
            new(21003, 0x2EFD, "RagingSliceRest", RayRectangleTemplate.Symmetric(6, 50)),
        ], [870, 21001, 20997, 20994, 20998]),
    ];
}
