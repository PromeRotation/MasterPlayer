using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("休养胜地布雷福洛克斯野营地", [1041u], "masterplayer.dungeon.brayflox", "1.0.0", "XSZ",
    "四场首领原始场地、顺劈、吐息、连续辅助体毒圈、束缚队友标记和小怪优先级。")]
public sealed class BrayfloxLongstop() : DungeonScript(1041);
