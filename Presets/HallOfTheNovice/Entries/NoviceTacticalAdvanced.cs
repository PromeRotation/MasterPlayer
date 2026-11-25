using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.HallOfTheNovice;

[MechanicScript("新人训练：复合标记", [1129],
    "masterplayer.novice.tactical-advanced", "1.0.0", "XSZ", "原生机制训练共用实现。")]
public sealed class NoviceTacticalAdvanced : DungeonScript
{
    public NoviceTacticalAdvanced() : base(1129) { }
}
