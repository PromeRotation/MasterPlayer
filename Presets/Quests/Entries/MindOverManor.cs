using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Mind Over Manor", [1233u], "masterplayer.quest.mind-over-manor", "1.0.0", "MasterPlayer",
    "精确自定义场地、三种落点读条、扇形及全屏伤害预测。")]
public sealed class MindOverManor() : DungeonScript(1233);
