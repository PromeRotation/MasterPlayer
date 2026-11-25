using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Quarantine", [466], "masterplayer.quest.quarantine", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：优先处理 0x1231。")]
public sealed class Quarantine() : QuestTargetScript(466);
