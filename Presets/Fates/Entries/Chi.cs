using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Fates;

[MechanicScript("FATE：Chi", [NativeFateCatalog.CHI],
    "masterplayer.fate.chi", "1.0.0", "XSZ", "原生双段组合技、轰炸队列、动态死刑及伤害预测。")]
public sealed class Chi : DungeonScript
{
    public Chi() : base(NativeFateCatalog.CHI) { }
}
