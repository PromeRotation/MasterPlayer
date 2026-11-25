using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Lord of the Inferno", [275], "masterplayer.quest.lord-of-the-inferno", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：其他敌人优先于 0x3C7。")]
public sealed class LordOfTheInferno() : QuestTargetScript(275);
