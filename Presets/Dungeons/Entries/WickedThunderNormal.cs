using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts;

[MechanicScript("阿卡狄亚轻量级4（普通）", [1231], "masterplayer.raid.wickedthunder.normal",
    "1.0.0", "PromeRadar", note: "原生连续机制、分摊、诱导与场地变化")]
public sealed class WickedThunderNormal() : DungeonScript(1231);
