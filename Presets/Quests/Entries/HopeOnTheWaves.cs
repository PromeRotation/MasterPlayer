using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("再渡红玉海", [757u], "masterplayer.quest.hope-on-the-waves", "1.0.0", "MasterPlayer",
    "完整区域阶段、六组范围攻击、自爆倒计时和小怪优先级；保留阿尔菲诺极限技拉位意图。")]
public sealed class HopeOnTheWaves() : DungeonScript(757);
