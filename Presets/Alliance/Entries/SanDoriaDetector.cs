using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("桑多利亚：第二巡行·探测器", [1304], "masterplayer.alliance.detector",
    "1.0.0", "PromeRadar", note: "原生杂兵机制、独立生命周期与回放规划")]
public sealed class SanDoriaDetector : BozjaScript
{
    public SanDoriaDetector() : base(1304, "A20Detector") { }
}
