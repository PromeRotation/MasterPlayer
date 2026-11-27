using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("桑多利亚：第二巡行·克拉肯", [1304], "masterplayer.alliance.kraken",
    "1.0.0", "PromeRadar", note: "原生杂兵机制、独立生命周期与回放规划")]
public sealed class SanDoriaKraken : BozjaScript
{
    public SanDoriaKraken() : base(1304, "A20Kraken") { }
}
