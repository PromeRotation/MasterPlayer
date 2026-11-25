using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("The Reason Roaille", [410], "masterplayer.quest.the-reason-roaille", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：HP恰1优先级0，0xDB7优先级2，其他合法敌人优先级1，保留Pointless保护。")]
public sealed class TheReasonRoaille() : QuestTargetScript(410);
