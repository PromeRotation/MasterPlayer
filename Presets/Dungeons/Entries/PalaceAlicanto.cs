using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.PalaceOfTheDead;

[MechanicScript("死者宫殿 110层 Boss", [598u], "masterplayer.palace.alicanto", "1.0.0", "XSZ",
    "Alicanto 原生读条、伤害预测和精确场地，仅接管 Boss。")]
public sealed class PalaceAlicanto() : DungeonScript(598);
