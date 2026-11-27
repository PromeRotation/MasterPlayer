using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("魔航船虚无方舟·艾奇德娜", [508], "masterplayer.alliance.echidna",
    "1.0.0", "PromeRadar", note: "原生标准组件与击退提示；不扩自动位移")]
public sealed class Echidna : BozjaScript
{
    public Echidna() : base(508, "A14Echidna") { }
}
