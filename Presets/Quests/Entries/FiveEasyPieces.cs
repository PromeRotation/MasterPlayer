using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Five Easy Pieces", [315], "masterplayer.quest.five-easy-pieces", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：其他敌人优先于 0x640。")]
public sealed class FiveEasyPieces() : QuestTargetScript(315);
