using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Our Unsung Heroes", [722u], "masterplayer.quest.our-unsung-heroes", "1.0.0", "MasterPlayer",
    "学者任务战：图标分摊分散、NPC队伍、独立图标时序及无敌目标选敌，保留范围攻击顺带命中。")]
public sealed class OurUnsungHeroes() : DungeonScript(722);
