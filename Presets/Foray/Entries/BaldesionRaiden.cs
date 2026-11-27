using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Foray;

[MechanicScript("巴尔德西昂兵武塔·莱丁", [827], "masterplayer.foray.baldesion.raiden",
    "1.0.0", "PromeRadar", note: "双阶段斩铁剑、连锁闪电、锁链与完整状态链")]
public sealed class BaldesionRaiden : BozjaScript
{
    public BaldesionRaiden() : base(827, "BA2Raiden") { }
}
