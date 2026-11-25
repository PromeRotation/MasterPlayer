using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("The Voidgate Breathes Gloomy", [317], "masterplayer.quest.the-voidgate-breathes-gloomy", "1.0.0", "MasterPlayer",
    "MP 原生任务选敌：0x6B1 显式优先级0，其余保留基础优先级。")]
public sealed class TheVoidgateBreathesGloomy() : QuestTargetScript(317);
