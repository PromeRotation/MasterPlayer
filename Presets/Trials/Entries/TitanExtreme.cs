using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("泰坦歼殛战", [296u], "masterplayer.trial.titan-extreme", "1.0.0", "XSZ",
    "原生七阶段、连续炸弹与地裂、花岗岩牢狱、地动与缩圈；按实际事件推进。")]
public sealed class TitanExtreme() : DungeonScript(296);
