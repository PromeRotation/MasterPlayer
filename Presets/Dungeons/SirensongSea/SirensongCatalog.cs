using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class SirensongCatalog
{
    internal const uint TERRITORY = 1142;
    internal static bool Owns(uint oid) => oid is 0x1AFB or 0x1AFC or 0x1AFE;
    internal static uint KnockbackAction(uint job) => job switch
    {
        6 or 7 or 24 or 25 or 26 or 27 or 28 or 33 or 35 or 36 or 40 or 42 => 7559,
        1 or 2 or 3 or 4 or 5 or 19 or 20 or 21 or 22 or 23 or 29 or 30 or 31 or 32 or 34 or 37 or 38 or 39 or 41 => 7548,
        _ => 0,
    };
    internal static DungeonDefinition[] Create() =>
    [
        new(0x1AFB, "Lugat", p => SirensongArenas.Lugat(p.Y),
        [
            new(8022, 0x1AFB, "AmorphousApplause", new FanTemplate(24.5f, MathF.PI)),
            new(8023, 0x1AFB, "Hydroball", null, "四人分摊"),
            new(8024, 0x1AFB, "SeaSwallowsAll", null, "全屏伤害"),
            new(8025, 0x1AFB, "Overtow", null, "全屏伤害"),
            new(8026, 0x1AFB, "ConcussiveOscillation", new CircleTemplate(7)),
            new(8027, 0x18D6, "ConcussiveOscillation", new CircleTemplate(8)),
        ], [872]),
        new(0x1AFC, "The Governor", p => SirensongArenas.Governor(p.Y),
        [
            new(8030, 0x1AFC, "ShadowFlow", null),
            new(8032, 0x1AFC, "EnterNight", null, "抗击退或远离首领拉断连线"),
            new(8033, 0x1AFC, "ShadowSplit", null),
        ], [872, 8031, 8034, 8029, 8028]),
        new(0x1AFE, "Lorelei", p => SirensongArenas.Lorelei(p.Y),
        [
            new(8041, 0x1AFE, "VirginTears", null),
            new(8037, 0x1AFE, "MorbidAdvance", null, "强制前进：准备面向安全落点"),
            new(8038, 0x1AFE, "MorbidRetreat", null, "强制后退：准备背向安全落点"),
            new(8039, 0x1AFE, "SomberMelody", null, "全屏伤害"),
            new(8040, 0x1AFE, "VoidWaterIII", new CircleTemplate(8)),
        ], [8035, 8036]),
    ];
}
