using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("神龙歼灭战", [679u], "masterplayer.trial.shinryu", "1.0.0", "XSZ",
    "水圈元素切换、落冰与连续突刺、坦克分摊、断链、薄冰及事件驱动破格场地。")]
public sealed class Shinryu() : DungeonScript(679);
