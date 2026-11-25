using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.PalaceOfTheDead;

[MechanicScript("死者宫殿 130层 Boss", [600u], "masterplayer.palace.alfard", "1.0.0", "XSZ",
    "Alfard 原生四次诱导、火冰预测与常驻圈、顺劈及回中环形范围，仅接管 Boss。")]
public sealed class PalaceAlfard() : DungeonScript(600);
