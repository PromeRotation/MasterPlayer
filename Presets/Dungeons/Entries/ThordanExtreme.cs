using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("圆桌骑士歼殛战", [448], "masterplayer.trial.thordan.extreme", "1.0.0", "PromeRadar",
    "极骑神原生机制：三阶段、五组骑士组合、连续冲击、联合凝视及击退站位。")]
public sealed class ThordanExtreme() : DungeonScript(448);
