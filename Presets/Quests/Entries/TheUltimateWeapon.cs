using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("超越幻想，究极神兵：拉哈布雷亚", [1052u], "masterplayer.quest.ultimate-weapon", "1.0.0", "XSZ",
    "原生拉哈布雷亚：场地缩小、独立幻影、火球预测、沥青海及击退站位。")]
public sealed class TheUltimateWeapon() : DungeonScript(1052);
