using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("禁忌城邦玛哈·卡洛菲斯提莉", [556], "masterplayer.alliance.calofisteri",
    "1.0.0", "PromeRadar", note: "原生标准组件与击退提示；不扩自动位移")]
public sealed class Calofisteri : BozjaScript
{
    public Calofisteri() : base(556, "A25Calofisteri") { }
}
