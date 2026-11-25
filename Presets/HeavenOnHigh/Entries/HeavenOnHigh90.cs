using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.HeavenOnHigh;

[MechanicScript("天之御柱 90层 Boss", [775u], "masterplayer.hoh.90", "1.0.0", "XSZ",
    "温罗原生读条、三击沙球逐轴匹配及精确场地，保留楼层机制。")]
public sealed class HeavenOnHigh90() : DungeonScript(775);
