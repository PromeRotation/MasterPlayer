using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Fates;

[MechanicScript("FATE：Formidable", [NativeFateCatalog.FORMIDABLE],
    "masterplayer.fate.formidable", "1.0.0", "XSZ", "原生七连环、图标分摊、爆炸物、击退投影及停动诊断。")]
public sealed class Formidable : DungeonScript
{
    public Formidable() : base(NativeFateCatalog.FORMIDABLE) { }
}
