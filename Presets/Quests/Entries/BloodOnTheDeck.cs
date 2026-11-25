using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Blood on the Deck", [708u], "masterplayer.quest.blood-on-the-deck", "1.0.0", "MasterPlayer",
    "四种AOE、敌人方向标记及精确甲板场地。")]
public sealed class BloodOnTheDeck() : DungeonScript(708);
