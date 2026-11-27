using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("荣华神域塔利亚·神的使者第一组", [1178], "masterplayer.alliance.angelosfirst",
    "1.0.0", "PromeRadar", note: "原生杂兵机制、独立生命周期与回放规划")]
public sealed class ThaleiaAngelosFirst : BozjaScript
{
    public ThaleiaAngelosFirst() : base(1178, "A30Trash2Pack1") { }
}
