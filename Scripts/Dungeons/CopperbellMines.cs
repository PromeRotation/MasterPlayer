using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("铜铃铜山", [1038u], "masterplayer.dungeon.copperbell", "1.1.0", "XSZ",
    "Kottos、Ichorous Ire、Gyges：读条范围躲避、场地缺口与死刑提示。")]
public sealed class CopperbellMines() : DungeonScript(1038);
