using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("朱诺：第一巡行·坐镇巨像", [1248], "masterplayer.alliance.groundskeeper",
    "1.0.0", "PromeRadar", note: "原生杂兵机制、独立生命周期与回放规划")]
public sealed class JeunoGroundskeeper : BozjaScript
{
    public JeunoGroundskeeper() : base(1248, "A10Groundskeeper") { }
}
