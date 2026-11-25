using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Otis Oathbroken", [1213u], "masterplayer.quest.otis-oathbroken", "1.0.0", "MasterPlayer",
    "Otis：独立圆形组、六锥与前四冲锋/前二导航、NPC分散分摊及20边形场地。")]
public sealed class OtisOathbroken() : DungeonScript(1213);
