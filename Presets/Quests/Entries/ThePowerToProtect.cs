using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("The Power to Protect", [648u], "masterplayer.quest.the-power-to-protect", "1.0.0", "MasterPlayer",
    "守护之力：持续火区与极度警戒的停手、停读条及移动暂停。")]
public sealed class ThePowerToProtect() : DungeonScript(648);
