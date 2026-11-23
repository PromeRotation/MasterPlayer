using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Trials;

[MechanicScript("利维亚桑歼灭战", [281u], "masterplayer.trial.whorleater-hard", "1.0.0", "XSZ",
    "独立结算的冲撞伤害与击退、旋转冲锋、毒区落点及职业选敌。")]
public sealed class WhorleaterHard() : DungeonScript(281);
