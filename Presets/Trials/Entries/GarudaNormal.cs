using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("迦楼罗讨伐战", [1047u], "masterplayer.trial.garuda-normal", "1.0.0", "XSZ",
    "普通迦楼罗：石柱视线、羽毛选敌、顺劈与风眼实际命中激活。")]
public sealed class GarudaNormal() : DungeonScript(1047);
