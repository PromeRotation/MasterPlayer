using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.PalaceOfTheDead;

[MechanicScript("死者宫殿 40层 Boss", [564u], "masterplayer.palace.ixtab", "1.0.0", "XSZ",
    "Ixtab 原生读条、地板状态、小怪优先级和精确场地，仅接管 Boss。")]
public sealed class PalaceIxtab() : DungeonScript(564);
