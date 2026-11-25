using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Every Little Thing She Does Is Magitek", [309], "masterplayer.quest.every-little-thing-she-does-is-magitek", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：区域全部合法敌人优先级归零，保留Pointless保护。")]
public sealed class EveryLittleThingSheDoesIsMagitek() : QuestTargetScript(309);
