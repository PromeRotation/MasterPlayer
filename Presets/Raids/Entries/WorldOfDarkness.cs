using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Raids;

[MechanicScript("水晶塔 暗之世界", [151u], "masterplayer.alliance.darkness", "1.0.0", "XSZ",
    "原生四首领：死亡视线、元素吐息、冲锋、全屏预告及五连追踪粒子光束。")]
public sealed class WorldOfDarkness() : DungeonScript(151);
