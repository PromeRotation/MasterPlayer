// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// 第 29 组（斗兽奇弈高段 第一盘 2026-09-11 21.10.10 实测 OID）：
//   奇子·树精[19661] 本体、奇子·蛞蝓[19662]×12、奇子·花苗(树苗)[19663]、奇子·凶蛛蝎[19664]、奇子·比洛克[19665]。
// 技能范围由 CrucibleTreantAOEs 处理；此组件只负责小怪优先级和蛞蝓时间轴提示。
//
// 48769 结束后约 1s 刷出蛞蝓；约 5s 后开始接近目标、约 5.8s 首次近战攻击只是早期样本提示。
// 封存 run1980/run1986 的同一实例在出生 12s 后仍反复使用 49682，否定旧的接触自爆/定时自毁说法。
// 49682 是无读条的单目标近战动作；追击、仇恨和实际首次接触时间尚未独立校准。
sealed class CrucibleGroveAdds(BossModule module) : BossComponent(module)
{
    private static readonly uint[] AddOIDs = [19661u, 19662u, 19663u, 19664u, 19665u];
    private const uint Slug = 19662u;
    private const uint FallingLeaves = 48769u;
    public const double SlugRushDelay = 5.0;
    public const double SlugContactDelay = 5.8;

    private readonly Dictionary<ulong, DateTime> _slugSpawn = [];
    private DateTime _leavesFinish;

    private IEnumerable<Actor> Living() => Module.Enemies(AddOIDs)
        .Where(a => !a.IsDeadOrDestroyed && a.IsTargetable);

    public override void OnCastStarted(Actor actor, ActorCastInfo spell)
    {
        if (spell.Action.ID == FallingLeaves)
            _leavesFinish = spell.FinishAt;
    }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == Slug)
            _slugSpawn[actor.InstanceID] = WorldState.CurrentTime;
    }

    public override void OnActorDeath(Actor actor) => _slugSpawn.Remove(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => _slugSpawn.Remove(actor.InstanceID);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var add in Living())
        {
            hints.SetPriority(add, add.OID switch
            {
                19663u => 8, // 花苗：飞石反复高伤
                19665u => 7, // 比洛克：每约 11s 给树精回 7297
                19662u => 6, // 蛞蝓：持续近战攻击
                19664u => 5, // 凶蛛蝎
                _ => 4,      // 树精本体
            });
        }
    }

    public override void AddGlobalHints(GlobalHints hints)
    {
        var now = WorldState.CurrentTime;
        if (_leavesFinish > now.AddSeconds(-2))
            hints.Add("落叶纷飞：约 1s 后刷出蛞蝓，出现约 5s 后开始接近目标；会持续攻击，尽快清理");
        var armed = 0;
        var contact = double.MaxValue;
        foreach (var (id, spawn) in _slugSpawn)
        {
            if (WorldState.Actors.Find(id) is not { IsDeadOrDestroyed: false })
                continue;
            ++armed;
            contact = Math.Min(contact, (spawn.AddSeconds(SlugContactDelay) - now).TotalSeconds);
        }
        if (armed == 0)
            return;
        hints.Add(contact > 0
            ? $"蛞蝓 ×{armed}：预计约 {contact:F1}s 后开始近战攻击（49682），拉开距离并清理"
            : $"蛞蝓 ×{armed}：会持续追击并近战攻击（49682），拉开距离并清理");
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Living().Any(a => a.OID != 19661u))
            hints.Add("优先击杀花苗、比洛克，再清蛞蝓和凶蛛蝎", false);
    }
}
