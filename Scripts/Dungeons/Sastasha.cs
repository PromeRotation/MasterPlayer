using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("沙斯塔夏浸蚀洞窟", [1036u], "masterplayer.dungeon.sastasha", "1.0.0", "XSZ",
    "BMR 普通版：Chopper、Denn、水纹与珊瑚提示；目标优先级及绘制差异见迁移记录，待实机验收。")]
public sealed class Sastasha() : DungeonScript(1036);
