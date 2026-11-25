using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("暗杀芝诺斯", [685u], "masterplayer.quest.time-between-the-seconds", "1.0.0", "MasterPlayer",
    "芝诺斯双阶段、创建火圈、连续范围攻击与全屏预测；保留源组件的阶段清理规则。")]
public sealed class TheTimeBetweenTheSeconds() : DungeonScript(685);
