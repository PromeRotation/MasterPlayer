using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("伊弗利特讨伐战", [1045u], "masterplayer.trial.ifrit-normal", "1.0.0", "XSZ",
    "普通伊弗利特：地火、光辉炎柱、顺劈、火柱倒数与选敌。")]
public sealed class IfritNormal() : DungeonScript(1045);
