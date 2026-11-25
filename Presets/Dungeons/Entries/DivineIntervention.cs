using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("神意", [456u], "masterplayer.quest.divine-intervention", "1.0.0", "XSZ",
    "双 Boss 独立读条、锁定冲锋、链条优先级和精确场地，双方死亡或销毁后结束。")]
public sealed class DivineIntervention() : DungeonScript(456);
