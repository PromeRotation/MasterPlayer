using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.HeavenOnHigh;

[MechanicScript("天之御柱 10层 Boss", [770u], "masterplayer.hoh.10", "1.0.0", "XSZ",
    "亡者船原生读条、撞墙击退投影和精确场地；普通楼层与区域模块保持独立。")]
public sealed class HeavenOnHigh10() : DungeonScript(770);
