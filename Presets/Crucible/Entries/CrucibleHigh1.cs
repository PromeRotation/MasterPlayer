using MasterPlayer.Scripting;
using MasterPlayer.Mechanics.Crucible;

namespace MasterPlayer.Scripts.Crucible;

[MechanicScript("斗兽奇弈·高段第一盘", [1342u], "masterplayer.crucible.high1", "2.1.0", "KanoNoUta", "B22–B31：读条范围预测、自动躲避与机制提示")]
public sealed class CrucibleHigh1Script() : CrucibleScript(22, 31)
{
    [UserSetting("高1策略（普通练级 / 兽道III主动路线）")]
    public CrucibleHigh1Strategy Strategy
    {
        get => Settings.Get(nameof(Strategy), CrucibleHigh1Strategy.NormalLeveling);
        set => Settings.Set(nameof(Strategy), value);
    }

    protected override CrucibleHigh1Strategy High1Strategy => Strategy;
}
