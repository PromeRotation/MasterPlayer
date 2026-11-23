using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("对龙城塞石卫塔", [1042u], "masterplayer.dungeon.stone-vigil", "1.0.0", "XSZ",
    "三场精确场地、坦克顺劈、漩涡连续推进、俯冲及时间线落地预测。")]
public sealed class StoneVigil() : DungeonScript(1042);
