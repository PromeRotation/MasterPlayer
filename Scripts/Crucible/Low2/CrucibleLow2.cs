using MasterPlayer.Scripting;
using MasterPlayer.Mechanics.Crucible;

namespace MasterPlayer.Scripts.Crucible;

[MechanicScript("斗兽奇弈·普通第二盘", [1340u], "masterplayer.crucible.low2", "2.0.0", "KanoNoUta", "本地 BMR 7.5.6.10 机制迁移，B07–B13")]
public sealed class CrucibleLow2Script() : CrucibleScript(7, 13);
