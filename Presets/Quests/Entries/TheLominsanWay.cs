using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("The Lominsan Way", [307], "masterplayer.quest.the-lominsan-way", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：0x6EA 优先级1，0x6E9 显式优先级-1，其余保留基础优先级。")]
public sealed class TheLominsanWay() : QuestTargetScript(307);
