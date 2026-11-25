using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Quests;

[MechanicScript("蓝色铠甲的猛将", [459u], "masterplayer.quest.close-encounters-of-the-vith-kind", "1.0.0", "XSZ",
    "Regula 原生落点、常驻障碍及动态剑阵，保留销毁事件关闭整组的语义。")]
public sealed class CloseEncountersOfTheVIthKind() : DungeonScript(459);
