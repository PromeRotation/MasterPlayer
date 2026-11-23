using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("真伊弗利特讨伐战", [292u], "masterplayer.trial.ifrit-hard", "1.0.0", "XSZ",
    "火柱倒数、顺劈、地火、光辉炎柱及时间轴驱动的分身冲锋。")]
public sealed class IfritHard() : DungeonScript(292);
