using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("白虎镇魂战", [746u], "masterplayer.trial.byakko", "1.0.0", "XSZ",
    "天雷分散、扫腿、白帝直线及荒魂移动预测。")]
public sealed class Byakko() : DungeonScript(746);
