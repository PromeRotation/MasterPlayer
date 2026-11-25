using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("The Power of a Tourney", [488], "masterplayer.quest.power-of-a-tourney", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：优先处理 0x1236。")]
public sealed class ThePowerOfATourney() : QuestTargetScript(488);
