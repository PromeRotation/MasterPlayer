using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Chasing Shadows", [233], "masterplayer.quest.chasing-shadows", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：优先其他敌人，其次 0x376，最后 0x224。")]
public sealed class ChasingShadows() : QuestTargetScript(233);
