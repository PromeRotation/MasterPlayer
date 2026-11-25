using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("四国联合军演", [533u], "masterplayer.quest.a-spectacle-for-the-ages", "1.0.0", "XSZ",
    "Aldynn 原生范围、士气地板障碍和 Tizona 选敌，仅接管已实现首领。")]
public sealed class ASpectacleForTheAges() : DungeonScript(533);
