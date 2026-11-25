using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("正统优雷卡20层 复制系统", [1100], "masterplayer.eureka.20", "1.0.0", "PromeRadar",
    "原生机制：真实效果驱动的吐息批次、锁定位置及全局结束FIFO。仅Boss机制。")]
public sealed class EurekaCloningNode() : DungeonScript(1100);
