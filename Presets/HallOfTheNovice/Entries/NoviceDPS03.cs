using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.HallOfTheNovice;

[MechanicScript("新人训练：配合防护职业打倒多个敌人", [NoviceCatalog.DPS03],
    "masterplayer.novice.dps03", "1.0.0", "XSZ", "原生场地、首个角斗士指示目标及误攻提示。")]
public sealed class NoviceDPS03 : DungeonScript
{
    public NoviceDPS03() : base(NoviceCatalog.DPS03) { }
}
