using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("死者宫殿120层 Kirtimukha", [599], "masterplayer.palace.kirtimukha", "1.0.0", "PromeRadar",
    "原生机制；楼层、道中与交互继续由独立模块处理")]
public sealed class PalaceKirtimukha() : DungeonScript(599);
