using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("泰坦讨伐战", [1046u], "masterplayer.trial.titan-normal", "1.0.0", "XSZ",
    "普通泰坦：大地之重、地裂、跳跃、心核倒数与石牢选敌。")]
public sealed class TitanNormal() : DungeonScript(1046);
