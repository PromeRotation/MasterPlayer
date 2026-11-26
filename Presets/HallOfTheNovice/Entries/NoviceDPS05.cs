using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.HallOfTheNovice;

[MechanicScript("新人训练：打倒增援的敌人", [547],
    "masterplayer.novice.dps05", "1.0.0", "XSZ", "原生场地及全部合法增援优先级。")]
public sealed class NoviceDPS05 : DungeonScript
{
    public NoviceDPS05() : base(547) { }
}
