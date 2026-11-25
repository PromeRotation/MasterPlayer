using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("The Hands of Fate", [476], "masterplayer.quest.hands-of-fate", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：优先处理 0x1268–0x126D。")]
public sealed class TheHandsOfFate() : QuestTargetScript(476);
