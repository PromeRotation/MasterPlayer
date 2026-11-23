using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class TwintaniaCatalog
{
    internal const uint TERRITORY = 245;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x7E5, "Twintania", p => new CircleGeometry(new Vector3(-3, p.Y, -6.5f), 31),
        [
            new(1458, 0, "DeathSentence", null, "死亡宣告：换坦与减伤"),
            new(1243, 0, "LiquidHellAdds", null),
            new(1247, 0, "Divebomb", null),
            new(1249, 0, "Twister", null, "旋风：保持移动"),
            new(670, 0, "LiquidHellBoss", null),
        ], [1461, 870, 1240, 1241, 1242, 1452, 1246, 1451, 1245, 1456, 669, 1212, 1248, 671, 1250, 1251, 1257, 1453, 1256, 1457])
    ];
}
