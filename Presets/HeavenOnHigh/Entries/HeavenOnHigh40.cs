using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.HeavenOnHigh;

[MechanicScript("天之御柱 40层 Boss", [782u], "masterplayer.hoh.40", "1.0.0", "XSZ",
    "怖军原生读条、分散、持续风圈与撞墙击退投影，仅接管首领。")]
public sealed class HeavenOnHigh40() : DungeonScript(782);
