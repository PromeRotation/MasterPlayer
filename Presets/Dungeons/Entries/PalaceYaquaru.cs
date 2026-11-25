using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("死者宫殿70层 Yaquaru", [594], "masterplayer.palace.yaquaru", "1.0.0", "PromeRadar",
    "原生机制：毒区三路交接、构造池引用、出池评分及四槽Heavy驱散意图。")]
public sealed class PalaceYaquaru() : DungeonScript(594);
