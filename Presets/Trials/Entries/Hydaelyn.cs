using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("海德林歼灭战", [995u], "masterplayer.trial.hydaelyn", "1.0.0", "XSZ",
    "武器切换、幻日环、五连回声、水晶出离、动态光波及坦克分摊。")]
public sealed class Hydaelyn() : DungeonScript(995);
