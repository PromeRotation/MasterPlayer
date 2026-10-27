// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// 第 29 组（斗兽奇弈高段 第一盘 2026-09-11 21.10.10 实测 OID）：
//   奇子·树精[19661] 本体、奇子·蛞蝓[19662]×12、奇子·花苗(树苗)[19663]、奇子·凶蛛蝎[19664]、奇子·比洛克[19665]。
// 技能范围由 CrucibleTreantAOEs 处理；此组件只负责小怪优先级和保持距离。
sealed class CrucibleGroveAdds(BossModule module) : BossComponent(module)
{
    private static readonly uint[] AddOIDs = [19661u, 19662u, 19663u, 19664u, 19665u];

    private IEnumerable<Actor> Living() => Module.Enemies(AddOIDs)
        .Where(a => !a.IsDeadOrDestroyed && a.IsTargetable);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var add in Living())
        {
            hints.SetPriority(add, add.OID switch
            {
                19662u => 8, // 蛞蝓：同场 12 只，最容易贴身
                19663u => 7, // 花苗/树苗
                19664u => 6, // 凶蛛蝎
                19665u => 5, // 比洛克
                _ => 4,      // 树精本体
            });
            if (hints.FindEnemy(add) is { } enemy)
                enemy.StayAtLongRange = true;
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Living().Any())
            hints.Add("优先击杀小怪（蛞蝓/花苗），别被贴身缠住！", false);
    }
}
