using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("魔列车歼灭战", [1307u], "masterplayer.trial.doomtrain", "1.0.0", "XSZ",
    "普通模式原生脚本：五节车厢、双层平台与单向传送、真实阶段推进、连续光束、吸入墙及点名诱导。")]
public sealed class Doomtrain() : DungeonScript(1307);
