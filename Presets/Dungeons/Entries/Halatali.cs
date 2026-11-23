using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Dungeons;

[MechanicScript("魔兽领域日影地修炼所", [1245u], "masterplayer.dungeon.halatali", "1.0.0", "XSZ",
    "Firemane 连续扇形、Thunderclap Guivre 电水池与岸区、Tangata 三段震波及护盾禁攻。")]
public sealed class Halatali() : DungeonScript(1245);
