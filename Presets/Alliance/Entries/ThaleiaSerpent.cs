using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("荣华神域塔利亚·海蛇", [1178], "masterplayer.alliance.serpent",
    "1.0.0", "PromeRadar", note: "原生杂兵机制、独立生命周期与回放规划")]
public sealed class ThaleiaSerpent : BozjaScript
{
    public ThaleiaSerpent() : base(1178, "A30Trash1") { }
}
