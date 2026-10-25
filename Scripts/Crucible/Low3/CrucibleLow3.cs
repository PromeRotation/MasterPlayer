using MasterPlayer.Scripting;
using MasterPlayer.Mechanics.Crucible;

namespace MasterPlayer.Scripts.Crucible;

[MechanicScript("斗兽奇弈·普通第三盘", [1341u], "masterplayer.crucible.low3", "2.1.0", "KanoNoUta", "B14–B21：读条范围预测、自动躲避与机制提示")]
public sealed class CrucibleLow3Script() : CrucibleScript(14, 21);
