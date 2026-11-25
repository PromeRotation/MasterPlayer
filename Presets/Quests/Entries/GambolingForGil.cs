using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("初次登台", [867u], "masterplayer.quest.gamboling-for-gil", "1.0.0", "MasterPlayer",
    "舞者职业任务：动态冲锋诱导、凝视、风圈及独立击退投影。")]
public sealed class GambolingForGil() : DungeonScript(867);
