using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("正统优雷卡70层 Aeturna", [1105], "masterplayer.eureka.70", "1.0.0", "PromeRadar",
    "原生机制：晶石碎裂预测、凶暴拉线、钢爪效果结算及范围。仅Boss机制。")]
public sealed class EurekaAeturna() : DungeonScript(1105);
