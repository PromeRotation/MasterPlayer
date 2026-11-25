using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("衣锦还乡", [634u], "masterplayer.quest.his-forgotten-home", "1.0.0", "MasterPlayer",
    "Slickshell Captain：图标诱导、读条/效果/实体毒池及护卫优先级；保留任务区域内的完整阶段。")]
public sealed class HisForgottenHome() : DungeonScript(634);
