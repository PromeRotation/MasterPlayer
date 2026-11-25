using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.TreasureHunt;

[MechanicScript("厄尔庇斯育体宝殿·原生首领", [1123u], "masterplayer.treasure.shifting-gymnasion", "1.4.0", "XSZ",
    "独立读条、凝视、连续双锤/四锤和多向扇形、强制行进预测、毒区、同心序列、分散/八人分摊及诱导和水圈击退，双视图及奖励组生命周期。")]
public sealed class ShiftingGymnasion() : DungeonScript(1123);
