using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("人偶军事基地·轻型陆战装置", [917], "masterplayer.alliance.lightartillery",
    "1.0.0", "PromeRadar", note: "原生火圈障碍、点名边缘站位与自毁计数")]
public sealed class PuppetsBunkerLightArtillery : BozjaScript
{
    public PuppetsBunkerLightArtillery() : base(917, "A20LightArtilleryUnit") { }
}
