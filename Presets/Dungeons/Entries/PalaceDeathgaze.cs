using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.PalaceOfTheDead;

[MechanicScript("死者宫殿 10层 Boss", [561u], "masterplayer.palace.deathgaze", "1.0.0", "XSZ",
    "Palace Deathgaze 原生读条、伤害预测和精确场地，仅接管 Boss。")]
public sealed class PalaceDeathgaze() : DungeonScript(561);
