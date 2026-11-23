using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("真泰坦讨伐战", [293u], "masterplayer.trial.titan-hard", "1.0.0", "XSZ",
    "心核与石牢选敌、大地粉碎外圈及爆弹连续爆炸分组。")]
public sealed class TitanHard() : DungeonScript(293);
