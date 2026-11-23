using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("邪教驻地无限城古堡", [167u], "masterplayer.dungeon.amdapor-keep", "1.0.0", "XSZ",
    "两阶段首领、恶魔墙窄带击退与禁区、石柱遮挡和瘟疫连线诱导。")]
public sealed class AmdaporKeep() : DungeonScript(167);
