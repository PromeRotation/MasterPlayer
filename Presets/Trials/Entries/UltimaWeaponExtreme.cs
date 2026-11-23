using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("究极神兵假想作战", [348u], "masterplayer.trial.ultima-extreme", "1.0.0", "XSZ",
    "原生蛮神连招、风暴内环、追踪球、三轮魔科学爆弹和飞空艇坠落；分工用于球站位与减伤轮换。")]
public sealed class UltimaWeaponExtreme() : DungeonScript(348)
{
    [UserSetting("个人机制分工")]
    public CoilAssignment Assignment
    {
        get => Settings.Get(nameof(Assignment), CoilAssignment.Unassigned);
        set => Settings.Set(nameof(Assignment), value);
    }

    protected override string MechanicAssignment => Assignment.ToString();
}
