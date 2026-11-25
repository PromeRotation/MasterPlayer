using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("英雄无法安息", [914u], "masterplayer.quest.a-sleep-disturbed", "1.0.0", "MasterPlayer",
    "Huaca：双独立连线诱导、NPC分散分摊及真实读条结束。任务交互阶段保留原模块。")]
public sealed class ASleepDisturbed() : DungeonScript(914);
