using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Raids;

[MechanicScript("水晶塔 古代人迷宫", [174u], "masterplayer.alliance.labyrinth", "1.0.0", "XSZ",
    "原生四首领：骨龙复活、独立辅助体范围、黄道陨石遮挡、漩涡及梅吉多火焰。")]
public sealed class LabyrinthOfTheAncients() : DungeonScript(174);
