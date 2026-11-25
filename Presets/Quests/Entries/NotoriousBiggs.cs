using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Notorious Biggs", [304], "masterplayer.quest.notorious-biggs", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：区域全部合法敌人优先级归零，保留Pointless保护。")]
public sealed class NotoriousBiggs() : QuestTargetScript(304);
