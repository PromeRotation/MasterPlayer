using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("正统优雷卡80层 Proto-Kaliya", [1106], "masterplayer.eureka.80", "1.0.0", "PromeRadar",
    "原生机制：磁力推拉、护罩环形场地、神经毒气、十字炮台及死刑。仅Boss机制。")]
public sealed class EurekaProtoKaliya() : DungeonScript(1106);
