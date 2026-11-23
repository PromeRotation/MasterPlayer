using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class TenderValleyCatalog
{
    internal const uint TERRITORY = 1203;
    internal static bool Owns(uint oid) => oid is 0x4234 or 0x41BE or 0x4253 or 0x4164;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x4234, "Barreltender", p => new RectangleGeometry(new(-65, p.Y, 470), 0, 49, 49),
        [
            new(37384, 0, "HeavyweightNeedlesVisual", null),
            new(37386, 0, "HeavyweightNeedles", new FanTemplate(36, MathF.PI * 50 / 180)),
            new(37387, 0, "TenderDrop", null),
            new(37388, 0, "NeedleStorm", null),
            new(37389, 0, "NeedleSuperstorm", null),
            new(37390, 0, "BarrelBreaker", null),
            new(39154, 0, "PricklyRight", new FanTemplate(36, MathF.PI * 330 / 180)),
            new(39155, 0, "PricklyLeft", new FanTemplate(36, MathF.PI * 330 / 180)),
            new(37391, 0, "SucculentStomp", new CircleTemplate(6), PartyAoe: "分摊"),
            new(37392, 0, "BarbedBellow", null, "全屏伤害"),
            new(39242, 0, "TenderFury", null, "单体伤害"),
        ], [872, 37393]),
        new(0x41BE, "Anthracite", p => TenderValleyArenas.Anthracite(p.Y),
        [
            new(36401, 0, "Anthrabomb", new CircleTemplate(10)),
            new(36402, 0, "Anthrabomb", new CircleTemplate(10)),
            new(36545, 0, "HotBlast", RayRectangleTemplate.Symmetric(6, 40)),
            new(36551, 0, "HotBlast", RayRectangleTemplate.Symmetric(6, 40)),
            new(36553, 0, "AnthrabombSpread", new CircleTemplate(6), PartyAoe: "分散"),
            new(36555, 0, "BurningCoals", new CircleTemplate(6), PartyAoe: "分摊"),
            new(36557, 0, "CarbonaceousCombustion", null, "全屏伤害"),
            new(38468, 0, "ChimneySmack", null, "单体伤害"),
        ], [872, 36542, 36543, 36549, 36550, 36552, 36544, 36546, 36556, 36547, 36548, 36554, 38467]),
        new(0x4253, "Yok Huy Attestant", p => TenderValleyArenas.Attestant(p.Y),
        [
            new(39221, 0, "TectonicShift", new CircleTemplate(8)),
            new(38539, 0, "SunToss", new CircleTemplate(6)),
            new(38540, 0, "BoulderToss", null, "单体伤害"),
        ], [872, 38538, 39823, 39824, 39825]),
        new(0x4164, "Greatest Serpent of Tural", p => new RectangleGeometry(new(-130, p.Y, -554), 0, 29, 29),
        [
            new(36748, 0, "DubiousTulidisaster", null, "全屏伤害"),
            new(36745, 0, "GreatestLabyrinth", null, "全屏伤害"),
            new(36756, 0, "GreatestFlood", null, "全屏伤害"),
            new(36749, 0, "ExaltedWobble", new CircleTemplate(9)),
            new(36750, 0, "MisplacedMystery", RayRectangleTemplate.Symmetric(5, 52)),
            new(36754, 0, "GreatTorrent", new CircleTemplate(6)),
        ], [872, 36747, 36746, 36744, 36757, 36743, 36753, 36752, 36751, 39983, 39982, 39981, 36742, 36741, 36755]),
    ];
}
