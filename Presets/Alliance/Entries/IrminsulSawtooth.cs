using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("魔航船虚无方舟·天柱树", [508], "masterplayer.alliance.irminsulsawtooth",
    "1.0.0", "PromeRadar", note: "原生机制与圆环墙击退提示；不扩自动位移")]
public sealed class IrminsulSawtooth : BozjaScript
{
    public IrminsulSawtooth() : base(508, "A12IrminsulSawtooth") { }
}
