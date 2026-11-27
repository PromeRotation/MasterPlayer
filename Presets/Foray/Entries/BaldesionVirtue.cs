using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Foray;

[MechanicScript("巴尔德西昂兵武塔·绝对的美德", [827], "masterplayer.foray.baldesion.virtue",
    "1.0.0", "PromeRadar", note: "明暗属性、连线塔、风点名与完整状态链")]
public sealed class BaldesionVirtue : BozjaScript
{
    public BaldesionVirtue() : base(827, "BA3AbsoluteVirtue") { }
}
