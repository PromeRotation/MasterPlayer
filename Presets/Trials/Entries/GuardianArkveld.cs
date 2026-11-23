using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("护锁刃龙狩猎战", [1300u], "masterplayer.trial.arkveld", "1.0.0", "XSZ",
    "普通模式：链刃、同心圆、双类塔、追击连圈、脉冲与偏侧扫射。源WIP缺口见规格。")]
public sealed class GuardianArkveld() : DungeonScript(1300);
