using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Fates;

[MechanicScript("FATE：Mica the Magical Mu", [1192],
    "masterplayer.fate.mica", "1.0.0", "XSZ", "原生抽卡缓存、连续冲锋、分批圆环与动态死刑。")]
public sealed class MicaTheMagicalMu : DungeonScript
{
    public MicaTheMagicalMu() : base(1192) { }
}
