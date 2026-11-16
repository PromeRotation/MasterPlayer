namespace MasterPlayer.Mechanics.Crucible;

// kano 11-13（低2 B11 牛魔兄弟）：两只血量差控制住，都压低后杀一只、马上杀另一只。
// 11-10 离线（candidates/low2-b11-20261110）：先杀弟时弟死后哥剩 25–28k，哥叠伤害提高，人物 50–62s 后被收掉（2164/2181/2182）。
// 规则：两只都活着时，打血量百分比高的那只（差距 >6% 才换，避免来回切）；两只都 ≤15% 时先打低的那只收掉，再打另一只。
sealed class CrucibleMinotaurBalance(BossModule module) : BossComponent(module)
{
    private const uint Elder = 19551, Younger = 19552;
    private const float SwitchGap = 0.06f, FinishBelow = 0.15f;
    private ulong _focus;

    private static float Pct(Actor a) => a.HPMP.MaxHP > 0 ? (float)a.HPMP.CurHP / a.HPMP.MaxHP : 1;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var alive = Module.Enemies(Elder).Concat(Module.Enemies(Younger))
            .Where(a => !a.IsDeadOrDestroyed && a.IsTargetable && a.HPMP.CurHP > 0).ToArray();
        if (alive.Length != 2)
        {
            _focus = 0;
            return;
        }
        var (hi, lo) = Pct(alive[0]) >= Pct(alive[1]) ? (alive[0], alive[1]) : (alive[1], alive[0]);
        Actor pick;
        if (Pct(hi) <= FinishBelow)
            pick = lo;
        else
        {
            var current = alive.FirstOrDefault(a => a.InstanceID == _focus);
            pick = current != null && Pct(hi) - Pct(current) <= SwitchGap ? current : hi;
        }
        _focus = pick.InstanceID;
        hints.SetPriority(pick, 5);
        hints.ForcedTarget = pick;
    }

    public override void AddGlobalHints(GlobalHints hints)
    {
        var alive = Module.Enemies(Elder).Concat(Module.Enemies(Younger)).Count(a => !a.IsDeadOrDestroyed && a.HPMP.CurHP > 0);
        if (alive == 2) hints.Add("牛魔兄弟：打血多的那只，两只都低了再一起收");
    }
}
