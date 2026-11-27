using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Foray;

[MechanicScript("巴尔德西昂兵武塔·亚特", [827], "masterplayer.foray.baldesion.art",
    "1.0.0", "PromeRadar", note: "亚特十组件、原生幻枪队列、停止移动诊断与追踪地雷")]
public sealed class BaldesionArt : BozjaScript
{
    public BaldesionArt() : base(827, "BA1Art") { }
}
