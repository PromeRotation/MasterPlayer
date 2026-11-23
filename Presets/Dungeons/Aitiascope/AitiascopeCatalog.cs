using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AitiascopeCatalog
{
    internal const uint TERRITORY = 978;
    internal static bool Owns(uint oid) => oid is 0x3469 or 0x346B or 0x346E;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x3469, "Livia", p => AitiascopeArenas.Livia(p.Y),
        [
            new(25673, 0x3469, "AglaeaBite", new FanTemplate(9, MathF.Tau / 3), "死刑"),
            new(25668, 0x233C, "AglaeaClimb", new FanTemplate(20, MathF.PI / 2)),
            new(25670, 0x346A, "AglaeaShotFirst", RayRectangleTemplate.Symmetric(6, 20)),
            new(25671, 0x346A, "AglaeaShotReturn", RayRectangleTemplate.Symmetric(6, 20)),
            new(25674, 0x3469, "Disparagement", new FanTemplate(40, MathF.Tau / 3)),
            new(25672, 0x3469, "Frustration", null, "全屏伤害"),
            new(25676, 0x233C, "IgnisAmoris", new CircleTemplate(6)),
            new(25677, 0x233C, "IgnisOdi", new CircleTemplate(6), PartyAoe: "分摊"),
        ], [24771, 25666, 25667, 25669, 25675]),
        new(0x346B, "Rhitahtyn", p => new RectangleGeometry(new(11, p.Y, 144), 0, 39, 39),
        [
            new(25686, 0x346B, "AnvilOfTartarus", null, "死刑"),
            new(25679, 0x233C, "Impact", RayRectangleTemplate.Symmetric(40, 14)),
            new(25680, 0x346B, "ShieldSkewer", RayRectangleTemplate.Symmetric(14, 40)),
            new(25684, 0x233C, "ShrapnelShell", new CircleTemplate(6)),
            new(25685, 0x346B, "TartareanImpact", null, "全屏伤害"),
            new(25687, 0x346B, "TartareanSpark", RayRectangleTemplate.Symmetric(6, 40)),
        ], [870, 25688, 25682, 25683, 25678]),
        new(0x346E, "Amon", p => AitiascopeArenas.Amon(p.Y),
        [
            new(25700, 0x346E, "DarkForte", null, "死刑"),
            new(25701, 0x346E, "Entracte", null, "全屏伤害"),
            new(27756, 0x233C, "DreamsOfIce", new CircleTemplate(6)),
            new(25702, 0x346E, "CurtainCall", null, "躲到冰块背后"),
            new(25695, 0x233C, "Epode", RayRectangleTemplate.Symmetric(12, 70)),
            new(25704, 0x233C, "EruptionForte", new CircleTemplate(8)),
            new(25697, 0x346E, "LeftFiragaForte", RayRectangleTemplate.Symmetric(20, 40)),
            new(25696, 0x346E, "RightFiragaForte", RayRectangleTemplate.Symmetric(20, 40)),
            new(25690, 0x346E, "ThundagaForteProximity", new CircleTemplate(15)),
            new(25691, 0x233C, "ThundagaForteFirst", new FanTemplate(20, MathF.PI / 4)),
            new(25692, 0x233C, "ThundagaForteSecond", new FanTemplate(20, MathF.PI / 4)),
        ], [24712, 25694, 24709, 25693, 25703]),
    ];
}
