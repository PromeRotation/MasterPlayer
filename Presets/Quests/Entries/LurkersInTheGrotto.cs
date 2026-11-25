using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Lurkers in the Grotto", [252], "masterplayer.quest.lurkers-in-the-grotto", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：优先处理 0x386 以外的敌人。")]
public sealed class LurkersInTheGrotto() : QuestTargetScript(252);
