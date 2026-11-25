using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("褪色的记忆", [932u], "masterplayer.quest.faded-memories", "1.0.0", "MasterPlayer",
    "Flame General Aldynn 与 Nidhogg 的独立场地、机制与生命周期。")]
public sealed class FadedMemories() : DungeonScript(932);
