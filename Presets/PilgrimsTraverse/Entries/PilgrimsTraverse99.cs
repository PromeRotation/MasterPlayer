using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.PilgrimsTraverse;

[MechanicScript("朝圣之路 99层 / 卓异的悲寂歼灭战", [1290u, 1333u], "promeradar.pt99", "1.0.1", "XSZ", "两区域共用：光暗水、吸收换色、锁链静止、石柱、引导、双向地火与自动选敌；不适用于深想战")]
public sealed class PilgrimsTraverse99() : PilgrimsBossScript(99);
