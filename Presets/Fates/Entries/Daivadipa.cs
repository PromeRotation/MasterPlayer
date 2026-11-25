using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Fates;

[MechanicScript("FATE：德瓦达帕", [NativeFateCatalog.DAIVADIPA],
    "masterplayer.fate.daivadipa", "1.0.0", "XSZ", "原生双色球序列、强制行进模型及双视图投影。")]
public sealed class Daivadipa : DungeonScript
{
    public Daivadipa() : base(NativeFateCatalog.DAIVADIPA) { }
}
