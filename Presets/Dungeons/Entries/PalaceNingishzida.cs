using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.PalaceOfTheDead;

[MechanicScript("死者宫殿 30层 Boss", [563u], "masterplayer.palace.ningishzida", "1.0.0", "XSZ",
    "Ningishzida 原生四次诱导、火冰预测与常驻圈、顺劈及回中环形范围，仅接管 Boss。")]
public sealed class PalaceNingishzida() : DungeonScript(563);
