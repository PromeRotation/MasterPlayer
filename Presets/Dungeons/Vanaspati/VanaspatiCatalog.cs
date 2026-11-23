using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class VanaspatiCatalog
{
    internal const uint TERRITORY = 970;
    // Preserve the quantized cardinal angles used by the source packets.
    internal const float EAST = 89.999f * MathF.PI / 180;
    internal const float WEST = -90.004f * MathF.PI / 180;
    internal static bool Owns(uint oid) => oid is 0x33E8 or 0x33E9 or 0x33EB;
    internal static bool Misdirected(uint status) => status is 1422 or 2936 or 3694 or 3909;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x33E8, "Snatcher", p => VanaspatiArenas.Snatcher(p.Y),
        [
            new(25141, 0x33E8, "LastGasp", null, "死刑"),
            new(25143, 0x33E8, "LostHope", null, "即将获得误导"),
            new(25144, 0x33E8, "NoteOfDespair", null, "全屏伤害"),
            new(25138, 0x233C, "Vitriol", new CircleTemplate(13)),
            new(25142, 0x233C, "Wallow", new CircleTemplate(6), PartyAoe: "分散"),
            new(25140, 0x33E8, "WhatIsLeft", new FanTemplate(40, MathF.PI)),
            new(25139, 0x33E8, "WhatIsRight", new FanTemplate(40, MathF.PI)),
        ], [872, 25137]),
        new(0x33E9, "Wrecker", p => VanaspatiArenas.WreckerStarting(p.Y),
        [
            new(25147, 0x33E9, "AetherSprayFire", null, "全屏伤害"),
            new(25148, 0x33E9, "AetherSprayWater", null, "全屏伤害"),
            new(25153, 0x33E9, "MeaninglessDestruction", null, "全屏伤害"),
            new(27851, 0x233C, "PoisonHeartStack", new CircleTemplate(6), PartyAoe: "分摊"),
            new(25154, 0x33E9, "TotalWreck", null, "死刑"),
            new(27847, 0x3731, "Withdraw", null),
        ], [870, 25145, 25146, 25151, 27852]),
        new(0x33EB, "Svarbhanu", p => new RectangleGeometry(new(300, p.Y, -157), 0, 49, 49),
        [
            new(25167, 0x233C, "CosmicKissCircle", new CircleTemplate(6)),
            new(25168, 0x233C, "CosmicKissSpread", new CircleTemplate(6), PartyAoe: "分散"),
            new(25169, 0x233C, "CosmicKiss", null, "全屏伤害"),
            new(25170, 0x33EB, "FlamesOfDecay", null, "全屏伤害"),
            new(25171, 0x33EB, "GnashingOfTeeth", null, "死刑"),
        ], [872, 25160, 27489, 25164, 25165, 25162, 25163, 25161, 25374, 25166]),
    ];
}
