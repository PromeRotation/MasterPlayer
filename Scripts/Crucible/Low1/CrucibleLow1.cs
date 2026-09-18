using MasterPlayer.Scripting;
using MasterPlayer.Mechanics.Crucible;

namespace MasterPlayer.Scripts.Crucible;

[MechanicScript("斗兽奇弈·普通第一盘", [1339u], "masterplayer.crucible.low1", "2.0.0", "KanoNoUta", "本地 BMR 7.5.6.10 机制迁移，B01–B06")]
public sealed class CrucibleLow1Script() : CrucibleScript(1, 6);
