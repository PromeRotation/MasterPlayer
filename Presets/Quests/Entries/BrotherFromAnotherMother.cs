using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Brother from Another Mother", [314], "masterplayer.quest.brother-from-another-mother", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：0x815 显式优先级0，其余保留基础优先级。")]
public sealed class BrotherFromAnotherMother() : QuestTargetScript(314);
