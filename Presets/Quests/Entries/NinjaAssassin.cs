using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Ninja Assassin", [484], "masterplayer.quest.ninja-assassin", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：优先处理 0x11E4。")]
public sealed class NinjaAssassin() : QuestTargetScript(484);
