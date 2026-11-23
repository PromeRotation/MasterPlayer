using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("皇都伊修加德保卫战", [1068u], "masterplayer.quest.steps-of-faith", "1.0.0", "XSZ",
    "原生维沙普：六阶段滚动场地、连续地火、双吐息计数、分散和停墙击退。")]
public sealed class TheStepsOfFaith() : DungeonScript(1068);
