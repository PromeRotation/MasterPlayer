using MasterPlayer.Mechanics.EurekaOrthos;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.EurekaOrthos;

[MechanicScript("正统优雷卡楼层机制", [1099, 1100, 1101, 1102, 1103, 1104, 1105, 1106, 1107, 1108],
    "masterplayer.eureka.floors", "1.0.0", "MasterPlayer", note: "仅战斗机制、视线与陷阱；无道中流程自动化")]
public sealed class EurekaFloors : EurekaFloorScript;
