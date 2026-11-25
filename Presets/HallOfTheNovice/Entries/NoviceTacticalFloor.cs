using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.HallOfTheNovice;

[MechanicScript("新人训练：地面标记", [1128],
    "masterplayer.novice.tactical-floor", "1.0.0", "XSZ", "原生机制训练共用实现。")]
public sealed class NoviceTacticalFloor : DungeonScript
{
    public NoviceTacticalFloor() : base(1128) { }
}
