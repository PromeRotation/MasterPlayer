using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("死者宫殿100层 Nybeth Obdilord", [597], "masterplayer.palace.nybeth", "1.0.0", "PromeRadar",
    "原生机制；楼层、道中与交互继续由独立模块处理")]
public sealed class PalaceNybeth() : DungeonScript(597);
