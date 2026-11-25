using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.PalaceOfTheDead;

[MechanicScript("死者宫殿 150层 Boss", [602u], "masterplayer.palace.tisiphone", "1.0.0", "XSZ",
    "Tisiphone 原生范围、职业选敌、僵尸即时/未来危险区与预战提示，仅接管 Boss。")]
public sealed class PalaceTisiphone() : DungeonScript(602);
