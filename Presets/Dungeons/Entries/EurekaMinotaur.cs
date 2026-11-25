using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("正统优雷卡60层 自控化弥诺陶洛斯16", [1104], "masterplayer.eureka.60", "1.0.0", "PromeRadar",
    "原生机制：八连挥击、雷球创建预测及径向击退。仅Boss机制。")]
public sealed class EurekaMinotaur() : DungeonScript(1104);
