using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("谎言的泡沫", [863u], "masterplayer.quest.a-feast-of-lies", "1.0.0", "MasterPlayer",
    "Ran'jit：十四组件、连续前五火圈、恰十二雷圈的选敌交接及撞墙击退。")]
public sealed class AFeastOfLies() : DungeonScript(863);
