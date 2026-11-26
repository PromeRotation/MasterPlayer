using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Fates;

[MechanicScript("FATE：灾厄的古塔尼亚", [818],
    "masterplayer.fate.archaeotania", "1.0.0", "XSZ", "原生读条、至少八人分摊、旋风障碍和海啸投影。")]
public sealed class Archaeotania : DungeonScript
{
    public Archaeotania() : base(818) { }
}
