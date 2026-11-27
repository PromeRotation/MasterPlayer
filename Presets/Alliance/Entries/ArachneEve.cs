using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("禁忌城邦玛哈·阿剌克涅", [556], "masterplayer.alliance.arachneeve",
    "1.0.0", "PromeRadar", note: "原生机制、蛛网场地与拉人提示")]
public sealed class ArachneEve : BozjaScript
{
    public ArachneEve() : base(556, "A21ArachneEve") { }
}
