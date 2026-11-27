using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("人偶军事基地·重型陆战装置", [917], "masterplayer.alliance.heavyartillery",
    "1.0.0", "PromeRadar", note: "原生分段场地、能量弹与时间窗激光")]
public sealed class PuppetsBunkerHeavyArtillery : BozjaScript
{
    public PuppetsBunkerHeavyArtillery() : base(917, "A20HeavyArtilleryUnit") { }
}
