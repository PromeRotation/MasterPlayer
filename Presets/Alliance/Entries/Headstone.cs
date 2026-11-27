using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("禁忌城邦玛哈·石面", [556], "masterplayer.alliance.headstone",
    "1.0.0", "PromeRadar", note: "原生标准组件；只覆盖源实际激活行为")]
public sealed class Headstone : BozjaScript
{
    public Headstone() : base(556, "A23Headstone") { }
}
