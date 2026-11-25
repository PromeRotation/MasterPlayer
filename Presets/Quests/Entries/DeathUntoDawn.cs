using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("死斗至黎明·月神伊弗利特", [977u], "masterplayer.quest.death-unto-dawn", "1.0.0", "MasterPlayer",
    "Lunar Ifrit：前三冲锋、前二十字、炎柱优先级与全体伤害预测；保留其他阶段模块。")]
public sealed class DeathUntoDawn() : DungeonScript(977);
