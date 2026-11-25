using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.TreasureHunt;

[MechanicScript("加加财富天坑·原生首领", [1209u], "masterplayer.treasure.cenote-ja-ja-gural", "1.2.0", "XSZ",
    "Bull Apollyon、Golden Molter及第1–4房：独立读条、隐藏显形、持续池、动态冲锋、房间生命周期与真实组合场地。")]
public sealed class CenoteJaJaGural() : DungeonScript(1209);
