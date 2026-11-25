using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.HeavenOnHigh;

[MechanicScript("天之御柱 20层 Boss", [771u], "masterplayer.hoh.20", "1.0.0", "XSZ",
    "别蝶原生读条、PR凝视及精确场地，仅接管首领。")]
public sealed class HeavenOnHigh20() : DungeonScript(771);
