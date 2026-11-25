using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.PalaceOfTheDead;

[MechanicScript("死者宫殿 50层 Boss", [565u], "masterplayer.palace.edda", "1.0.0", "XSZ",
    "Edda Blackbosom 原生读条、PR凝视、伤害预测和精确场地，仅接管 Boss。")]
public sealed class PalaceEdda() : DungeonScript(565);
