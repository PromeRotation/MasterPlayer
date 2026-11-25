using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("The Crow Knows", [414], "masterplayer.quest.the-crow-knows", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：0xDC9优先级3，0xDBE优先级1，其他合法敌人优先级2。")]
public sealed class TheCrowKnows() : QuestTargetScript(414);
