using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("艳翼蛇鸟歼灭战", [1195u], "masterplayer.trial.valigarmanda", "1.0.0", "XSZ",
    "球体预判、雷电平台、双坦踩塔击退、冻结持续移动、直线分摊与三连灾变。")]
public sealed class Valigarmanda() : DungeonScript(1195);
