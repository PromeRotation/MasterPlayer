using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("血战苍穹魔科学研究所", [1110u], "masterplayer.dungeon.research-facility", "1.0.0", "XSZ",
    note: "MP原生首领与道中：炮台连线、分摊与视线、冰火星融合、双星先后、连续减益及孔洞场地。")]
public sealed class AetherochemicalResearchFacility() : DungeonScript(1110);
