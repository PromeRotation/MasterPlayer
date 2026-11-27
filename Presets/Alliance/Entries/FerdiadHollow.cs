using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("影之国·虚空弗迪亚", [627], "masterplayer.alliance.ferdiadhollow",
    "1.0.0", "PromeRadar", note: "原生标准组件；只覆盖源实际激活行为")]
public sealed class FerdiadHollow : BozjaScript
{
    public FerdiadHollow() : base(627, "A32FerdiadHollow") { }
}
