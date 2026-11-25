using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("雷天军星", [671u], "masterplayer.quest.rhalgrs-beacon", "1.0.0", "MasterPlayer",
    "Fordola：动态终结剑阵、毒圈逐项交接及独立导航/落点判定的撞墙击退。")]
public sealed class RhalgrsBeacon() : DungeonScript(671);
