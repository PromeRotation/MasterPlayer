using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("铜铃铜山", [1038u], "masterplayer.dungeon.copperbell", "1.0.0", "XSZ",
    "BMR 普通版：Kottos、Ichorous Ire、Gyges，含场地缺口；队伍伤害预测等差异见迁移记录，待实机验收。")]
public sealed class CopperbellMines() : DungeonScript(1038);
