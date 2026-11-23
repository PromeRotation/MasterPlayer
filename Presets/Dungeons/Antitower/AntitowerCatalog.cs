using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AntitowerCatalog
{
    internal const uint TERRITORY = 1111;
    internal static bool IsNpcDoll(uint oid) => oid is 0x1503 or 0x1504 or 0x1502;
    internal static bool IsPlayerDoll(uint oid) => oid is 0x15E0 or 0x15E1 or 0x15E2 or 0x15E3;
    internal static bool Owns(uint oid) => oid is 0x14FC or 0x3D82 || IsNpcDoll(oid);

    internal static DungeonDefinition[] Create() =>
    [
        new(0x14FC, "Zuro Roggo", p => AntitowerArenas.ZuroRoggo(p.Y),
        [
            new(5538, 0x233C, "WaterBomb1", new CircleTemplate(6)),
            new(5979, 0x233C, "WaterBomb2", new CircleTemplate(6)),
            new(5977, 0x233C, "WaterBomb3", new CircleTemplate(6)),
            new(5539, 0x14FC, "ToyHammer", null, "死刑"),
        ], [872, 5537, 32370, 5540, 5542, 5543, 5541]),
        new(0x3D82, "Ziggy", p => AntitowerArenas.Ziggy(p.Y),
        [
            new(31835, 0x3D82, "GyratingGlare", null, "全屏伤害"),
            new(31838, 0x3D83, "MysticLight", new CircleTemplate(12)),
            new(31839, 0x3D83, "DeepFracture", new CircleTemplate(11)),
            new(31832, 0x3D82, "JitteringGlare", new FanTemplate(40, MathF.PI / 6)),
            new(31837, 0x3D82, "JitteringJab", null, "死刑"),
        ], [872, 31836, 31831, 31833, 31840]),
        new(0x1503, "Calcabrina", p => AntitowerArenas.Calcabrina(p.Y),
        [
            new(5552, 0x1504, "HeatGazeBrina", new DonutTemplate(5, 10)),
            new(5552, 0x15E3, "HeatGazeBrinaPlayer", new DonutTemplate(5, 10)),
            new(5552, 0x15E1, "HeatGazeBrinaPlayer", new DonutTemplate(5, 10)),
            new(5551, 0x1503, "HeatGazeCalca", new FanTemplate(19.9f, MathF.PI / 3)),
            new(5551, 0x15E2, "HeatGazeCalcaPlayer", new FanTemplate(19.9f, MathF.PI / 3)),
            new(5551, 0x15E0, "HeatGazeCalcaPlayer", new FanTemplate(19.9f, MathF.PI / 3)),
            new(5556, 0x1502, "Knockout", null, "死刑"),
            new(5557, 0x1502, "Brace", null, "从未招架方向攻击"),
        ], [872, 5554, 5553, 5555, 5559, 5558, 5561, 5562, 5810, 5563, 5560], [0x1504, 0x1502]),
    ];
}
