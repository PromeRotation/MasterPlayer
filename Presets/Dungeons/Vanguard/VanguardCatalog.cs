using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class VanguardCatalog
{
    internal const uint TERRITORY = 1198;
    internal static bool IsTrash(uint oid) => oid is 0x447A or 0x4479 or 0x41DA or 0x43D4;
    internal static bool Owns(uint oid) => oid is 0x447A or 0x41D9 or 0x41D6 or 0x4478 or 0x4479 or 0x41D8
        or 0x41DA or 0x447B or 0x43D4 or 0x41DB or 0x411D or 0x4237 or 0x411E;
    internal static bool Enhanced(uint action) => action is 39140 or 39141 or 36559 or 36560 or 36563 or 36564 or 37184 or 37191;
    internal static bool Combo(uint action) => action is 36591 or 36592 or 36593;
    internal static uint SimpleGroup(uint action) => action switch
    {
        37160 or 37350 => 37160,
        36574 or 36589 or 36590 => 36574,
        37198 or 36580 or 36581 or 36582 or 36583 or 36585 or 36586 or 36587 or 36588 => 37198,
        _ => action,
    };
    internal static DungeonDefinition[] Create() =>
    [
        new(0x447A, "Vanguard Leptocyon", p => VanguardArenas.Leptocyon(p.Y),
            [new(39017, 0, "SpreadShot", new FanTemplate(12, MathF.PI / 2))], [872, 39019],
            [0x41D9, 0x41D6, 0x4478]),
        new(0x4479, "Vanguard Sentry R7", p => VanguardArenas.Sentry(p.Y),
        [
            new(38051, 0, "Swoop", new CircleTemplate(0)),
            new(38451, 0, "AerialDischarge", new DonutTemplate(4, 10)),
            new(39018, 0, "Electrostrike", new CircleTemplate(6)),
        ], [872], [0x41D8]),
        new(0x41DA, "Vanguard Aerostat I", p => new RectangleGeometry(new(-50, p.Y, -15), 0, 15.4f, 50),
            [new(38452, 0, "AerialDischarge", new DonutTemplate(3, 12))], [872], [0x447B]),
        new(0x43D4, "Vanguard Aerostat II", p => VanguardArenas.Aerostat(p.Y),
        [
            new(38452, 0, "AerialDischarge", new DonutTemplate(3, 12)),
            new(38060, 0, "AerialCannon", RayRectangleTemplate.Symmetric(4, 50)),
            new(39017, 0, "SpreadShot", new FanTemplate(12, MathF.PI / 2)),
        ], [872], [0x41D6, 0x4478, 0x41DB]),
        new(0x411D, "Commander R8", p => new RectangleGeometry(new(-100, p.Y, 207), 0, 39, 39),
        [
            new(36571, 0, "Electrowave", null, "全屏伤害"),
            new(39140, 0, "EnhancedMobility1", RayRectangleTemplate.Symmetric(6, 14)),
            new(39141, 0, "EnhancedMobility2", RayRectangleTemplate.Symmetric(6, 14)),
            new(36559, 0, "EnhancedMobility3", RayRectangleTemplate.Symmetric(6, 14)),
            new(36560, 0, "EnhancedMobility4", RayRectangleTemplate.Symmetric(6, 14)),
            new(36563, 0, "EnhancedMobility5", RayRectangleTemplate.Symmetric(14, 10)),
            new(36564, 0, "EnhancedMobility6", RayRectangleTemplate.Symmetric(14, 10)),
            new(37184, 0, "EnhancedMobility7", RayRectangleTemplate.Symmetric(14, 20)),
            new(37191, 0, "EnhancedMobility8", RayRectangleTemplate.Symmetric(14, 20)),
            new(36569, 0, "Rush", new CircleTemplate(0)),
            new(36570, 0, "AerialOffensive", new CircleTemplate(14)),
            new(36573, 0, "Electrosurge", new CircleTemplate(5), PartyAoe: "分散"),
        ], [36403, 36561, 36562, 39142, 39143, 36566, 36567, 36565, 36568, 36572]),
        new(0x4237, "Protector", p => new RectangleGeometry(new(0, p.Y, -100), 0, 29, 45),
        [
            new(37161, 0, "Electrowave", null, "全屏伤害"),
            new(37151, 0, "BlastCannon", RayRectangleTemplate.Symmetric(4, 26)),
            new(37155, 0, "HomingCannon", RayRectangleTemplate.Symmetric(2, 50)),
            new(37156, 0, "Shock", new CircleTemplate(3)),
            new(37351, 0, "BatteryCircuit", new FanTemplate(30, MathF.PI / 6)),
            new(37162, 0, "RapidThunder", null, "单体伤害"),
            new(39016, 0, "Bombardment", new CircleTemplate(5)),
            new(37160, 0, "Electrowhirl1", new CircleTemplate(6)),
            new(37350, 0, "Electrowhirl2", new CircleTemplate(6)),
            new(37349, 0, "TrackingBolt", new CircleTemplate(8), PartyAoe: "分散"),
            new(37345, 0, "HeavyBlastCannon", null),
        ], [878, 37154, 37153, 37149, 37158, 37159, 37344, 37150, 37348, 37343, 37347]),
        new(0x411E, "Zander", p => VanguardArenas.ZanderStarting(p.Y),
        [
            new(36575, 0, "Burst1", RayRectangleTemplate.Symmetric(40, 20)),
            new(36591, 0, "Burst2", RayRectangleTemplate.Symmetric(80, 20)),
            new(36594, 0, "Electrothermia", null, "全屏伤害"),
            new(36595, 0, "SaberRush", null, "单体伤害"),
            new(36596, 0, "Screech", null, "全屏伤害"),
            new(36597, 0, "ShadeShot", null, "单体伤害"),
            new(36574, 0, "SoulbaneSaber", RayRectangleTemplate.Symmetric(4, 20)),
            new(36589, 0, "SlitherbaneForeguard", RayRectangleTemplate.Symmetric(4, 20)),
            new(36590, 0, "SlitherbaneRearguard", RayRectangleTemplate.Symmetric(4, 20)),
            new(36592, 0, "SlitherbaneForeguardCone", new FanTemplate(20, MathF.PI)),
            new(36593, 0, "SlitherbaneRearguardCone", new FanTemplate(20, MathF.PI)),
            new(37922, 0, "SoulbaneShock", new CircleTemplate(5), PartyAoe: "分散"),
            new(37198, 0, "Syntheslean", new FanTemplate(19, MathF.PI / 2)),
            new(36580, 0, "Syntheslither1", new FanTemplate(19, MathF.PI / 2)),
            new(36581, 0, "Syntheslither2", new FanTemplate(19, MathF.PI / 2)),
            new(36582, 0, "Syntheslither3", new FanTemplate(19, MathF.PI / 2)),
            new(36583, 0, "Syntheslither4", new FanTemplate(19, MathF.PI / 2)),
            new(36585, 0, "Syntheslither5", new FanTemplate(19, MathF.PI / 2)),
            new(36586, 0, "Syntheslither6", new FanTemplate(19, MathF.PI / 2)),
            new(36587, 0, "Syntheslither7", new FanTemplate(19, MathF.PI / 2)),
            new(36588, 0, "Syntheslither8", new FanTemplate(19, MathF.PI / 2)),
        ], [870, 39240, 39241, 36579, 36584, 36576, 36577, 36578]),
    ];
}
