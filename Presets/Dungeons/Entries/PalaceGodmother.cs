using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("死者宫殿90层 The Godmother", [596], "masterplayer.palace.godmother", "1.0.0", "PromeRadar",
    "原生机制；楼层、道中与交互继续由独立模块处理")]
public sealed class PalaceGodmother() : DungeonScript(596);
