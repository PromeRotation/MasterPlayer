using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("佐拉加歼灭战", [1200u], "masterplayer.trial.zoraal-ja", "1.0.0", "XSZ",
    "普通模式原生脚本：双阶段检查点、连续半场、缩场与外平台投影、连线飞剑及四连分摊。")]
public sealed class ZoraalJa() : DungeonScript(1200);
