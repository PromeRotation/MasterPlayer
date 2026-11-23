using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("名门府邸静语庄园", [1040u], "masterplayer.dungeon.haukke", "1.0.0", "XSZ",
    "三场首领原始场地、顺劈与队友避让、读条范围、Petrifying Eye 背对视线和小怪优先级。")]
public sealed class HaukkeManor() : DungeonScript(1040);
