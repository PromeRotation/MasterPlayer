using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Shades of Shatotto", [669], "masterplayer.quest.shades-of-shatotto", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：0x1AE8保留零优先级，其他合法敌人优先级1。")]
public sealed class ShadesOfShatotto() : QuestTargetScript(669);
