using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("正统优雷卡40层 双塔尼亚复制体", [1102], "masterplayer.eureka.40", "1.0.0", "PromeRadar",
    "原生机制：双旋风采样、俯冲锁向、风池交接及涡轮击退。仅Boss机制。")]
public sealed class EurekaTwintania() : DungeonScript(1102);
