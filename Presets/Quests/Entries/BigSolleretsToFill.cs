using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Big Sollerets to Fill", [498], "masterplayer.quest.big-sollerets-to-fill", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：0x11D2 优先级5，其余保留基础优先级。")]
public sealed class BigSolleretsToFill() : QuestTargetScript(498);
