using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("The Threat of Perplexity", [267], "masterplayer.quest.the-threat-of-perplexity", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：按源重载保留0x2A4优先级677，其余保留基础优先级。")]
public sealed class TheThreatOfPerplexity() : QuestTargetScript(267);
