using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("光之巫女", [861u], "masterplayer.quest.the-oracle-of-light", "1.0.0", "MasterPlayer",
    "Ran'jit：双雷线、前八火圈及锁定方向撞墙击退投影。")]
public sealed class TheOracleOfLight() : DungeonScript(861);
