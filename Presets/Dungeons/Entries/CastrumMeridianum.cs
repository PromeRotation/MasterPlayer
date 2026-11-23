using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("神兵要塞帝国南方堡", [1043u], "masterplayer.dungeon.castrum-meridianum", "1.0.0", "XSZ",
    "三场首领、延迟全屏伤害、内外圈与莉维亚连续组合范围。")]
public sealed class CastrumMeridianum() : DungeonScript(1043);
