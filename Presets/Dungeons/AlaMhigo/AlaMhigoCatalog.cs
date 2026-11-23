using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AlaMhigoCatalog
{
    internal const uint TERRITORY = 1146;
    internal static bool Owns(uint oid) => oid is 0x1BA4 or 0x1BA6 or 0x1BAA;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x1BA4, "Magitek Scorpion", p => AlaMhigoArenas.Scorpion(p.Y),
        [
            new(8269, 0x1BA4, "ElectromagneticField", null, "全屏伤害"),
            new(8265, 0x18D6, "TailLaserFront", null),
            new(8266, 0x18D6, "TailLaserBack", null),
        ], [9303, 8262, 8263, 8264, 8267, 8268]),
        new(0x1BA6, "Aulus mal Asina", p => AlaMhigoArenas.Aulus(p.Y),
        [
            new(8271, 0x1BA6, "ManaBurst", null, "全屏伤害"),
            new(8282, 0x1BA7, "AetherochemicalGrenado", new CircleTemplate(8)),
            new(8283, 0x1BA7, "IntegratedAetheromodulator", new DonutTemplate(11.4f, 15.6f)),
            new(8276, 0x1BA8, "MagitekRay", RayRectangleTemplate.Symmetric(2, 45.6f)),
            new(8286, 0x18D6, "Demimagicks", new CircleTemplate(5), PartyAoe: "分散"),
        ], [9304, 8279, 8275, 8280, 8272, 8274, 8273, 8270, 8278, 8285]),
        new(0x1BAA, "Zenos yae Galvus", p => new RectangleGeometry(new(250, p.Y, -353), 0, 45, 45),
        [
            new(8293, 0x1BAA, "ArtOfTheSwell", null, "击退15码：进入中央"),
            new(9606, 0x1BAB, "ArtOfTheSwellClone", null, "击退15码：进入中央"),
            new(8289, 0x18D6, "UnmovingTroikaSecond", new FanTemplate(9.96f, MathF.PI * 2 / 3)),
            new(8290, 0x18D6, "UnmovingTroikaLast", new FanTemplate(9.96f, MathF.PI * 2 / 3)),
            new(8294, 0x1BAA, "ArtOfTheStorm", new CircleTemplate(8)),
            new(9607, 0x1BAB, "ArtOfTheStormClone", new CircleTemplate(8)),
            new(8296, 0x1BAA, "ArtOfTheSword", null),
            new(9608, 0x1BAB, "ArtOfTheSwordClone", null),
            new(9398, 0x1BAA, "VeinSplitter", new CircleTemplate(10)),
            new(8300, 0x1BAB, "VeinSplitterClone", new CircleTemplate(10)),
            new(8299, 0x1BAA, "LightlessSpark", new FanTemplate(40.96f, MathF.PI / 2)),
            new(8301, 0x1BAA, "Concentrativity", null, "全屏伤害"),
        ], [870, 8292, 9119, 8287, 9140, 9300, 8288, 8295, 8297, 9609, 717, 8302, 8303, 9118, 8304]),
    ];
}
