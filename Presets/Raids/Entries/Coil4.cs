using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Raids;

[MechanicScript("巴哈姆特大迷宫 邂逅之章4", [244u], "masterplayer.raid.coil4", "1.0.0", "XSZ",
    "原生六波状态与终端结束条件，巨兵顺劈、车点名绕背和集火优先级。")]
public sealed class Coil4() : DungeonScript(244);
