using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Familiar Faces", [461], "masterplayer.quest.familiar-faces", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：0x106C 优先，其他合法敌人保留零优先级。")]
public sealed class FamiliarFaces() : QuestTargetScript(461);
