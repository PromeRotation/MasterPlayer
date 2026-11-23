using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("光之战士歼灭战", [922u], "masterplayer.trial.warrioroflight", "1.0.0", "XSZ",
    "检查点双主对象、火冰停手移动、连续塔与地火、光剑、连线和分摊。")]
public sealed class WarriorOfLight() : DungeonScript(922);
