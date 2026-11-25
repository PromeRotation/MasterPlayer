using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("正统优雷卡10层 蜜言妖", [1099], "masterplayer.eureka.10", "1.0.0", "PromeRadar",
    "原生机制：地雷前六项顺序晋升及冲击范围。仅Boss机制。")]
public sealed class EurekaGancanagh() : DungeonScript(1099);
