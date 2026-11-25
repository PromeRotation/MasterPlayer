using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Austerities of Flame", [323], "masterplayer.quest.austerities-of-flame", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：0x5EC 优先级5，其余保留基础优先级。")]
public sealed class AusteritiesOfFlame() : QuestTargetScript(323);
