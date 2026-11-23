using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("莫古力贤王歼灭战", [1067u], "masterplayer.trial.thornmarch-hard", "1.0.0", "XSZ",
    "跨莫古力死亡的完整阶段、地图塔分工、连续转圈与同心圆、毒区、八人分摊及双坦死刑。")]
public sealed class ThornmarchHard() : DungeonScript(1067)
{
    [UserSetting("当前视角陨石塔分工（未分工者避塔）")]
    public ThornmarchTowerAssignment TowerAssignment
    {
        get => Settings.Get(nameof(TowerAssignment), ThornmarchTowerAssignment.Unassigned);
        set => Settings.Set(nameof(TowerAssignment), value);
    }

    protected override string MechanicAssignment => TowerAssignment.ToString();
}
