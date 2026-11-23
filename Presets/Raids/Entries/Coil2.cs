using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Raids;

[MechanicScript("巴哈姆特大迷宫 邂逅之章2", [242u], "masterplayer.raid.coil2", "1.0.0", "XSZ",
    "七节点原生接管：连续压舱水、重力场、顺劈与病毒传递。病毒自动接取需设置完整且不重复的八槽分工。")]
public sealed class Coil2() : CoilScript(242);
