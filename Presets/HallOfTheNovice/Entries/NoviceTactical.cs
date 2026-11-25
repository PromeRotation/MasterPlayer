using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.HallOfTheNovice;

[MechanicScript("新人训练：攻击标记", [1127],
    "masterplayer.novice.tactical", "1.0.0", "XSZ", "原生连续判定、站位、击退与视线；交互意图保留，未扩展自动对话。")]
public sealed class NoviceTactical : DungeonScript
{
    public NoviceTactical() : base(1127) { }
}
