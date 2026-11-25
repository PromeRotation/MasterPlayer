using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("家人的温暖·首领短吻鳄", [1244u], "masterplayer.quest.warmth-of-the-family", "1.0.0", "MasterPlayer",
    "连线拦截、Critical Bite、凹多边形场地与独立终结怪引用。")]
public sealed class TheWarmthOfTheFamily() : DungeonScript(1244);
