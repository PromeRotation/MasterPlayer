using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("古代遗迹喀恩埋没圣堂", [1267u], "masterplayer.dungeon.qarn", "1.0.0", "XSZ",
    "三场首领原始场地、死宣发光平台站位、连续秘银柱射线及小怪优先级。")]
public sealed class Qarn() : DungeonScript(1267);
