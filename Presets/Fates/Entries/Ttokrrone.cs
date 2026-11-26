using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Fates;

[MechanicScript("FATE：得酷热涅", [1190],
    "masterplayer.fate.ttokrrone", "1.0.0", "XSZ", "原生沙暴、连续冲锋、沙球和七击/四击旋转序列。")]
public sealed class Ttokrrone : DungeonScript
{
    public Ttokrrone() : base(1190) { }
}
