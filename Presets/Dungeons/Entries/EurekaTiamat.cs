using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("正统优雷卡30层 提亚马特复制体", [1101], "masterplayer.eureka.30", "1.0.0", "PromeRadar",
    "原生机制：五连追踪、移动头部预测及双翼/龙尾范围。仅Boss机制。")]
public sealed class EurekaTiamat() : DungeonScript(1101);
