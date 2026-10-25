using MasterPlayer.Scripting;
using MasterPlayer.Mechanics.Crucible;

namespace MasterPlayer.Scripts.Crucible;

[MechanicScript("斗兽奇弈·普通第一盘", [1339u], "masterplayer.crucible.low1", "2.1.0", "KanoNoUta", "B01–B06：读条范围预测、自动躲避与机制提示")]
public sealed class CrucibleLow1Script() : CrucibleScript(1, 6);
