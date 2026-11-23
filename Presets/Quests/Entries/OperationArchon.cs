using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("贤者行军行动", [1049u], "masterplayer.quest.operation-archon", "1.0.0", "XSZ",
    "原生里塔提恩：独立范围、方向格挡禁攻及未格挡方向导航。")]
public sealed class OperationArchon() : DungeonScript(1049);
