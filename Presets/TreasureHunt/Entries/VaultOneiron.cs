using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.TreasureHunt;

[MechanicScript("巡梦金库·原生首领", [1279u], "masterplayer.treasure.vault-oneiron", "1.4.0", "XSZ",
    "独立读条、动态诱导、创建光线、连续地震、凝视双环、Old Bitter Eye拉近预告、Mobile Fortress连续轰炸、Yan钱袋与冲锋、Hexapod地雷/磁力/强制行进、奖励怪优先级和方形场地；仅接管已迁移首领。")]
public sealed class VaultOneiron() : DungeonScript(1279);
