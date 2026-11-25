using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("正统优雷卡50层 自控化奇美拉14X", [1103], "masterplayer.eureka.50", "1.0.0", "PromeRadar",
    "原生机制：冰雷双声队列、动态冲锋、追踪球与拉线站位。仅Boss机制。")]
public sealed class EurekaChimera() : DungeonScript(1103);
