using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Life Ephemeral, Path Eternal", [1023u], "masterplayer.quest.life-ephemeral-path-eternal", "1.0.0", "MasterPlayer",
    "贤者最终任务：连续震地、单人塔、NPC分摊分散、连线诱导及动态场地。")]
public sealed class LifeEphemeralPathEternal() : DungeonScript(1023);
