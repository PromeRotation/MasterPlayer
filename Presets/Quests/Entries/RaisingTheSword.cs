using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("为了更强", [706u], "masterplayer.quest.raising-the-sword", "1.0.0", "MasterPlayer",
    "骑士职业任务：风圈、锥形击退导航、墙体截断与落点投影。")]
public sealed class RaisingTheSword() : DungeonScript(706);
