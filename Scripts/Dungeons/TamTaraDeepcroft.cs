using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("塔姆塔拉墓园", [1037u], "masterplayer.dungeon.tamtara", "1.0.0", "XSZ",
    "BMR 普通版：Void Soulcounter 三场地、Galvanth 与小恶魔提示；目标优先级等差异见迁移记录，待实机验收。")]
public sealed class TamTaraDeepcroft() : DungeonScript(1037);
