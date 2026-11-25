using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("死者宫殿60层 The Black Rider", [593], "masterplayer.palace.blackrider", "1.0.0", "PromeRadar",
    "原生机制：动态冲锋、独立径向击退、方向禁区及持续毒区。")]
public sealed class PalaceBlackRider() : DungeonScript(593);
