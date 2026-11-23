using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("最终决战天幕魔导城", [1044u], "masterplayer.dungeon.praetorium", "1.0.0", "XSZ",
    "三场首领、分散分摊、尼禄场地变化与击退站位、盖乌斯提前射线和分身倒计时。")]
public sealed class Praetorium() : DungeonScript(1044);
