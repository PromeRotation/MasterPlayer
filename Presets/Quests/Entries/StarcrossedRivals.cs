using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Star-crossed Rivals", [260], "masterplayer.quest.starcrossed-rivals", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：0x2BD/0x2BE 优先，其次其他敌人，最后 0x2BA。")]
public sealed class StarcrossedRivals() : QuestTargetScript(260);
