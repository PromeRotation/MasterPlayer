using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Foray;

[MechanicScript("巴尔德西昂兵武塔·欧文", [827], "masterplayer.foray.baldesion.owain",
    "1.0.0", "PromeRadar", note: "元素幻枪、灵魂分摊、反向凝视与完整状态链")]
public sealed class BaldesionOwain : BozjaScript
{
    public BaldesionOwain() : base(827, "BA1Owain") { }
}
