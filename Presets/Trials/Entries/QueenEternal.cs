using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("永恒女王歼灭战", [1202u], "masterplayer.trial.queen-eternal", "1.0.0", "XSZ",
    "普通模式原生脚本：连续左右斩、地图条带、变形场地、击退、视线与加速度停手。")]
public sealed class QueenEternal() : DungeonScript(1202);
