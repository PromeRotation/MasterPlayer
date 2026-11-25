using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("Fangs of the Viper", [1235u], "masterplayer.quest.fangs-of-the-viper", "1.0.0", "MasterPlayer",
    "正二十边形场地、玩家入战触发、Gowrow缓存生命周期与六种AOE。")]
public sealed class FangsOfTheViper() : DungeonScript(1235);
