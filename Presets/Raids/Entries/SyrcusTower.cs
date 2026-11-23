using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Raids;

[MechanicScript("水晶塔 希尔科斯塔", [372u], "masterplayer.alliance.syrcus", "1.0.0", "XSZ",
    "原生四首领：独立辅助体范围、环形与直线、全屏伤害预告及谢幕冰块遮挡。")]
public sealed class SyrcusTower() : DungeonScript(372);
