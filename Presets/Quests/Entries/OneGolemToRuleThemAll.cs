using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("One Golem to Rule Them All", [673], "masterplayer.quest.one-golem-to-rule-them-all", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：两种陨石碎片优先级3，增援优先级2，其他合法敌人优先级1。")]
public sealed class OneGolemToRuleThemAll() : QuestTargetScript(673);
