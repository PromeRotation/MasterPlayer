using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Oh Captain, My Captain", [273], "masterplayer.quest.oh-captain-my-captain", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：0x3BC 优先，其他敌人保留仇恨、队伍、无敌状态及 FATE 的基础优先级。")]
public sealed class OhCaptainMyCaptain() : QuestTargetScript(273);
