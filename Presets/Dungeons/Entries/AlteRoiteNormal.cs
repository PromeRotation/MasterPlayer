using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Raids;

[MechanicScript("欧米茄时空狭缝 德尔塔幻境1", [691], "masterplayer.raid.alteroite.normal",
    "1.0.0", "PromeRadar", note: "原生火球、读条范围、图标分摊分散与击退投影")]
public sealed class AlteRoiteNormal() : DungeonScript(691);
