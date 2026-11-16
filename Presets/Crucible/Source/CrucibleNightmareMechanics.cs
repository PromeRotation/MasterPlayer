// See docs/reference/CrucibleAll.spec.md.
// 第 34 组（斗兽奇弈高段 第二盘）奇子·夜魔人。ACT run2187 / run2191（2026-11-08）。
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

static class CrucibleNightmare
{
    public const uint Boss = 19682u, Eye = 19683u, Bomb = 19684u, Light = 19685u;
    public static bool Alive(Actor a) => !a.IsDeadOrDestroyed && a.IsTargetable && a.HPMP.CurHP > 0;
    public static Actor[] Pets(WorldState ws, Actor player) => ws.Actors
        .Where(a => a != player && a.OwnerID == player.InstanceID && !a.IsDeadOrDestroyed && a.HPMP.CurHP > 0).ToArray();
}

// 49211 自爆：所有爆弹怪在 boss 现身时同时读 7.7s，读完 ~0.3s 生效。
// 每只炸弹只打一个目标，并非 AOE：run2187 两颗都打宠物 胡蜂（14.2/17.4m），
// 1.5m 外的玩家（15.1/18.9m）没挨；run2191 没有宠物在场，三颗全打玩家（≈3600/7621）。
// 两次都打的是离炸弹最近的单位，所以让宠物站在炸弹附近、玩家站得比宠物远。
[SkipLocalsInit]
sealed class CrucibleNightmareSelfDestruct(BossModule module) : BossComponent(module)
{
    private const uint SelfDestruct = 49211;
    private const float Margin = 1.5f;
    private readonly Dictionary<ulong, DateTime> _casts = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == SelfDestruct && !spell.EventHappened && Module.CastFinishAt(spell) > WorldState.CurrentTime)
            _casts[caster.InstanceID] = Module.CastFinishAt(spell, 0.3f);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == SelfDestruct && spell.Interrupted) _casts.Remove(caster.InstanceID);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == SelfDestruct) _casts.Remove(caster.InstanceID);
    }
    public override void OnActorDestroyed(Actor actor) => _casts.Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => _casts.Remove(actor.InstanceID);
    public override void Update()
    {
        foreach (var id in _casts.Where(c => c.Value.AddSeconds(1) < WorldState.CurrentTime).Select(c => c.Key).ToArray())
            _casts.Remove(id);
    }

    private IEnumerable<(Actor Bomb, DateTime Hit)> Casting()
    {
        foreach (var (id, hit) in _casts)
            if (WorldState.Actors.Find(id) is { } bomb && !bomb.IsDeadOrDestroyed)
                yield return (bomb, hit);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var bombs = Casting().ToArray();
        if (bombs.Length == 0) return;
        var pets = CrucibleNightmare.Pets(WorldState, actor);
        if (pets.Length == 0) return; // 没有宠物时谁站哪都是玩家吃，不白跑
        // 宠物去炸弹群中心抗；玩家对每颗炸弹都必须比最近的宠物远 Margin。
        var center = new WPos(bombs.Average(b => b.Bomb.Position.X), bombs.Average(b => b.Bomb.Position.Z));
        hints.PetMoveTo = center;
        foreach (var (bomb, hit) in bombs)
        {
            var pet = pets.Min(p => (p.Position - bomb.Position).Length());
            // 卡极限：只在生效前一刻要求站开，读条期间照常输出。
            hints.AddForbiddenZone(new SDCircle(bomb.Position, pet + Margin), hit, hit.AddSeconds(0.6));
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        var bombs = Casting().ToArray();
        if (bombs.Length == 0) return;
        var pets = CrucibleNightmare.Pets(WorldState, actor);
        if (pets.Length == 0)
            hints.Add("爆弹怪自爆：没有宠物抗，会全部打玩家！");
        else
            hints.Add("爆弹怪自爆：打离炸弹最近的单位，让宠物比你更靠近炸弹",
                bombs.Any(b => (actor.Position - b.Bomb.Position).Length() <= pets.Min(p => (p.Position - b.Bomb.Position).Length()) + Margin));
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var (bomb, _) in Casting())
            Arena.AddLine(pc.Position, bomb.Position, Colors.Danger);
    }
}

// 突击 49219（C043）/49220（C044）：2.7s，从当前位置冲到读条地点（实测 20~35m），冲锋线本身两场都没打到人。
// 冲完约 3s 在落点接 0.7s 短读条：49220 → 横扫 49224，49219 → 回转击 49222。
// 这两招只打 boss 当前仇恨目标（单体包）：run2187 横扫打 10.3m 外拉着仇恨的陷阱草，
// 4.9m 内正前方的玩家没挨；2191/2187 打到玩家时 boss 普攻也正在打玩家。走位躲不了，
// 只能让宠物拉住仇恨。v1 在落点封 R12/R10 圆（候选回放 2187 近战掉约 5 点），已去掉。
[SkipLocalsInit]
sealed class CrucibleNightmareCharge(BossModule module) : BossComponent(module)
{
    private const float LaneHalfWidth = 3;
    private record struct Charge(ulong Caster, WPos From, WPos To, DateTime Hit, string Followup);
    private readonly List<Charge> _charges = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime) return;
        if (spell.Action.ID is 49219 or 49220)
        {
            _charges.RemoveAll(c => c.Caster == caster.InstanceID);
            _charges.Add(new(caster.InstanceID, caster.Position, spell.LocXZ, Module.CastFinishAt(spell, 0.3f),
                spell.Action.ID == 49219 ? "回转击" : "横扫"));
        }
    }

    public override void Update()
    {
        _charges.RemoveAll(c => c.Hit.AddSeconds(5.5) < WorldState.CurrentTime);
        if (Module.PrimaryActor.IsDeadOrDestroyed) _charges.Clear();
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var c in _charges)
        {
            var dir = c.To - c.From;
            if (c.Hit > WorldState.CurrentTime && dir.LengthSq() > 1)
                hints.AddForbiddenZone(new AOEShapeRect(dir.Length(), LaneHalfWidth).Distance(c.From, Angle.FromDirection(dir)), c.Hit, c.Hit.AddSeconds(0.4));
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        foreach (var c in _charges)
            hints.Add($"突击→{c.Followup}：打仇恨目标，让宠物拉住仇恨", Module.PrimaryActor.TargetID == actor.InstanceID);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var c in _charges)
            if (c.Hit > WorldState.CurrentTime)
                Arena.AddLine(c.From, c.To, Colors.Danger);
    }
}

// 挥舞 49226（helper，4.7s，与 boss 49225 同时读）：环形。run2187 命中 4.2m 的宠物、0.5m 的玩家没挨；
// run2191 命中 4.4m 的玩家。内圈 <4.2，取 3（站 boss 脚下 ≤3m 仍在近战）；外圈未知，保守取 20。
// 2191 回放（v2）唯一剩下的命中就是它：MP 原来没有这招的形状。
[SkipLocalsInit]
sealed class CrucibleNightmareSwingRing(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeDonut Shape = new(3, 20);
    private readonly List<AOEInstance> _aoes = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 49226 && !spell.EventHappened)
            _aoes.Add(new(Shape, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID));
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 49226) _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 49226) _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
    }
}

// kano 11-11：现身就打本体；隐身阶段不管爆弹怪，先杀深瞳，再杀光元精。
// run2187：没清掉两只光元精，每 4.5s 一次放逐 + 深瞳普攻把没宠物的玩家磨死。
// kano 11-09：夜魔人要「引导」现身——光元精的范围技打到隐身的夜魔人，它才重新可选中。
// 实测是光散射 49212（C03C）：光元精血量掉到约 30~45% 时读 1.2s，以光元精自己为中心的圆，只打敌方单位
// （2239 打到 7.0m 外的爆弹怪，所以半径 ≥7m）。打到夜魔人后约 0.5s 夜魔人就可选中
// （2236 14:14:04.8 命中 3.8m 外的夜魔人 → 14:14:05.3 可选中；2239 第一、二次没打到，第三次 15:00:36.6 命中 0.9m 才现身，小怪阶段多拖 25s）。
// 隐身的夜魔人一直追着仇恨目标（宠物，吸引注意）用猛扑；2239 陆蟹是远程，站在光元精 9m 外，夜魔人跟着宠物，光散射打不到。
// 所以小怪阶段 boss 不可选中时：集中打血最少的光元精，宠物贴到这只光元精身上，把隐身的夜魔人带进光散射范围。
[SkipLocalsInit]
sealed class CrucibleNightmareAdds(BossModule module) : BossComponent(module)
{
    // 人物在近战距离内、宠物再往外一点，两者同在光元精朝向的射线上。
    private const float RevealPlayerRange = 2.5f, RevealPetRange = 5f;
    // run2344 第二遍：乌菊石收回后 27s 没有魔兽保护，两只光元精同时读光散射（1.2s），各 1467/1470 打在 15.6m 外的人物身上。
    // MP 读条开始才看到扇形（余量 1.47s），算不出路；当时现身引导还在把人往光元精正前方带。
    // 2300–2342 一百二十多次光散射里打中人物的只有 3 次，全部发生在人物没有「被保护」(2413) 的空档。
    // 没有保护时：不引导人物站到射线上；换宠空档（20s 内）里，血量 ≤50% 的光元精 20m 内提前当危险区，躲到它够不着的地方。
    // 超过 20s（三只宠都没了）不再躲，否则人物会一直绕场打不到东西。
    private const uint Protected = 2413;
    private const float ScatterRange = 20.5f, ScatterHpRatio = 0.5f;
    private const double UncoveredAvoidSeconds = 20;
    private DateTime _uncoveredSince;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var eyes = Module.Enemies(CrucibleNightmare.Eye).Where(CrucibleNightmare.Alive).ToArray();
        var lights = Module.Enemies(CrucibleNightmare.Light).Where(CrucibleNightmare.Alive).ToArray();
        var covered = actor.FindStatus(Protected) != null;
        if (covered) _uncoveredSince = default;
        else if (_uncoveredSince == default) _uncoveredSince = WorldState.CurrentTime;
        if (!covered && (WorldState.CurrentTime - _uncoveredSince).TotalSeconds <= UncoveredAvoidSeconds)
        {
            // ARR 回放（2300–2344 共 43 场）：无保护时 3 次命中里 2 次是刚刷出 1.1s、满血就读光散射的光元精；
            // 读条开始时 ACT 37 行把施法者 HP 报成 0，按 Alive() 读条期间反而不算活着。无保护窗口里所有光元精都躲，含读条中的。
            // 刚刷出的光元精先是不可选中（Alive 不认），1.1s 后就能读条，所以不看可选中。
            // 场地半径 20：三只光元精的 20.5m 圈常把全场盖满（2344 复测），那样寻路判「无安全格」整个停摆。
            // 依次退：全部 → 读条中 + ≤50% → 只躲读条中的；哪一层还有地方站就用哪一层。
            var all = Module.Enemies(CrucibleNightmare.Light).Where(l => !l.IsDeadOrDestroyed && (CrucibleNightmare.Alive(l) || l.CastInfo != null)).ToArray();
            bool Casting(Actor l) => l.CastInfo != null;
            bool Low(Actor l) => l.HPMP.MaxHP > 0 && (float)l.HPMP.CurHP / l.HPMP.MaxHP <= ScatterHpRatio;
            bool Escapable(Actor[] set)
            {
                for (var x = -18f; x <= 18; x += 2)
                    for (var z = -18f; z <= 18; z += 2)
                        if (x * x + z * z <= 18 * 18 && set.All(l => (l.Position - (Module.Center + new WDir(x, z))).Length() > ScatterRange + 1))
                            return true;
                return false;
            }
            foreach (var set in new[] { all, all.Where(l => Casting(l) || Low(l)).ToArray(), all.Where(Casting).ToArray() })
                if (set.Length > 0 && Escapable(set))
                {
                    foreach (var light in set)
                        hints.AddForbiddenZone(new SDCircle(light.Position, ScatterRange), WorldState.FutureTime(2));
                    break;
                }
        }
        foreach (var a in eyes) hints.SetPriority(a, 20);
        foreach (var a in lights) hints.SetPriority(a, 10);
        var finishBoss = Module.PrimaryActor;
        // kano 11-11：现身就打本体；隐身时才打深瞳、光元精。
        if (CrucibleNightmare.Alive(finishBoss))
        {
            foreach (var a in lights) hints.SetPriority(a, 0);
            foreach (var a in eyes) hints.SetPriority(a, 0);
            hints.SetPriority(finishBoss, 30);
            hints.ForcedTarget = finishBoss;
            return;
        }
        if (eyes.Length > 0)
            hints.ForcedTarget = eyes.FirstOrDefault(a => a.InstanceID == actor.TargetID)
                ?? eyes.MinBy(a => (a.Position - actor.Position).LengthSq());
        else if (lights.Length > 0)
        {
            // 打血最少的那只：它最先读光散射，宠物和隐身的夜魔人都跟着它。
            var bait = lights.MinBy(a => a.HPMP.CurHP)!;
            var boss = Module.PrimaryActor;
            hints.ForcedTarget = bait;
            if (CrucibleNightmare.Alive(boss)) return;
            // 11-10 实测（2239~2299 共 88 次光散射）：光散射是光元精正前方的扇形（命中目标 3m 外偏角都在 ±30° 内，最远 20m），
            // 光元精朝着自己的目标。隐身的夜魔人落在猛扑目标身上，停约 4s。旧引导把宠物放在光元精脚下，朝向等于随机，
            // 每次光散射只有约 20% 打中（2299 放了 4 次、隐身 56s）。
            // 现在让人和宠都站在同一条射线上（光元精 → 它的目标）：不管猛扑落在人还是宠身上，都在扇形中线上。
            var pets = CrucibleNightmare.Pets(WorldState, actor);
            var facing = WorldState.Actors.Find(bait.TargetID) is { } faced && (faced == actor || pets.Contains(faced))
                ? faced : pets.FirstOrDefault() ?? actor;
            var dir = facing.Position - bait.Position;
            if (dir.LengthSq() < 1)
                dir = actor.Position - bait.Position;
            if (dir.LengthSq() < 0.01f)
                dir = Module.Center - bait.Position;
            if (dir.LengthSq() < 0.01f) return;
            dir = dir.Normalized();
            if (hints.PetMoveTo == null && pets.Length > 0)
                hints.PetMoveTo = bait.Position + dir * RevealPetRange;
            if (covered)
                hints.GoalPoints.Add((bait.Position + dir * RevealPlayerRange, 1f, 50));
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (!CrucibleNightmare.Alive(Module.PrimaryActor) && Module.Enemies(CrucibleNightmare.Light).Any(CrucibleNightmare.Alive))
            hints.Add("夜魔人隐身：宠物贴着要打的光元精，光散射打到夜魔人它才现身", false);
    }
}
