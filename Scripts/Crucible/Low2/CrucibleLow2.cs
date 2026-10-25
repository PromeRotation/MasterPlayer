using MasterPlayer.Scripting;
using MasterPlayer.Mechanics.Crucible;

namespace MasterPlayer.Scripts.Crucible;

[MechanicScript("斗兽奇弈·普通第二盘", [1340u], "masterplayer.crucible.low2", "2.1.0", "KanoNoUta", "B07–B13：读条范围预测、自动躲避与机制提示")]
public sealed class CrucibleLow2Script() : CrucibleScript(7, 13);
