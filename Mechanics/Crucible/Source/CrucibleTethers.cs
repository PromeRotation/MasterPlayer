// Ported from BossmodRebornCN f6914f3e8. See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// ActorTetherInfo is the source of truth for mechanic links. ARR op 0x0234
// changes the selected target; actual tethers use ActorControl category 35/36.
// Earlier target observations (not proof of a mechanic tether):
//   33.2s  奇子·博学林鸮[19638] -> 玩家
//  117.2s  博学林鸮的羽毛[19640] -> 玩家，紧接 11.7s 读条 48667 暴风，128.3s 沿这条线击退
class CrucibleTetherLine(BossModule module, string hint, params uint[] sourceOIDs) : BossComponent(module)
{
    protected readonly List<(Actor Source, Actor Target)> Links = [];

    public override void Update()
    {
        Links.Clear();
        foreach (var source in Module.Enemies(sourceOIDs))
        {
            if (source.IsDeadOrDestroyed || source.Tether.Target == default)
                continue;
            if (WorldState.Actors.Find(source.Tether.Target) is { IsDeadOrDestroyed: false } target)
                Links.Add((source, target));
        }
    }

    public bool Tethered(Actor actor) => Links.Any(link => link.Target == actor);

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Tethered(actor))
            hints.Add(hint, true);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var (source, target) in Links)
        {
            var distance = (target.Position - source.Position).Length();
            // This distance threshold only changes the line colour; it does not constrain AI movement.
            var color = target == pc ? (distance >= 20f ? Colors.SafeFromAOE : Colors.Danger) : Colors.Object;
            Arena.AddLine(source.Position, target.Position, color);
            Arena.Actor(target, color);
        }
    }
}

// 羽毛（19640）与本体（19638）：连上后紧接着就是 48667 暴风（11.7s 读条）。
sealed class CrucibleOwlTether(BossModule module) : CrucibleTetherLine(module, "拉线：与博学林鸮/羽毛连线中，注意随后的暴风击退！", 19640u, 19638u);

// 第 24 组（高段第一盘 20.45.04 实测）：157.9s 尸生花[19645]、174.7s 花苗[19646]×3、225.7s 女王鹰蜂[19647] 都会连玩家。
// 未确认这些连线有距离解除条件；这里只画线和变色，不添加 AI 移动约束。
sealed class CrucibleMorbolTether(BossModule module) : CrucibleTetherLine(module, "拉线：被尸生花/花苗/女王鹰蜂连线中！", 19645u, 19646u, 19647u);
