using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("The Face of True Evil", [709u], "masterplayer.quest.the-face-of-true-evil", "1.0.0", "MasterPlayer",
    "武藏任务战：锁定读条范围、分身动态Arashi、共享判定时钟及全队伤害预测。")]
public sealed class TheFaceOfTrueEvil() : DungeonScript(709);
