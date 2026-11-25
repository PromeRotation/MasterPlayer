using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("黑衣青年：MA-X", [141u], "masterplayer.quest.ma-x", "1.0.0", "MasterPlayer",
    "FATE1409机械士兵：三种实际AOE、战前场地、诺克提斯及机械兵方向标记；仅接管MA-X。")]
public sealed class MAXQuest() : DungeonScript(141);
