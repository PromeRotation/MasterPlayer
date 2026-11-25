using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.HallOfTheNovice;

[MechanicScript("新人训练：控制多个敌人的仇恨", [NoviceCatalog.TANK04],
    "masterplayer.novice.tank04", "1.0.0", "XSZ", "原生木桩禁攻；保留低权重站位和动作意图诊断，不新增自动施放。")]
public sealed class NoviceTank04 : DungeonScript
{
    public NoviceTank04() : base(NoviceCatalog.TANK04) { }
}
