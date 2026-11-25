using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("失传的力量", [1091u], "masterplayer.quest.where-everything-begins", "1.0.0", "MasterPlayer",
    "Scarmiglione 第一阶段：小怪触发开战、前五暗黑圈、NPC 动态分散与分摊。")]
public sealed class WhereEverythingBegins() : DungeonScript(1091);
