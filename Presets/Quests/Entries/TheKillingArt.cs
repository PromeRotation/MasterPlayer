using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("The Killing Art", [1021u], "masterplayer.quest.the-killing-art", "1.0.0", "MasterPlayer",
    "钐镰客任务战：精确入口差集、前四半环FIFO、毒圈三路交接、小怪优先级及双视图。")]
public sealed class TheKillingArt() : DungeonScript(1021);
