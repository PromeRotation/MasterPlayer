using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.HallOfTheNovice;

[MechanicScript("新人训练：最终训练", [NoviceCatalog.BASIC_FINAL],
    "masterplayer.novice.basic-final", "1.0.0", "XSZ", "原生精确圆场及同类目标优先级。")]
public sealed class NoviceBasicFinal : DungeonScript
{
    public NoviceBasicFinal() : base(NoviceCatalog.BASIC_FINAL) { }
}
