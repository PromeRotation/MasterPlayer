using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("迦楼罗歼殛战", [297u], "masterplayer.trial.garuda-extreme", "1.0.0", "XSZ",
    "原生石柱遮挡、姐妹分身、风圈、邪轮预测及刺羽护罩；按实际消失/重现推进阶段。")]
public sealed class GarudaExtreme() : DungeonScript(297)
{
    [UserSetting("个人机制分工")]
    public CoilAssignment Assignment
    {
        get => Settings.Get(nameof(Assignment), CoilAssignment.Unassigned);
        set => Settings.Set(nameof(Assignment), value);
    }

    protected override string MechanicAssignment => Assignment.ToString();
}
