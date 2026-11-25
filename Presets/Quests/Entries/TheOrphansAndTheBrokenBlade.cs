using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("The Orphans and the Broken Blade", [715u], "masterplayer.quest.the-orphans-and-the-broken-blade", "1.0.0", "MasterPlayer",
    "暗骑任务战：NPC持续分摊、指定效果清理、铁链优先选敌及独立读条范围。")]
public sealed class TheOrphansAndTheBrokenBlade() : DungeonScript(715);
