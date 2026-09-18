using MasterPlayer.Scripting;
using MasterPlayer.Mechanics.Crucible;

namespace MasterPlayer.Scripts.Crucible;

[MechanicScript("斗兽奇弈·普通第三盘", [1341u], "masterplayer.crucible.low3", "2.0.0", "KanoNoUta", "本地 BMR 7.5.6.10 机制迁移，B14–B21")]
public sealed class CrucibleLow3Script() : CrucibleScript(14, 21);
