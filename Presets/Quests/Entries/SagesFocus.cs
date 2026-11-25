using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Sage's Focus", [1022u], "masterplayer.quest.sages-focus", "1.0.0", "MasterPlayer",
    "贤者任务战：精确场地、独立读条范围、NPC分散及单体/全队伤害预测。")]
public sealed class SagesFocus() : DungeonScript(1022);
