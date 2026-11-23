using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts;

[MechanicScript("鏖战红莲阿拉米格", [1146], "masterplayer.dungeon.ala-mhigo", "1.0.0", "MasterPlayer",
    note: "十二次尾部激光、火池交接、返回身体、芝诺三连击、缩场、击退与连线诱导。")]
public sealed class AlaMhigo() : DungeonScript(1146);
