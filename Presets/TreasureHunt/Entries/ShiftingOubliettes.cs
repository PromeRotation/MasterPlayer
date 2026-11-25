using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.TreasureHunt;

[MechanicScript("梦羽宝殿·原生首领", [924u], "masterplayer.treasure.shifting-oubliettes", "1.3.0", "XSZ",
    "独立读条、分身、固定及动态凝视、连续机制、击退吸引和水泡毒区，双视图及奖励组生命周期。")]
public sealed class ShiftingOubliettes() : DungeonScript(924);
