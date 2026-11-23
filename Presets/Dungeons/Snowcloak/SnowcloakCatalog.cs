using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class SnowcloakCatalog
{
    internal const uint TERRITORY = 1062;
    internal static readonly uint[] TRASH = [0xD1E, 0xD31, 0xD1F, 0xD1B];

    internal static DungeonDefinition[] Create() =>
    [
        new(0xD1E, "Northern Bateleur", p => SnowcloakArenas.NorthernStarting(p.Y),
        [
            new(331, 0xD1E, "WingCutter", new FanTemplate(6.9f, MathF.PI / 3)),
            new(3259, 0xD1F, "DoubleSmash", new FanTemplate(7.69f, MathF.PI * 2 / 3)),
            new(31264, 0xD1B, "SicklySneeze", new FanTemplate(7.9f, MathF.PI / 2)),
        ], [], [0xD31, 0xD1F, 0xD1B]),
        new(0xD07, "Wandil", p => SnowcloakArenas.WandilStarting(p.Y),
        [
            new(3080, 0xD07, "SnowDrift", null, "持续移动，避免冻结"),
            new(3111, 0xD2E, "ColdWave", new CircleTemplate(8)),
            new(3082, 0xD07, "Tundra", null, "场地即将缩小"),
        ], [3083]),
        new(0x3977, "Yeti", p => SnowcloakArenas.Yeti(p.Y),
        [
            new(29585, 0x3977, "Buffet", new FanTemplate(12, MathF.PI * 2 / 3)),
            new(29582, 0x3977, "Northerlies", null, "全屏伤害"),
            new(29589, 0x233C, "HeavySnow", new CircleTemplate(15)),
            new(29590, 0x233C, "LightSnow", new CircleTemplate(2)),
            new(29586, 0x3977, "Spin", new CircleTemplate(11)),
            new(29591, 0x233C, "FrozenCircle", new DonutTemplate(10, 40)),
            new(29592, 0x233C, "FrozenSpike", new CircleTemplate(5), PartyAoe: "分散"),
        ], [29583, 29584, 29588, 29587, 25583]),
        new(0x3979, "Fenrir", p => SnowcloakArenas.Fenrir(p.Y),
        [
            new(29595, 0x233C, "ThousandYearStorm", null, "全屏伤害"),
            new(29600, 0x397A, "PillarImpact", new CircleTemplate(4)),
            new(29599, 0x3979, "LunarCry", null, "躲到完整冰柱后"),
            new(29648, 0x397A, "PillarShatter", new CircleTemplate(8)),
            new(29601, 0x397A, "PillarShatterFollowup", new CircleTemplate(8)),
            new(29596, 0x3979, "EclipticBite", null, "死刑"),
            new(29593, 0x3979, "HeavenswardRoar", new FanTemplate(50, MathF.PI / 3)),
        ], [29594]),
    ];
}
