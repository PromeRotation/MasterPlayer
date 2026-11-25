using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("踏破风雪", [1010u], "masterplayer.quest.a-frosty-reception", "1.0.0", "MasterPlayer",
    "Vergilia：锁定预警动态跟随、实际落圈 FIFO、十字及直线、NPC 分散和全屏伤害。")]
public sealed class AFrostyReception() : DungeonScript(1010);
