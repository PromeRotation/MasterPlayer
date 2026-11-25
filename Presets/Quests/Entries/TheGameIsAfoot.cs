using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("那双眼中映出的是", [1177u], "masterplayer.quest.the-game-is-afoot", "1.0.0", "MasterPlayer",
    "Giant Colibri：前八落石、动态分摊分散、锁起点冲锋及队伍人数导航判定。")]
public sealed class TheGameIsAfoot() : DungeonScript(1177);
