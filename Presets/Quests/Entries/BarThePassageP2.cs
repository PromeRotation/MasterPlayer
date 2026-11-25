using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("决战！佐戈永结桥", [1246u], "masterplayer.quest.bar-the-passage-p2", "1.0.0", "MasterPlayer",
    "P1截线、P2护盾五连击及切场、P3地火连续判定/击退/缩场/分摊分散；接管三个首领阶段。")]
public sealed class BarThePassageP2() : DungeonScript(1246);
