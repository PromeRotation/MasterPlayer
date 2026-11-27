using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("禁忌城邦玛哈·奥兹玛", [556], "masterplayer.alliance.ozma",
    "1.0.0", "PromeRadar", note: "原生三场地、击退提示和加速炸弹诊断")]
public sealed class Ozma : BozjaScript
{
    public Ozma() : base(556, "A24Ozma") { }
}
