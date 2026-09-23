// 目录战组件（形状/时机均来自 2026-08-06 三场回放实测）：
// 组件按机制分组：核爆全屏 / 封印武器（远离+靠近+AI 引导）/ 元素球（只绘制）/ 元素整合 rect /
// 圣枪冲击波 / 二连召唤连招斩击 / 全知烈火分散 / 预言（陨石+天崩地裂）。
// 元素地板/球机制判定复杂（球无读条无伤害事件，仅 tether 连线 ~10s 后消失），暂只绘制不引导；
// ReplayValidatedCastAOEs 用于读条型 AoE（replay 加速去重）。
using System.Runtime.InteropServices;
using BossMod.Dawntrail.Foray.CriticalEngagement;
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN4Index;

// 核爆（全屏 AoE）：本体 48415 读条 4.7s；连续咏唱（48407）后本体 48416 二段核爆（no cast 事件，双核爆）
sealed class FlareCasts(BossModule module) : Components.RaidwideCasts(module, [(uint)AID.Flare, (uint)AID.Flare2], "核爆：全屏伤害");

// 封印武器·远离：本体 48384 读条 + Helper 48385 爱之歌中心 R15 圈（7.0s），玩家需离开中心 15y 之外
sealed class SealedImplementsAway(BossModule module) : Components.SimpleAOEs(module, (uint)AID.RomeosBallad, 15f)
{
    // 预言时序门控（2026-08-17）：预言预警激活且本机制生效晚于预言结算+1.5s → 压制 AI 禁区（AI 先躲预言）；
    // 机制早于/接近预言结算（爱之歌差 0.7s）→ 不压制，先躲机制。雷达（ActiveAOEs）不受影响。
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Module.FindComponent<ProphecyMeteors>() is { } prophecy
            && ProphecyMeteors.EarliestAOEActivation(ActiveAOEs(slot, actor)) is { } act
            && prophecy.ShouldSuppress(act))
        {
            return;
        }
        base.AddAIHints(slot, actor, assignment, hints);
    }
}

// 封印武器·靠近：本体 48386 读条 + Helper 48387 盯准 R11 圈（7.1s）@ 场边 R20.5
// （常规 3 个三角位 / 元素阶段后 6 个六方位），圈内危险，玩家需靠近中心。
// AI：圈禁区（基类自动）+ Goal 引导站中心 R9.5 内（回放实测玩家均站中心附近）
sealed class SealedImplementsNear(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Aim, 11f)
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);

        // 预言时序门控（2026-08-17）：预言预警激活且本机制生效晚于预言结算+1.5s（盯准差 2.4s）→ 压制中心 goal，
        // 不把 AI 拉进 boss 脚下（预言后进圈）；圈禁区（base）照常——场边圈该躲还是躲。
        if (Module.FindComponent<ProphecyMeteors>() is { } prophecy
            && ProphecyMeteors.EarliestAOEActivation(ActiveAOEs(slot, actor)) is { } act
            && prophecy.ShouldSuppress(act))
        {
            return;
        }

        if (ActiveAOEs(slot, actor).Length > 0)
            hints.GoalZones.Add(AIHints.GoalSingleTarget(Module.Center, 9.5f, 2f));
    }
}

// 元素地板（字典）：1EC008 火 / 1EC009 冰 / 1EC00A 雷 地板实体（EventObj，位于场地中心），每轮元素控制（48394）
// 布置时重新生成并写入 rotation（回放 08-11：一轮 rot 火0/雷60/冰120、二轮 火120/雷0/冰60——每轮重新布置，动态读取）。
// 台子方向校准（回放 08-11 两轮验证）：BossMod 台子方向 = 地板游戏 rotation + 180°（对侧 +180°，与 spell.Rotation 同换算）
sealed class ElementFloor(BossModule module) : BossComponent(module)
{
    // 0 火 / 1 雷 / 2 冰 → BossMod 台子基方向（BossMod 角，0=南）；实时读取地板实体（每轮布置后自动反映新值）
    public Angle? GetDir(int prop)
    {
        var oid = prop switch
        {
            0 => (uint)OID.FireSector, // 火地板（merge 后上游命名）
            1 => (uint)OID.ThunderSector, // 雷地板
            2 => (uint)OID.IceSector, // 冰地板
            _ => default
        };
        foreach (var f in Module.Enemies(oid))
        {
            if (!f.IsDeadOrDestroyed)
            {
                return f.Rotation + 180f.Degrees();
            }
        }
        return null;
    }
}

// 元素球 cone（2026-08-11 机制查清后重写，替换原 ElementBalls 的 R15 猜测圈）：
// 4B64 冰 / 4B65 火 / 4B66 雷球在元素创造（48400）读条结束后生成（回放无 ACT+、仅有 COM+/TETH 事件，故用 Update 轮询检测），
// 球顺时针旋转至同属性台子（地板）时，场地中心 Helper 对该台子方向打 Fan60 R30 cone（对侧双扇，中心=场地中心 (0,-628)）。
// 延迟按 ACT 模板（墨汁塔普通.xml 3a/3b 触发器）：三球同现按三波 7.3/9.8/12.3s（间隔 2.5s，与 08-11 回放实测
// 雷 7.87/冰 10.37/火 12.90 的间隔完全吻合；单波场景 ACT 3a 用 9.7s，此处统一 3b 模板首波 7.3s，实测后校准）；
// 球到达台子（同类 Tether 363/364/365 断开）后 +0.63s cone 施放（回放实测恒定），OnUntethered 校准 activation。
// 08-12 修复（08-12 回放冰球无预警根因，用户确认）：
// - 每轮元素控制内多次元素创造（48400）独立计时：08-12 回放一轮内"单球→扩散圈→单球 / 扩散→扩散→三球"，
//   若只在 48394 重置，第二批球被当作第一轮第 2 波（_spawnTime 基准错 32.7s）→ 预判 cone 提前过期 → 无预警；
//   改为 48400 读条开始也重置（每次创造独立 _spawnTime/_wave 基准）。
// - OnUntethered 改按 source.OID 定属性：断开事件后 tether 已清空（ID=0，BossModule 传断开后状态），
//   原按 tether.ID 匹配 363/364/365 永远失败 → 校准（断开+0.63s）从不生效；改按球实体 OID（4B64→冰/4B65→火/4B66→雷）。
// 08-13 修复（08-13 回放雷球预警提前消失）：球旋转时长不定（08-13 04:30 轮雷球生成 ±20.5y、旋转 9.4s 才到平台），
// 预判（生成+7.3s）在 OnUntethered 校准（断开+0.63s）前已被 activation+0.5s 清理点删除 → 校准落空 → 预警提前消失；
// 改为 OnEventCast（48396/48397/48398 实际施放）按属性+activation 校验移除预判项（预警保留到 cone 打出，
// activation 校验防环机制同 AID cone 误删），ActiveAOEs 清理宽限 0.5s→3s 兜底（预判项存活到校准）。
// 08-13 波次分配改按角距排序（用户方案）：台子每轮随机布置 → 各球结算顺序随机（角距决定），
// 原按球检测顺序分配波次模板（7.3/9.8/12.3s）与结算顺序无必然关系 → 预判期紧迫度颠倒（该躲的球浅黄）；
// 改为检测时记录球到同属性台子双扇的顺时针角距（ClockwiseDiff，BossMod 角递减），批收集完成
// （三属性齐或 0.5s 无新球）后按角距小→大排序分配早→晚波次；08-13 回放验证：本轮角距 雷30°/冰90°/火150°
// 与实际结算 雷7.72s/冰10.20s/火12.12s 顺序一致；Tether 断开校准（+0.63s）仍精确修正 activation。
// 紧迫度：最先生效的波次 Colors.Danger，其余 Colors.AOE（参考 ArcaneBeacon 紧迫度分级）。
sealed class ElementOrbs(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone Cone = new(30f, 30f.Degrees());
    private readonly List<AOEInstance> _aoes = [];
    private readonly HashSet<ulong> _known = [];
    private readonly bool[] _added = new bool[3]; // 每属性已记录（对侧双扇只画一次）
    private readonly ulong[] _ballActor = new ulong[3]; // 每属性首球 InstanceID（Tether 校准匹配用）
    private readonly Angle[] _ballDir = new Angle[3]; // 每属性台子方向（GetDir，排序分配时用）
    private readonly float[] _ballAngle = new float[3]; // 每属性首球到台子双扇的顺时针角距（弧度，排序分配用）
    private int _pendingCount; // 本批已记录角距的属性数（批齐/超时后排序分配）
    private DateTime _lastSeen; // 最后检测到新球时刻（0.5s 无新球 → 批收集完成）
    private DateTime _spawnTime; // 本批波次延迟基准（分配时刻）
    private int _wave;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.OmniElements || spell.Action.ID == (uint)AID.ElementaryEvocation)
        {
            // 元素控制（48394）读条开始 / 元素创造（48400）读条开始：重置状态——
            // 08-12 修复：一轮元素控制内多次创造（单球→扩散圈→单球 / 扩散→扩散→三球），每次创造独立计时基准
            _aoes.Clear();
            _known.Clear();
            _added[0] = _added[1] = _added[2] = false;
            _ballActor[0] = _ballActor[1] = _ballActor[2] = 0;
            _spawnTime = default;
            _wave = 0;
        }
    }

    public override void Update()
    {
        var floor = Module.FindComponent<ElementFloor>();
        foreach (var oid in new[] { (uint)OID.BallOfFire, (uint)OID.BallOfLevin, (uint)OID.SwirlingOrb })
        {
            foreach (var b in Module.Enemies(oid))
            {
                if (b.IsDeadOrDestroyed || !_known.Add(b.InstanceID))
                {
                    continue;
                }

                var prop = b.OID == (uint)OID.BallOfFire ? 0 : b.OID == (uint)OID.BallOfLevin ? 1 : 2;
                if (_added[prop] || floor?.GetDir(prop) is not { } dir)
                {
                    continue;
                }
                _added[prop] = true;
                _ballActor[prop] = b.InstanceID;
                _ballDir[prop] = dir;
                // 角距 = 球顺时针（BossMod 角递减）转到台子双扇（台子方向或对侧）的最近行程
                // （2026-08-13 用户方案：台子每轮随机布置 → 结算顺序随机，原按检测顺序分配波次曾致紧迫度颠倒；
                // 08-13 回放验证：本轮角距 雷30°/冰90°/火150° 与实际结算顺序 雷7.72s/冰10.20s/火12.12s 一致）
                var toBall = Angle.FromDirection(b.Position - Module.Arena.Center);
                var d1 = ClockwiseDiff(toBall, dir);
                var d2 = ClockwiseDiff(toBall, dir + 180f.Degrees());
                _ballAngle[prop] = MathF.Min(d1, d2);
                _lastSeen = WorldState.CurrentTime;
                ++_pendingCount;
            }
        }

        // 批收集完成（三属性齐或 0.5s 无新球）→ 按角距排序分配波次
        if (_pendingCount > 0 && WorldState.CurrentTime - _lastSeen > TimeSpan.FromSeconds(0.5d))
        {
            AssignWaves();
        }
    }

    // 顺时针（BossMod 角递减）从 from 到 to 的行程（0..2π 正角）
    private static float ClockwiseDiff(Angle from, Angle to) => ((from - to).Normalized().Rad + MathF.Tau) % MathF.Tau;

    // 按角距从小到大排序属性 → 分配波次模板 7.3/9.8/12.3s（角距小 = 先到台子 = 先结算 = 早波次；
    // 单/双球批直接按序；Tether 断开校准仍精确修正 activation）
    private void AssignWaves()
    {
        var order = new List<int>(_pendingCount);
        for (var i = 0; i < 3; ++i)
        {
            if (_added[i])
            {
                order.Add(i);
            }
        }
        order.Sort((x, y) => _ballAngle[x].CompareTo(_ballAngle[y]));
        foreach (var prop in order)
        {
            if (_wave == 0)
            {
                _spawnTime = WorldState.CurrentTime;
            }

            ++_wave;
            var activation = _spawnTime.AddSeconds(7.3f + (_wave - 1) * 2.5f); // ACT 模板 3b（三波 7.3/9.8/12.3；单波 3a 为 9.7s，统一此模板，实测后校准）
            var dir = _ballDir[prop];
            _aoes.Add(new(Cone, Module.Arena.Center, dir, activation, actorID: _ballActor[prop]));
            _aoes.Add(new(Cone, Module.Arena.Center, dir + 180f.Degrees(), activation, actorID: _ballActor[prop]));
        }
        _pendingCount = 0;
    }

    // 球到达台子（同类 Tether 断开）→ +0.63s cone 施放（回放实测恒定），校准预判 activation。
    // 08-12 修复：断开事件后 tether 已清空（ID=0，BossModule.OnActorUntethered 传断开后状态），
    // 原按 tether.ID 匹配 363/364/365 永远失败 → 改按 source.OID 定属性（4B65 火 / 4B66 雷 / 4B64 冰）
    public override void OnUntethered(Actor source, in ActorTetherInfo tether)
    {
        var prop = source.OID switch
        {
            (uint)OID.BallOfFire => 0, // 火
            (uint)OID.BallOfLevin => 1, // 雷
            (uint)OID.SwirlingOrb => 2, // 冰
            _ => -1
        };
        if (prop < 0 || source.InstanceID != _ballActor[prop])
        {
            return;
        }

        var activation = WorldState.FutureTime(0.63d); // 断开 +0.63s
        var len = _aoes.Count;
        for (var i = 0; i < len; ++i)
        {
            if (_aoes[i].ActorID == source.InstanceID)
            {
                _aoes[i] = _aoes[i] with { Activation = activation };
            }
        }
    }

    // 实际 cone 施放（48396 炽炎火 / 48397 冰澈冰 / 48398 霹雷雷）→ 移除对应属性预判项（2026-08-13 修复：
    // 原 activation+0.5s 硬清理在球旋转慢时提前删预警——08-13 04:30 轮雷球生成于 ±20.5y 处、旋转 9.4s 才到平台，
    // Tether 断开（31.48）晚于预判（29.37）+0.5s 清理点（29.87）→ 校准前项已删 → 校准落空 → 预警提前消失；
    // 改为按实际施放事件移除，预警保留到 cone 打出；activation 校验防环机制同 AID cone 误删球预判项）
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        var prop = spell.Action.ID switch
        {
            (uint)AID.FireIV => 0, // 48396 炽炎（火）
            (uint)AID.BlizzardIV => 2, // 48397 冰澈（冰）
            (uint)AID.ThunderIV => 1, // 48398 霹雷（雷）
            _ => -1
        };
        if (prop < 0)
        {
            return;
        }

        var now = WorldState.CurrentTime;
        _aoes.RemoveAll(a => a.ActorID == _ballActor[prop] && a.Activation <= now.AddSeconds(1d)); // 仅移除即将施放的项（校准后 activation=断开+0.63 ≈ 施放时刻）
        _added[prop] = false; // 允许下一轮再添加（_known 已去重，不会重复）
    }

    // 紧迫度：最先生效的波次深黄（Danger+risky），其余浅黄（AOE、risky=false）——参考 ArcaneBeacon
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var time = WorldState.CurrentTime;
        // 结算后清除宽限 3s（2026-08-13 修复：0.5s 宽限在球旋转慢时提前删预判项——预判项需存活到
        // OnUntethered 校准（断开+0.63s）与 OnEventCast 移除；3s 覆盖校准偏差，正常由 OnEventCast 精确移除）
        _aoes.RemoveAll(a => a.Activation.AddSeconds(3d) < time);

        var soon = DateTime.MaxValue;
        var len = _aoes.Count;
        for (var i = 0; i < len; ++i)
        {
            if (_aoes[i].Activation < soon)
            {
                soon = _aoes[i].Activation;
            }
        }

        for (var i = 0; i < len; ++i)
        {
            var a = _aoes[i];
            var urgent = soon != DateTime.MaxValue && a.Activation <= soon.AddSeconds(0.5f);
            _aoes[i] = urgent ? a with { Color = Colors.Danger, Risky = true } : a with { Color = Colors.AOE, Risky = false };
        }
        return CollectionsMarshal.AsSpan(_aoes);
    }
}

// 扩散环（元素展开）：环实体 1EC00B 火 / 1EC00C 冰 / 1EC00D 雷（EventObj，出现于场地中心 (0,-628)）依次出现，
// 圆环扩大至对应属性平台中心时 boss 对该平台打 Fan60 R30 cone（origin=场地中心，方向=同属性台子=ElementFloor 字典）。
// 环 → cone 延迟 6.7s 恒定（08-11 回放两轮五组 6.65~6.75s 实测；ACT 模板 6.5s 参考）；环间隔 慢 4.0s / 快 2.0s
// （连续咏唱后），施放顺序 = 环出现顺序。紧迫度按 activation 分级（最先生效波次 Danger，其余 AOE）。
sealed class ElementRings(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone Cone = new(30f, 30f.Degrees());
    private readonly List<AOEInstance> _aoes = [];
    private readonly HashSet<ulong> _known = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.OmniElements) // 元素控制读条开始：新一轮布置，重置状态
        {
            _aoes.Clear();
            _known.Clear();
        }
    }

    public override void OnActorCreated(Actor actor)
    {
        var prop = actor.OID switch
        {
            (uint)OID.FireRing => 0, // 火环（merge 后上游命名）
            (uint)OID.ThunderRing => 1, // 雷环
            (uint)OID.IceRing => 2, // 冰环
            _ => -1
        };
        if (prop < 0 || !_known.Add(actor.InstanceID))
        {
            return;
        }

        var dir = Module.FindComponent<ElementFloor>()?.GetDir(prop);
        if (dir == null)
        {
            return;
        }

        var activation = WorldState.FutureTime(6.7d); // 环出现 → cone 6.7s（回放实测；ACT 模板 6.5s）
        _aoes.Add(new(Cone, Module.Arena.Center, dir.Value, activation, actorID: actor.InstanceID));
        _aoes.Add(new(Cone, Module.Arena.Center, dir.Value + 180f.Degrees(), activation, actorID: actor.InstanceID));
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var time = WorldState.CurrentTime;
        _aoes.RemoveAll(a => a.Activation.AddSeconds(0.5d) < time); // 爆炸后清除

        var soon = DateTime.MaxValue;
        var len = _aoes.Count;
        for (var i = 0; i < len; ++i)
        {
            if (_aoes[i].Activation < soon)
            {
                soon = _aoes[i].Activation;
            }
        }

        for (var i = 0; i < len; ++i)
        {
            var a = _aoes[i];
            var urgent = soon != DateTime.MaxValue && a.Activation <= soon.AddSeconds(0.5f);
            _aoes[i] = urgent ? a with { Color = Colors.Danger, Risky = true } : a with { Color = Colors.AOE, Risky = false };
        }
        return CollectionsMarshal.AsSpan(_aoes);
    }
}

// 击退会死区（2026-08-12 用户修复：三分身独立禁入区互相覆盖安全区 → 改为多来源联合判定）：
// 单来源危险 = 距该分身 **>10f**（GuideRadius，可能同时吃两个击退、AI 无法确认存活）**或** 9y 径向击退
// （AwayFromOrigin）后落点在战斗场地外（异形场地 + 内圈即死区挖洞，InBounds 判定）；
// 联合语义：p 安全 ⟺ 存在任一分身 i：距 i ≤10f 且被 i 击退后落点在场内（安全区 = 三个 10f 圆盘扣除各自
// 击退出界部分的并集）；p 禁入 ⟺ 对所有分身均危险。恰在分身位置（距离 ~0，击退方向无定义）保守安全。
// 假距离实现（先例 SDKnockbackInCircleAwayFromOrigin：Contains=0f 禁入/1f 允许，ShapeDistance.cs 注释许可非真距离）。
sealed class KnockbackDeathZone(WPos[] origins, float radius, float distance, Func<WPos, bool> inBounds) : ShapeDistance
{
    private readonly WPos[] _origins = origins;
    private readonly float _radius = radius;
    private readonly float _distance = distance;
    private readonly Func<WPos, bool> _inBounds = inBounds;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Contains(in WPos p)
    {
        var count = _origins.Length;
        for (var i = 0; i < count; ++i)
        {
            var to = p - _origins[i];
            var lenSq = to.LengthSq();
            if (lenSq <= _radius * _radius)
            {
                if (lenSq <= 1e-4f)
                {
                    return false; // 恰在分身位置：击退方向无定义，保守安全
                }
                var projected = p + _distance * to.Normalized();
                if (_inBounds(projected))
                {
                    return false; // 该分身视角安全 → p 安全（任一分身安全即安全）
                }
            }
            // 该分身视角危险（距其 >10f 或击退后出界）：继续检查其余分身
        }
        return true; // 所有分身视角均危险 → 禁入
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override float Distance(in WPos p) => Contains(p) ? 0f : 1f;
}

// 击退引导（FlyingDecreeGuide，2026-08-12 用户方案 v3：GoalZones 绿色引导改为 ForbiddenZone 禁入）：
// boss 读条飞翔指令（48403）→ 三分身（4B6F）落固定三角位（北 (0,-612.5)/南西 (-13.423,-635.75)/南东 (13.423,-635.75)，R15.5）
// → 分身 R15 圆击退 9y（AwayFromOrigin，用户实测）。
// 禁入区（ForbiddenZone，仅 AI 视觉；2026-08-12 用户修复：三分身独立禁入区互相覆盖安全圆盘 → 联合判定）：
// 单分身视角危险 = 距其 >10f（可能同时吃两个击退）或 9y 径向击退后落点在战斗场地外（异形场地 + 内圈即死区挖洞）；
// 联合语义：安全 = 任一分身视角安全（≤10f 且击退后落点在场内），对所有分身均危险才禁入。
// AI 避开会死区 = 等效站击退安全区。
// 弃用 GoalZones 绿色引导（2026-08-12 用户确认）：绿色引导受 NavigationDecision 施法门控（CastInfo==null 才栅格化
// GoalZones）影响，AI 玩家持续施法时完全消失（08-12 回放 10:03:53 起连续闪灼实测）；ForbiddenZone 栅格化在门控之前
// 无条件执行（NavigationDecision.Build），AI 面板始终显示。
// 窗口（与 FlyingDecreeKnockbacks 雷达箭头同窗口）：48403 飞翔指令读条开始激活 → 48405/48406 冲击波读条结束停用，
// 兜底 15s（48403 开始起算，防事件缺失；2026-08-12 用户确认 100s 过长）；
// OnCastFinished 加 EventHappened 防护（2026-08-12 修复：回放加速/重同步时 48405/48406 结束事件可能提前补发
// 致引导提前停用，与 HolyLanceShockwaves 同款防护）。
sealed class FlyingDecreeGuide(BossModule module) : BossComponent(module)
{
    private const float KnockbackDistance = 9f; // 击退距离（用户实测）
    private const float GuideRadius = 10f; // 击退引导有效半径（2026-08-12 用户规则：仅击退来源 10f 范围内有效，原 15f）
    // 分身固定三角位（2026-08-12 修复：回放 08-11 两轮位置一致；分身跳跃（48404）前尚在场地中心 (0,-628)，
    // 轮询实时位置会把引导区画在中心导致"三角位绿色引导不显示"，故用固定位）
    private static readonly WPos[] PhantomPositions =
    [
        new(0f, -612.5f), // 北
        new(-13.423f, -635.75f), // 南西
        new(13.423f, -635.75f), // 南东
    ];
    private bool _active;
    private DateTime _expire;

    // 跨相位常驻（2026-08-12 修复：封印武器读条（48384/48386）触发 p2→p3 相位切换，Exit 时未标记
    // KeepOnPhaseChange 的组件被 ClearComponents 销毁重建 → _active 窗口状态丢失 → 禁区消失；
    // 改为构造激活 + KeepOnPhaseChange（与 FTMN2 ThrownSwords 同模式，States 不再挂载）
    public override bool KeepOnPhaseChange => true;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.PropulsiveProphecy) // 飞翔指令读条开始：激活引导（圣枪 4B62 常驻非触发点，以 48403 为准）
        {
            _active = true;
            _expire = WorldState.FutureTime(15d); // 兜底窗口 15s（2026-08-12 用户确认；正常由冲击波读条结束停用，兜底仅防事件缺失）
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
        {
            return; // 回放加速/重同步的重复事件：不误停用（2026-08-12 修复，与 HolyLanceShockwaves 同款防护）
        }
        if (spell.Action.ID is (uint)AID.Shockwave1 or (uint)AID.Shockwave) // 冲击波 48405/48406 读条结束：飞翔阶段结束 → 停用引导
        {
            _active = false;
        }
    }

    public override void Update()
    {
        if (_active && WorldState.CurrentTime >= _expire)
        {
            _active = false;
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!_active)
        {
            return;
        }

        // 击退会死区禁入（单次联合判定；2026-08-12 用户修复：三分身独立禁入区会互相覆盖安全圆盘 → 改为联合 shape）：
        // 安全 = 任一分身视角安全（距该分身 ≤10f 且击退后落点在场内）；对所有分身均危险（>10f 或击退出界）才禁入。
        // 紧迫度 g 恒=10（2026-08-12 用户方案：activation = now+11s → g = 11−1s 缓冲 = 10 恒定中等紧迫——
        // 原 activation=default 恒 g=0 最高紧迫，AI 死守击退安全碎片区；封印武器读条 g 从 ~4.7 衰减至 <10 后
        // 紧迫度反超，把 AI 从被覆盖区域驱离到正确安全区）
        hints.AddForbiddenZone(new KnockbackDeathZone(PhantomPositions, GuideRadius, KnockbackDistance, Module.Arena.InBounds), WorldState.FutureTime(11d));
    }
}

// 飞翔指令击退箭头（2026-08-11 用户方案）：boss 读条飞翔指令（48403）→ 三分身（4B6F）落固定三角位
// （北 (0,-612.5)/南西 (-13.423,-635.75)/南东 (13.423,-635.75)，R15.5）→ 分身 R15 圆击退 9y（AwayFromOrigin，用户实测）。
// 雷达视图：仅对"距分身 15y 以内"的玩家显示击退箭头（来源=分身、距离 9f、AwayFromOrigin）；R15 圈本身由游戏 omen 显示、不画；
// AI 视觉由 FlyingDecreeGuide 绿色引导负责。多来源：玩家位于多个分身圈内时返回多个击退（基类按顺序依次应用）。
// 箭头绘制由 GenericKnockback 基类 DrawArenaForeground 自动完成（黄线 + 落点）。
sealed class FlyingDecreeKnockbacks(BossModule module) : Components.GenericKnockback(module)
{
    private const float KnockbackDistance = 9f; // 击退距离（用户实测）
    private const float CircleRadius = 15f; // 分身击退圆半径（圈内玩家被击退）
    // 分身固定三角位（2026-08-12 修复：与 FlyingDecreeGuide 同根因——分身跳跃（48404）前尚在场地中心，
    // 轮询实时位置会导致圈/箭头画在中心，改用固定位；回放 08-11 两轮位置一致）
    private static readonly WPos[] PhantomPositions =
    [
        new(0f, -612.5f), // 北
        new(-13.423f, -635.75f), // 南西
        new(13.423f, -635.75f), // 南东
    ];
    private readonly List<Knockback> _knockbacks = [with(4)];
    private bool _active;
    private DateTime _expire;

    // 跨相位常驻（2026-08-12 修复：与 FlyingDecreeGuide 同根因——封印武器读条触发 p2→p3 相位切换，
    // 非 KeepOnPhaseChange 组件被销毁重建 → _active 丢失 → 封印武器期间雷达箭头消失；构造激活 + KeepOnPhaseChange，
    // 与 FTMN2 ThrownSwords 同模式，States 不再挂载）
    public override bool KeepOnPhaseChange => true;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.PropulsiveProphecy) // 飞翔指令读条开始：激活（圣枪 4B62 常驻非触发点，以 48403 为准）
        {
            _active = true;
            _expire = WorldState.FutureTime(15d); // 兜底窗口 15s（2026-08-12 用户确认：飞翔指令这轮击退相关统一 15s，与 FlyingDecreeGuide 一致；正常由冲击波读条结束停用）
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.Shockwave1 or (uint)AID.Shockwave) // 冲击波 48405/48406 读条结束：飞翔阶段结束 → 停用
        {
            _active = false;
        }
    }

    public override void Update()
    {
        if (_active && WorldState.CurrentTime >= _expire)
        {
            _active = false;
        }
    }

    // 圈内玩家（距分身 ≤ 15y）显示击退箭头；圈外不显示；多圈内返回多个来源（基类按顺序依次应用）
    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        _knockbacks.Clear();
        if (!_active)
        {
            return CollectionsMarshal.AsSpan(_knockbacks);
        }

        var count = PhantomPositions.Length;
        for (var i = 0; i < count; ++i)
        {
            if ((actor.Position - PhantomPositions[i]).LengthSq() <= CircleRadius * CircleRadius)
            {
                _knockbacks.Add(new(PhantomPositions[i], KnockbackDistance, WorldState.CurrentTime, kind: Kind.AwayFromOrigin));
            }
        }
        return CollectionsMarshal.AsSpan(_knockbacks);
    }
}

// 交界引导（ElementWaitGuide，2026-08-11 用户方案）：元素阶段（球 cone / 环 cone）扇形 AOE 从场地中心向
// 六等分台子方向（0/60/120/180/240/300°）打 Fan60 R30——AI 提前站到两个相邻扇形交界处等待，
// 预警一出只需一步跨到安全侧（符合玩家操作习惯）。
// 交界点 = 六等分中间角方向（30/90/150/210/270/330°）@ Radius 9f（2026-08-12 用户计算确认：原 20y 超出 FTMN4
// 异形场地边界——20y 处已在场外（六边形边心距仅 13y，外接正方形不覆盖中间角方向），9f 在场地内；R2.5 判定圆）。
// 窗口（2026-08-12 用户精确窗口）：48394 元素控制读条完毕（OnCastFinished）激活 → 48401/48905 元素整合读条
// 开始（OnCastStarted）停用——整合读条期间即停用（避免与后续击退引导冲突，交界点权重 1.0 会压过击退引导 0.5）。
// 兜底窗口 100s（48394 读完起算；回放 08-11：球机制 +54~71s、环机制 +72~81s，100s 覆盖整轮元素阶段，防事件缺失）。
// 权重 1.0f（高于 CenterGoal 的 0.1——元素阶段优先交界点；停用后 AI 回到中心弱引导）。
// 得分 = 到 6 个交界点最近距离 ≤ 2.5f → 1.0f（取 min 避免多目标叠加糊权重）。
sealed class ElementWaitGuide(BossModule module) : BossComponent(module)
{
    private const float Radius = 9f; // 交界点距场地中心距离（2026-08-12 用户计算确认：20y 超出异形场地边界，9f 在场地内；可调）
    private const float AcceptRadius = 2.5f; // 交界点判定半径（可调）
    private const float Weight = 1.0f; // 引导权重（高于 CenterGoal 0.1，元素阶段优先交界点）
    private readonly WPos[] _spots = new WPos[6];
    private bool _active;
    private DateTime _expire;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.ElementaryChemistry or (uint)AID.UnknownWeaponskill2) // 元素整合读条开始：元素阶段收尾 → 停用交界引导
        {
            _active = false;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.OmniElements) // 元素控制读条完毕：新一轮布置完成 → 激活交界引导
        {
            _active = true;
            _expire = WorldState.FutureTime(100d); // 兜底窗口（回放：球/环机制在 48394 后 54~81s，100s 覆盖整轮；正常由元素整合读条开始停用）
            var center = Module.Arena.Center;
            for (var i = 0; i < 6; ++i)
            {
                _spots[i] = center + (30f + 60f * i).Degrees().ToDirection() * Radius; // 六等分中间角（台子方向之间）
            }
        }
    }

    public override void Update()
    {
        if (_active && WorldState.CurrentTime >= _expire)
        {
            _active = false; // 兜底窗口过期：AI 回到中心弱引导
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!_active)
        {
            return;
        }

        hints.GoalZones.Add(p =>
        {
            var best = float.MaxValue;
            for (var i = 0; i < 6; ++i)
            {
                var d = (p - _spots[i]).LengthSq();
                if (d < best)
                {
                    best = d;
                }
            }
            return best <= AcceptRadius * AcceptRadius ? Weight : 0f;
        });
    }
}

// 元素整合：Helper 48905 rect 15x15 ×3 @ 场地边缘三角（(0,-656)/(±24.249,-614)，R28 边缘），
// 与本体 48401 元素整合同步读条 6.0s（回放实测 2 次/轮，CST! 无目标=无人中招）
sealed class ElementaryChemistryRects(BossModule module) : ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID)
        => actionID == (uint)AID.UnknownWeaponskill2 ? new(new AOEShapeRect(15f, 7.5f)) : null;
}

// 圣枪冲击波击退箭头（2026-08-12 用户最终需求）：3 圣枪 4B62 固定三角位（(0,-612.5)/(±13.423,-635.75)）
// + 6 Helper 233C 同位置同步读条 48405（圣枪版）/ 48406（Helper 版）4.7s——以施法者位置为圆心 R15 圈内玩家
// 被从中心向外击退（AwayFromOrigin；回放实测：玩家聚集中心北侧时仅北枪覆盖，南两枪距离 >15 不覆盖）。
// 视觉方案（与 FlyingDecreeKnockbacks 同款模式）：仅雷达击退箭头——距任一冲击波中心 ≤15y 的玩家显示
// 以该中心为起点的箭头，圈外无箭头；不画 AOE 圈（游戏 omen 自带）；AI 视觉禁用：不继承 AoE 组件、
// GenericKnockback 基类无 AddAIHints 禁区（仅画箭头），48405/48406 均不生成 ForbiddenZone。
// 同位置多施法者去重（圣枪+Helper 成对同位置同批，只记一次中心）。
// 清理：冲击波读条结束（OnCastFinished 48405/48406）移除对应中心；元素控制（48394）读条开始重置；Update 兜底过期。
sealed class HolyLanceShockwaves(BossModule module) : Components.GenericKnockback(module)
{
    private const float KnockbackDistance = 10f; // 击退距离（上游 PropulsiveShockwave 实测 10f，可调）
    private const float CircleRadius = 15f; // 冲击波击退圆半径（圈内玩家被击退，回放实测）
    private readonly List<(WPos Origin, DateTime Activation)> _centers = [with(6)]; // 去重后的冲击波中心（施法者位置）
    private readonly List<Knockback> _knockbacks = [with(3)]; // 当前玩家击退列表（每中心至多一个）

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.OmniElements) // 元素控制读条开始：新一轮重置
        {
            _centers.Clear();
        }
        else if (spell.Action.ID is (uint)AID.Shockwave1 or (uint)AID.Shockwave)
        {
            if (spell.EventHappened)
            {
                return;
            }
            var activation = Module.CastFinishAt(spell);
            var origin = caster.Position;
            if (_centers.Any(c => (c.Origin - origin).LengthSq() < 1f && Math.Abs((c.Activation - activation).TotalSeconds) < 1d))
            {
                return; // 同位置同批施法者（圣枪+Helper 成对）：去重只记一次
            }
            _centers.Add((origin, activation));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.Shockwave1 or (uint)AID.Shockwave)
        {
            _centers.RemoveAll(c => c.Origin.AlmostEqual(caster.Position, 0.5f)); // 读条结束：移除对应中心（箭头消失）
        }
    }

    public override void Update()
    {
        _centers.RemoveAll(c => WorldState.CurrentTime > c.Activation.AddSeconds(1d)); // 兜底过期（正常由 OnCastFinished 移除）
        base.Update();
    }

    // 圈内玩家（距任一冲击波中心 ≤ 15y）显示以该中心为起点的击退箭头；圈外无箭头；
    // 位于多个中心圈内时返回多个击退（基类按顺序依次应用）；箭头绘制由基类 DrawArenaForeground 自动完成（黄线+落点）
    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        _knockbacks.Clear();
        var count = _centers.Count;
        for (var i = 0; i < count; ++i)
        {
            var c = _centers[i];
            if ((actor.Position - c.Origin).LengthSq() <= CircleRadius * CircleRadius)
            {
                _knockbacks.Add(new(c.Origin, KnockbackDistance, WorldState.CurrentTime, kind: Kind.AwayFromOrigin));
            }
        }
        return CollectionsMarshal.AsSpan(_knockbacks);
    }
}

// 二连召唤·封印武器连招斩击：本体 48390 读条同时 Helper 48391 镰鼬之风 ×3（方向 180/-60/60），
// 48390 结束后 Helper 48389 居合斩 ×3（方向 -120/120/0），均为 60° cone R30、6.0s 读条（回放实测）。
// 紧迫度分级（2026-08-12 用户需求）：3s 内即将生效的项深黄（Colors.Danger + risky），其余浅黄（Colors.AOE）——
// 基类 RiskyActivationWindow 窗口分级：距最早生效项 ≤3s 的项深黄（两批先后读条，先批读条剩余 ≤3s 起深黄，
// 后批保持浅黄；同批 3 项同刻生效同步转深黄）
sealed class SlashCombos(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCone Cone = new(30f, 30f.Degrees());

    protected override double RiskyActivationWindow => 3d; // 3s 内即将生效深黄（2026-08-12 用户需求）

    protected override AOEConfig? ConfigFor(uint actionID)
        => actionID is (uint)AID.WindSlash or (uint)AID.Iainuki ? new(Cone) : null;

    // AI 禁区时序门控（2026-08-16 用户要求 + 2026-08-17 时序化）：预言预警激活且封印武器（镰鼬/居合）禁区生效
    // 晚于预言结算+1.5s → 压制（不生成 AI 禁区，AI 先躲预言）；机制早于/接近预言结算 → 不压制（先躲机制）。
    // 仅门控 AI 禁区（AddAIHints）——雷达绘制路径（基类 ActiveAOEs）完全不动，人工视觉照常显示。
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Module.FindComponent<ProphecyMeteors>() is { } prophecy
            && ProphecyMeteors.EarliestAOEActivation(ActiveAOEs(slot, actor)) is { } act
            && prophecy.ShouldSuppress(act))
        {
            return;
        }
        base.AddAIHints(slot, actor, assignment, hints);
    }
}

// 全知烈火/劫火分散（2026-08-12 恢复 SpreadFromIcon 实现——merge 前 7.5.5.34 实测正常）：
// 点名图标 466（Icon_loc06sp_05ak1）一出现即画 R6 圈（48418 全知烈火读条期间即可见点名黄圈），
// 判定 = 48420 全知劫火事件（no cast，分 3 批），5.1s 判定延迟。
// a9104c308 重构曾换成 48418 读条驱动版（读完+0.2s 才画，读条期间无点名圈），用户实测缺失，已回滚。
sealed class AllConsumingFlames(BossModule module) : Components.SpreadFromIcon(module,
    (uint)IconID.Icon_loc06sp_05ak1, (uint)AID.AllConsumingFlames, 6f, 5.1d);

// 预言：本体 48412 读条后生成预言现象 4B63 ×3（初始 120° 分布 R9），瞬移至落点后 0.5s 读条：
// 48413 陨石 R10 ×2 @ 南侧 (±13.4,-635.8)、48414 天崩地裂 R5-15 donut ×1 @ 北侧 (0,-612.5)（回放实测）
// 预言（48412）：boss 读条 → 预言现象 4B63 ×3 出现（状态 2552 extra：0x44C=天崩月环/0x44D=陨石）并连线
// Index2 4B72（Tether 88，Index2 位置 = 落点台位）→ 4B63 移至落点 → ~10s 后 0.2s 快读条 48413 陨石/48414 天崩。
// 读条仅 0.2s 来不及反应，须依赖前置事件提前预警（ACT 对照：23: Tether 88 行 + 实体状态堆栈 0x9F8==0x44C
// 判月环 → t=9.4s；回放 08-11 实测 Tether→生效 ~10.2s，可校准）：
// - OnTethered（88，source=Index2 4B72）记录落点 = Index2 位置
// - OnStatusGain（2552，extra 0x44C 天崩 Donut 5-15 / 0x44D 陨石 Circle R10）记录类型
// - Update 补添前置项（Tether 与状态同毫秒到达、顺序不定；落点+类型齐备且未添加时添加）
// - 前置项：origin=Index2 落点、activation=前置时刻+9.4s、risky=true（AI 提前避开）、深黄最紧迫
//   （Colors.Danger，2026-08-13 用户确认：预言预警全程最紧迫、无临近分级）；
//   0.2s 读条到达时移除同落点前置项（读条精确项由基类接管，防双显示）；Activation+0.5s 过期清除；48394 重置。
sealed class ProphecyMeteors(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle StarfallShape = new(10f);
    private static readonly AOEShapeDonut CleansingShape = new(5f, 15f);
    private readonly List<AOEInstance> _preview = [with(6)]; // 前置预警项（读条项由基类管理）
    private readonly Dictionary<ulong, WPos> _targets = []; // 预言现象 InstanceID → 落点（Index2 位置）
    private readonly Dictionary<ulong, ushort> _types = []; // 预言现象 InstanceID → 状态 extra
    private readonly HashSet<ulong> _previewed = []; // 已添加前置项的预言现象
    private readonly List<AOEInstance> _displayed = [with(12)]; // 合并列表（读条项 + 前置项）

    // 预言前置预警是否激活（前置预警项非空）：供 SlashCombos 门控 AI 禁区使用（2026-08-16）
    public bool PreviewActive => _preview.Count > 0;

    // 预言前置预警最先生效（结算）时刻：_preview 空时 null（2026-08-17 时序门控用）
    public DateTime? NearestSettleTime
    {
        get
        {
            if (_preview.Count == 0)
                return null;
            var soon = _preview[0].Activation;
            for (var i = 1; i < _preview.Count; ++i)
                if (_preview[i].Activation < soon)
                    soon = _preview[i].Activation;
            return soon;
        }
    }

    // 时序门控（2026-08-17 用户方案 A）：预言预警激活且机制 activation 晚于预言结算 + 1.5s 安全余量 → 压制
    // （先躲预言；机制早于或接近预言结算则不压制，先躲机制——实测案例：爱之歌差 0.7s 不压制=当场躲；
    //  盯准差 2.4s 压制=预言后进圈）
    public bool ShouldSuppress(DateTime mechActivation)
    {
        var settle = NearestSettleTime;
        return settle != null && mechActivation > settle.Value.AddSeconds(SettleSafetyMargin);
    }
    private const float SettleSafetyMargin = 1.5f; // 安全余量：机制晚于预言结算 1.5s 以上才压制（2026-08-17）

    // 静态辅助：取 AOE 列表最早 activation（时序门控用，2026-08-17；命名避开基类 EarliestActivation 属性）
    public static DateTime? EarliestAOEActivation(ReadOnlySpan<AOEInstance> aoes)
    {
        if (aoes.Length == 0)
            return null;
        var soon = aoes[0].Activation;
        for (var i = 1; i < aoes.Length; ++i)
            if (aoes[i].Activation < soon)
                soon = aoes[i].Activation;
        return soon;
    }

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.Starfall => new(StarfallShape),
        (uint)AID.Cleansing => new(CleansingShape),
        _ => null
    };

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.OmniElements) // 元素控制读条开始：新一轮重置
        {
            _preview.Clear();
            _targets.Clear();
            _types.Clear();
            _previewed.Clear();
        }
        else if (spell.Action.ID is (uint)AID.Starfall or (uint)AID.Cleansing)
        {
            // 0.2s 快读条到达：移除同落点前置项（读条精确项由基类接管，防双显示）
            _preview.RemoveAll(a => a.Origin.AlmostEqual(caster.Position, 0.5f));
        }
        base.OnCastStarted(caster, spell);
    }

    // 预言现象（4B63）连线 Index2（4B72）：记录落点 = Index2 位置（台位，与生效落点一致）
    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (source.OID == (uint)OID.Index2 && tether.ID == (uint)TetherID.Tether_chn_m0361_mainte_1i)
        {
            _targets[tether.Target] = source.Position;
        }
    }

    // 预言现象状态 2552：extra 0x44C=天崩（月环）/ 0x44D=陨石
    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (status.ID == (uint)SID.UnknownStatus)
        {
            _types[actor.InstanceID] = status.Extra;
        }
    }

    public override void Update()
    {
        // Tether 与状态同毫秒到达、顺序不定：落点+类型齐备且未添加的预言现象补添前置项
        foreach (var (id, extra) in _types)
        {
            if (_previewed.Add(id) && _targets.TryGetValue(id, out var origin))
            {
                AOEShape shape = extra == 0x44C ? CleansingShape : StarfallShape; // 0x44C 天崩（月环）/ 0x44D 陨石
                // 紧迫度全程最紧迫（2026-08-13 用户确认：预言预警无临近分级、全程深黄；risky 默认 true AI 全程避开）
                _preview.Add(new(shape, origin, default, WorldState.FutureTime(9.4d), Colors.Danger, actorID: id, shapeDistance: shape.Distance(origin, default)));
            }
        }
        base.Update();
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var time = WorldState.CurrentTime;
        _preview.RemoveAll(a => a.Activation.AddSeconds(0.5d) < time); // 前置项结算后清除

        var baseSpan = base.ActiveAOEs(slot, actor);
        if (_preview.Count == 0)
        {
            return baseSpan;
        }

        _displayed.Clear();
        _displayed.AddRange(baseSpan);
        _displayed.AddRange(_preview);
        return CollectionsMarshal.AsSpan(_displayed);
    }
}

// 异形场地周期切换（2026-08-07 用户实测修正：元素控制读条完毕生成 / 元素整合读条完毕回收）：
// 初始 3 平台（南/东北/西北）→ 元素控制（48394）读条结束 → 6 平台；
// 元素整合（48401）读条期间额外 3 平台（东南/西南/北）红色禁入提示 → 读条结束 → 切回 3 平台并清提示。
// 回放验证（0557 场）：48394 读条 06:21:11→15 / 06:24:08→12；48401 读条 06:22:11→15 / 06:25:22→26；
// 爆弹怪两轮（06:22:45 / 06:25:56）在整合结束后约 30s 的机制堆叠阶段生成于初始 3 平台外缘中点，与场地回收一致。
sealed class ArenaShapes(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCustom ExtraShape = new(IndexArena.ExtraShapes); // 元素整合期间禁入的额外 3 正方形
    private readonly List<AOEInstance> _extra = [with(1)];

    public override bool KeepOnPhaseChange => true;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.ElementaryChemistry) // 元素整合读条开始：额外 3 平台禁入
        {
            _extra.Clear();
            _extra.Add(new(ExtraShape, IndexArena.Center, color: Colors.Danger, shapeDistance: ExtraShape.Distance(IndexArena.Center, default)));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        var id = spell.Action.ID;
        if (id == (uint)AID.OmniElements && Arena.Bounds != IndexArena.FullBounds) // 元素控制读条结束：展开全部 6 平台
        {
            Arena.Bounds = IndexArena.FullBounds;
            Arena.Center = IndexArena.FullBounds.Center;
        }
        else if (id == (uint)AID.ElementaryChemistry) // 元素整合读条结束：回收额外 3 平台，清禁入提示
        {
            _extra.Clear();
            if (Arena.Bounds != IndexArena.InitialBounds)
            {
                Arena.Bounds = IndexArena.InitialBounds;
                Arena.Center = IndexArena.InitialBounds.Center;
            }
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_extra);
}
