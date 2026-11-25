using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.PalaceOfTheDead;

[MechanicScript("死者宫殿 140层 Boss", [601u], "masterplayer.palace.ah-puch", "1.0.0", "XSZ",
    "Ah Puch 原生读条、地板状态、小怪优先级和精确场地，仅接管 Boss。")]
public sealed class PalaceAhPuch() : DungeonScript(601);
