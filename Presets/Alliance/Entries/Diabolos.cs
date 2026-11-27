using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("影之国·虚空迪亚波罗斯", [627], "masterplayer.alliance.diabolos",
    "1.0.0", "PromeRadar", note: "原生标准组件；只覆盖源实际激活行为")]
public sealed class Diabolos : BozjaScript
{
    public Diabolos() : base(627, "A35Diabolos") { }
}
