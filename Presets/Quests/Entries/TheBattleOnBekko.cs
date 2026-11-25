using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("The Battle on Bekko", [711u], "masterplayer.quest.the-battle-on-bekko", "1.0.0", "MasterPlayer",
    "龟甲岛任务战：四段半环按实际结束推进、前八矩形、常驻障碍及伤害预测。")]
public sealed class TheBattleOnBekko() : DungeonScript(711);
