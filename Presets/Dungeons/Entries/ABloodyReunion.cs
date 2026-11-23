using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("雷古拉·范·休著斯追击战", [560u], "masterplayer.quest.a-bloody-reunion", "1.0.0", "XSZ",
    "原生读条、炮台爆炸倒计时与选敌、雷达及世界绘制。")]
public sealed class ABloodyReunion() : DungeonScript(560);
