using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("灿烂神域阿格莱亚·拉尔戈神使", [1054], "masterplayer.alliance.rhalgr",
    "1.0.0", "PromeRadar", note: "原生双锥、同心圈、诱导坦克死刑与完整状态链")]
public sealed class AglaiaRhalgrEmissary : BozjaScript
{
    public AglaiaRhalgrEmissary() : base(1054, "A10RhalgrEmissary") { }
}
