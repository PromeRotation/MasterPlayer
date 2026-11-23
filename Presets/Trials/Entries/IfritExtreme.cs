using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("伊弗利特歼殛战", [295u], "masterplayer.trial.ifrit-extreme", "1.0.0", "XSZ",
    "原生三轮火狱之楔、灼热咆哮三连、地火引导、锁链与坦克减伤。")]
public sealed class IfritExtreme() : DungeonScript(295)
{
    [UserSetting("个人机制分工")]
    public CoilAssignment Assignment
    {
        get => Settings.Get(nameof(Assignment), CoilAssignment.Unassigned);
        set => Settings.Set(nameof(Assignment), value);
    }

    protected override string MechanicAssignment => Assignment.ToString();
}
