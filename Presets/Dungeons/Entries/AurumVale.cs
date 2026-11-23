using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("毒雾洞窟黄金谷", [1331u], "masterplayer.dungeon.aurum-vale", "1.0.0", "XSZ",
    "三场重制首领、延迟死刑、270度月环扇形、三层减益果实站位与交互。")]
public sealed class AurumVale() : DungeonScript(1331);
