using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("永远之暗歼灭战", [1295u], "masterplayer.trial.necron", "1.0.0", "XSZ",
    "普通模式：手掌连续判定、储存圆环、监牢单向传送、四人塔及Director换场。")]
public sealed class Necron() : DungeonScript(1295);
