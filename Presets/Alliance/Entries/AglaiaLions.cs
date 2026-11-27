using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("灿烂神域阿格莱亚·双狮", [1054], "masterplayer.alliance.lions",
    "1.0.0", "PromeRadar", note: "原生双狮队列、原Actor生命周期与回放规划")]
public sealed class AglaiaLions : BozjaScript
{
    public AglaiaLions() : base(1054, "A10Lions") { }
}
