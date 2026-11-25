using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("正统优雷卡90层 Administrator", [1107], "masterplayer.eureka.90", "1.0.0", "PromeRadar",
    "原生机制：拦截序列、连续激光、十字与环形范围。仅Boss机制。")]
public sealed class EurekaAdministrator() : DungeonScript(1107);
