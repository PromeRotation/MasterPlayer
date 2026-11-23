using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("恩欧歼灭战", [1361u], "masterplayer.trial.enuo", "1.0.0", "XSZ",
    "普通模式：追踪连圈、分摊分散、深度冻结诱导、换场及零式遮挡。")]
public sealed class Enuo() : DungeonScript(1361);
