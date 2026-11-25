using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.HeavenOnHigh;

[MechanicScript("天之御柱 30层 Boss", [772u], "masterplayer.hoh.30", "1.0.0", "XSZ",
    "蝶化身原生矩形、圆环与场地；保留源四个实际组件，不增加跳跃操作。")]
public sealed class HeavenOnHigh30() : DungeonScript(772);
