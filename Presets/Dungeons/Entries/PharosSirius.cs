using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("领航明灯天狼星灯塔", [160u], "masterplayer.dungeon.pharos-sirius", "1.0.0", "XSZ",
    "四场首领、成长水晶毒区、三层爆炸分散、鸟蛋禁攻、塞壬月环和击退预测。")]
public sealed class PharosSirius() : DungeonScript(160);
