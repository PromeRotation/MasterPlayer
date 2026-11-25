using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.HeavenOnHigh;

[MechanicScript("天之御柱 80层 Boss", [784u], "masterplayer.hoh.80", "1.0.0", "XSZ",
    "锻冶媪原生读条、冰球创建预测及真实效果消费，保留楼层机制。")]
public sealed class HeavenOnHigh80() : DungeonScript(784);
