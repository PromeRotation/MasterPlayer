using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("死者宫殿80层 古丹纳", [595], "masterplayer.palace.gudanna", "1.0.0", "PromeRadar",
    "原生Boss机制；不包含深宫道中流程自动化")]
public sealed class PalaceGudanna() : DungeonScript(595);
