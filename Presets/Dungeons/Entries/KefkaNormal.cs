using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("欧米茄时空狭缝 西格玛幻境4", [751], "masterplayer.raid.kefka.normal", "1.0.0", "PromeRadar",
    "凯夫卡普通战原生机制：真假AOE、八人分摊、反凝视、连续击退与真实判定。")]
public sealed class KefkaNormal() : DungeonScript(751);
