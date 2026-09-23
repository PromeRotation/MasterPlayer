using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE213WebOfTerror;

public enum OID : uint
{
    Boss = 0x4DFA, // R6.5, BNpcName 14840, crescent arachne
    Daughter = 0x4DFB, // R2.4, BNpcName 14841, daughter of arachne
    BoundaryController = 0x4DFC, // R1.0, non-targetable arena controller
    Helper = 0x233C
}

public enum AID : uint
{
    LethalBoundary = 0xC4BD, // controller, repeated persistent 20-30y outer deathwall pulse
    ImplosionVisual = 0xC4BE, // boss->self, 5.0s cast, raidwide visual
    ImplosionHit = 0xC4BF, // helpers->players, no cast, raidwide damage
    Summon = 0xC4C0, // boss->self, 3.0s cast, summons daughters
    ArachnidWebStart = 0xC4C1, // boss->daughter, 3.0s cast, visual/link start
    ArachnidWebLink = 0xC4C2, // daughter->daughter, no cast, visual/link propagation
    ArachnidFunnel = 0xC4C3, // boss->location, 5.0s cast, charge width 20
    ArachnidFunnelContinue = 0xC4C4, // boss->location, no cast, subsequent charge width 20
    VenomEruption = 0xC4C7, // daughter->self, 12.0s cast, lethal raidwide if daughter survives
    ConformityBoss = 0xC4C8, // boss->self, 3.0s cast, range 50 45-degree cone
    ConformityDaughter = 0xC4C9, // daughter->self, 3.0s cast, range 50 45-degree cone
    BedrockUpliftVisual = 0xC4CA, // boss->self, 4.7s cast, visual
    BedrockUpliftCircle = 0xC4CB, // helpers->self, 5.0s cast, range 10 circle
    BedrockUpliftMiddle = 0xC4CC, // helpers->self, 7.0s cast, range 10-20 donut
    BedrockUpliftOuter = 0xC4CD, // helpers->self, 9.0s cast, range 20-30 donut
    DaughterAutoAttack = 0xC5CB, // daughter->player, no cast, single-target
    QueensOrders = 0xC5D7, // boss->self, 3.0s cast, orders daughter Conformity casts
    ArachnidFunnelAftershock = 0xC5F8, // helper->location, no cast, charge width 20
    AutoAttack = 0xC6A5 // boss->player, no cast, single-target
}

sealed class LethalBoundary(BossModule module) : Components.GenericAOEs(module)
{
    // The lethal band is the 20-30 ring (death points clustered at 20.9-28.1y under the old r20
    // arena). The AI donut keeps its 19.5 inner edge (grid rasterization margin) - the deathwall
    // itself is unchanged. The arena display radius is now 25 (combat actually extends to R25:
    // boss funnel / daughter landing points), so only the outer fence outline moves out to 25.
    private static readonly AOEShapeDonut Shape = new(19.5f, 30f);
    private readonly AOEInstance[] _aoe = [new(Shape, module.Arena.Center)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;

    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        // Draw the full 19.5-30 deathwall band (same range as the AI donut; the part beyond the r25
        // arena is clipped automatically) plus the r25 fence outline so the arena boundary reads clearly.
        Arena.ZoneDonut(Arena.Center, 19.5f, 30f, Colors.Danger);
        Arena.ZoneCircleOutlineUnclipped(Arena.Center, 25f, Colors.Danger, 3f);
    }
}

// The spider web: the boss casts C4C1 on a daughter, then the link propagates daughter to
// daughter (C4C2). Replay exposes it as real tether events (IDs 0x1A4 boss->daughter, 0x198
// daughter->daughter); draw the live lines so players can see the web structure while the
// daughters walk and before the funnel charge follows the web.
sealed class ArachnidWeb(BossModule module) : BossComponent(module)
{
    private readonly List<(Actor Source, Actor Target)> _links = [];
    private readonly HashSet<ulong> _seenSources = [];

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (tether.ID is not (0x1A4u or 0x198u))
            return;
        if (WorldState.Actors.Find(tether.Target) is not { } target)
            return;

        var sourceID = source.InstanceID;
        var targetID = tether.Target;
        _links.RemoveAll(l => l.Source.InstanceID == sourceID && l.Target.InstanceID == targetID
            || l.Source.InstanceID == targetID && l.Target.InstanceID == sourceID);
        _links.Add((source, target));
        _seenSources.Add(source.InstanceID);
        // 注意：突进顺序以 ArachnidFunnelPath 的 cast 事件链（C4C1/C4C2）为权威；
        // 此处 tether 仅用于画线，不维护有序链（tether 方向可能反转/瞬态，实测不可靠）
    }

    public override void OnUntethered(Actor source, in ActorTetherInfo tether)
    {
        var sourceID = source.InstanceID;
        var targetID = tether.Target;
        _links.RemoveAll(l => l.Source.InstanceID == sourceID && l.Target.InstanceID == targetID
            || l.Source.InstanceID == targetID && l.Target.InstanceID == sourceID);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        _links.RemoveAll(l => l.Source.InstanceID == actor.InstanceID || l.Target.InstanceID == actor.InstanceID);
        _seenSources.Remove(actor.InstanceID);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var (source, target) in _links)
            if (!source.IsDeadOrDestroyed && !target.IsDeadOrDestroyed)
                Arena.AddLine(source.Position, target.Position, Colors.Danger);
    }
}

sealed class Conformity(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCone Shape = new(50f, 22.5f.Degrees());

    protected override AOEConfig? ConfigFor(uint actionID) => actionID is (uint)AID.ConformityBoss or (uint)AID.ConformityDaughter ? new(Shape) : null;
}

// ArachnidFunnel 已移除：其 C4C4 aftershock / C5F8 脉冲显示并入 ArachnidFunnelPath（窗口段承担脉冲伤害，
// 避免与滚动窗口重复显示）；C5F8 脉冲同时驱动窗口滚动（该段含脉冲完全结束才移除）。

// 蜘蛛突进轨迹预测（四级精度）：C4C3 读条开始时先用各小怪快照位直接生成整条轨迹（立即预警）；
// 之后检测到小怪位移（>=1y）后用（快照→当前）方向与 R25 圆环求交精化；停稳（距环 <0.5y 或 0.3s 不动）
// 直接取实际坐标；C4C4/C5F8 结算时以实际坐标接管。滚动显示即将发生的 3 段矩形（宽 20，第 1 段从 boss
// 位置出发），窗口由 C5F8 脉冲驱动滚动（该段含脉冲完全结束才移除，C4C4 缺失脉冲时兜底）。同时逐段
// AddForbiddenZone，AI 视觉与寻路禁区一致：最近结算段紧迫度最高（Danger 色+最紧禁区），
// 禁区带 activation 时间感知自动解除。
sealed class ArachnidFunnelPath(BossModule module) : Components.GenericAOEs(module)
{
    private const float HalfWidth = 10f; // 突进矩形半宽（总宽 20）
    private const float RingRadius = 25f; // 小怪停点落在圆心 R25 圆环上（回放实测，偏差≤0.16）
    private const double SegmentInterval = 1.55d; // 段间隔（回放实测）
    private const double SampleDelay = 0.5d; // cast 开始后约 0.5s 采样移动方向
    private const int MaxDisplayedSegments = 3; // 滚动 3 段
    private const float MinSwapAngleRad = 0.1745f; // 吸附换点防抖：累计方向与旧停点夹角 <10°（约 0.1745 rad）则不换
    private const double DisplayDelay = 2d; // 读条开始后延迟显示矩形链：确保首轮吸附（0.5s 采样+2y 检查点）已在 2s 内完成

    // 小怪固定停点（实测 2026-08-18，误差 ≤1y）：4 连轮为正交点、6 连轮为斜向点，均落在圆心 R25 圆环上。
    // 圆环预测（RingPredicted）算出交点后吸附到最近停点，消除射线求交的方向误差。
    private static readonly WPos[] KnownLandingPoints =
    [
        new(195f, -136f), new(170f, -111f), new(145f, -136f), new(170f, -161f), // 4 连轮正交点
        new(152.25f, -118.25f), new(187.75f, -153.75f), new(187.75f, -118.25f), new(152.25f, -153.75f) // 6 连轮斜向点
    ];

    private enum LandingPrecision { Snapshot, RingPredicted, Stopped, Actual }

    private readonly List<AOEInstance> _displayed = [with(MaxDisplayedSegments)];

    private bool _active;
    private DateTime _castStart;
    private DateTime _firstActivation;
    private WPos _bossStart;
    private readonly List<ulong> _castChain = []; // 以 cast 事件为权威的有序链（C4C1 链头 + C4C2 传播），OnCastStarted 用其初始化
    private readonly List<ulong> _order = []; // 与 _landing 对齐的链 ID（快照时的有效小怪，供 Update/Settle 定位）
    private readonly List<WPos> _snapshots = []; // cast 开始时各小怪位置（快照位，第一级精度）
    private readonly List<WPos> _lastPos = []; // 与 _order 对齐的上帧位置（停稳检测）
    private readonly List<DateTime> _stopSince = []; // 与 _order 对齐的停稳开始时刻（default=未开始）
    private readonly List<(WPos Pos, LandingPrecision Precision)> _landing = []; // 各段落点（快照位→圆环预测→停稳→实际）
    private readonly List<float> _nextCheckSq = []; // 与 _order 对齐：下一吸附检查点的位移平方阈值（2y=4 起步，递增）
    private int _settled; // 窗口起点（已完全结束=脉冲已过的段数，由 C5F8 推进 / C4C4 兜底）
    private int _startedSegments; // 已发生突进的段数（C4C3 + C4C4 计数），用于 C5F8 缺失时兜底滚动

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.ArachnidFunnel || spell.EventHappened)
        {
            return;
        }

        _active = true;
        _castStart = WorldState.CurrentTime;
        _firstActivation = Module.CastFinishAt(spell);
        _bossStart = caster.Position;
        _settled = 0;
        _startedSegments = 0;
        _order.Clear();
        _snapshots.Clear();
        _landing.Clear();
        _lastPos.Clear();
        _stopSince.Clear();

        // 用 cast 事件建立的有序链（C4C1 链头 + C4C2 传播）；过滤已销毁/找不到的小怪，保持各列表对齐
        foreach (var id in _castChain)
        {
            if (WorldState.Actors.Find(id) is { } daughter && !daughter.IsDeadOrDestroyed)
            {
                _order.Add(id);
                _snapshots.Add(daughter.Position);
                _landing.Add((daughter.Position, LandingPrecision.Snapshot)); // 快照位立即作为第一级精度
                _lastPos.Add(daughter.Position);
                _stopSince.Add(default);
                _nextCheckSq.Add(4f);
            }
        }

        // 兜底：若 cast 建链缺失（C4C1/C4C2 事件未到齐），回退到场上所有活的小怪按
        // InstanceID 排序，至少保证首段预警从读条第一帧就显示
        if (_order.Count == 0)
        {
            foreach (var daughter in Module.Enemies((uint)OID.Daughter).OrderBy(d => d.InstanceID))
            {
                if (!daughter.IsDeadOrDestroyed)
                {
                    _order.Add(daughter.InstanceID);
                    _snapshots.Add(daughter.Position);
                    _landing.Add((daughter.Position, LandingPrecision.Snapshot));
                    _lastPos.Add(daughter.Position);
                    _stopSince.Add(default);
                    _nextCheckSq.Add(4f);
                }
            }
        }
    }

    public override void Update()
    {
        if (!_active || WorldState.CurrentTime < _castStart.AddSeconds(SampleDelay))
        {
            return;
        }

        for (var i = 0; i < _landing.Count; ++i)
        {
            if (i >= _order.Count)
            {
                continue;
            }
            if (WorldState.Actors.Find(_order[i]) is not { } daughter || daughter.IsDeadOrDestroyed)
            {
                continue;
            }

            var snapshot = _snapshots[i];
            var current = daughter.Position;

            // 渐进式重评估：小怪路径非直线（先沿出生 heading 走一段再拐弯），位移 2y 时一次定终身会把
            // 拐弯前方向当最终方向吸错停点。故在离散检查点（2y/5y/10y/15y）用累计位移方向重新方向匹配，
            // 允许更换吸附点；位移越大方向越接近 (停点−出生点) 真方向。仅 Snapshot/RingPredicted 参与，
            // Stopped/Actual 已定不再重估。Snapshot 级保持快照原位不吸附，等首次检查点再评估。
            if (_landing[i].Precision is LandingPrecision.Snapshot or LandingPrecision.RingPredicted)
            {
                var dispSq = (current - snapshot).LengthSq();
                if (dispSq >= _nextCheckSq[i])
                {
                    var newPoint = SnapToKnown(current, snapshot, i);
                    var oldPoint = _landing[i].Pos;
                    // 防抖：新旧不同且累计方向与旧点夹角 <10°（旧点已可信）不换，避免末端噪声换点
                    if ((newPoint - oldPoint).LengthSq() > 0.5f)
                    {
                        var moveDir = (current - snapshot).Normalized();
                        var angleToOld = MathF.Acos(Math.Clamp(moveDir.Dot((oldPoint - current).Normalized()), -1f, 1f));
                        if (angleToOld >= MinSwapAngleRad)
                        {
                            _landing[i] = (newPoint, LandingPrecision.RingPredicted);
                        }
                    }
                    _nextCheckSq[i] = NextCheckpoint(dispSq);
                }
            }

            // 问题3：停稳即接管（Stopped 级，第四精度）——距 R25 圆环 <0.5y 或连续 ~0.3s 位置不变，
            // 直接从快照/预测跃迁到实际坐标，不等结算
            if (_landing[i].Precision == LandingPrecision.Actual)
            {
                continue;
            }
            if (MathF.Abs((current - Module.Arena.Center).Length() - RingRadius) < 0.5f)
            {
                _landing[i] = (current, LandingPrecision.Stopped);
                continue;
            }
            if (_stopSince[i] == default)
            {
                _lastPos[i] = current;
                _stopSince[i] = WorldState.CurrentTime;
            }
            else if ((current - _lastPos[i]).LengthSq() < 0.01f)
            {
                if (WorldState.CurrentTime >= _stopSince[i].AddSeconds(0.3d))
                {
                    _landing[i] = (current, LandingPrecision.Stopped);
                }
            }
            else
            {
                _lastPos[i] = current;
                _stopSince[i] = WorldState.CurrentTime;
            }
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (_active && spell.Action.ID == (uint)AID.ArachnidFunnel)
        {
            SettleSegment(0); // 首段读条结束兜底：实际落点接管
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        // 链构建：以 cast 事件为权威（tether 方向可能反转/瞬态不可靠），含 inactive 时期也要维护
        switch (spell.Action.ID)
        {
            case (uint)AID.ArachnidWebStart: // C4C1：boss 连第一只小怪，链头（cast 方向可能反转，取小怪端）
                {
                    var headID = IsDaughter(caster.InstanceID) ? caster.InstanceID : spell.MainTargetID;
                    AddToCastChain(headID);
                    break;
                }
            case (uint)AID.ArachnidWebLink: // C4C2：小怪→小怪传播，方向无关地插入
                AddCastLink(caster.InstanceID, spell.MainTargetID);
                break;
        }

        if (!_active)
        {
            return;
        }

        switch (spell.Action.ID)
        {
            case (uint)AID.ArachnidFunnel:
                SettleSegment(0); // 首段落点实际接管（窗口不滚，Danger 持续到该段脉冲 C5F8 结束）
                _startedSegments = Math.Max(_startedSegments, 1);
                break;
            case (uint)AID.ArachnidFunnelContinue:
                ++_startedSegments;
                // 兜底：若上一段的 C5F8 未到，C4C4 到达时上一段应已完全结束，窗口推进到该段
                _settled = Math.Max(_settled, _startedSegments - 1);
                SettleSegment(_startedSegments - 1); // 当前段落点实际接管
                break;
            case (uint)AID.ArachnidFunnelAftershock:
                // C5F8 脉冲：该段（含脉冲伤害）完全结束，窗口滚动
                _settled = Math.Min(_settled + 1, _landing.Count);
                break;
        }
    }

    private bool IsDaughter(ulong id) => WorldState.Actors.Find(id) is { } a && a.OID == (uint)OID.Daughter;

    // 把 id 作为链头（仅小怪，去重）
    private void AddToCastChain(ulong id)
    {
        if (IsDaughter(id) && !_castChain.Contains(id))
        {
            _castChain.Add(id);
        }
    }

    // 把 source→target 的传播插入链（两端都应是小怪；方向无关地保证 target 放在 source 之后）
    private void AddCastLink(ulong sourceID, ulong targetID)
    {
        var srcOk = IsDaughter(sourceID) && !_castChain.Contains(sourceID);
        var tgtOk = IsDaughter(targetID) && !_castChain.Contains(targetID);
        if (!srcOk && !tgtOk)
        {
            return;
        }
        if (srcOk)
        {
            _castChain.Add(sourceID);
        }
        if (tgtOk)
        {
            var idx = _castChain.IndexOf(sourceID);
            if (idx >= 0)
            {
                _castChain.Insert(idx + 1, targetID);
            }
            else
            {
                _castChain.Add(targetID);
            }
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (_active && _order.Contains(actor.InstanceID))
        {
            _active = false; // 链上小怪死亡，预测链失效，等待下一轮
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        // 读条开始后 2s 内不显示（雷达与 AI 视觉同源，走 ActiveAOEs 即两者一致）：此时已完成首轮吸附，
        // 矩形链生成即固定在正确位置，避免移动前/吸附中的错误预警
        if (!_active || _landing.Count == 0 || WorldState.CurrentTime < _castStart.AddSeconds(DisplayDelay))
        {
            return [];
        }

        // 问题2：Snapshot 级只记录数据不显示——所有段都还是快照位（小怪未移动、方向未知）时返回空，
        // 等位移检测触发、方向匹配吸附出正确停点（RingPredicted 及以上）后才开始显示
        var anyPredicted = false;
        for (var i = 0; i < _landing.Count; ++i)
        {
            if (_landing[i].Precision != LandingPrecision.Snapshot)
            {
                anyPredicted = true;
                break;
            }
        }
        if (!anyPredicted)
        {
            return [];
        }

        // 滚动 3 段：从 _settled（已结算段）开始显示后面最多 3 段。不用 activation 与当前时间比较，
        // 避免 _firstActivation 异常时被"跳过已结算"判断把全部段当过去而静默清空
        var shown = 0;
        for (var i = _settled; i < _landing.Count; ++i)
        {
            if (shown >= MaxDisplayedSegments)
            {
                break;
            }

            var end = _landing[i].Pos;
            var start = i == 0 ? _bossStart : _landing[i - 1].Pos;
            AddRect(_displayed, start, end, _firstActivation.AddSeconds(i * SegmentInterval), i == _settled ? Colors.Danger : Colors.AOE);
            ++shown;
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    private static void AddRect(List<AOEInstance> destination, WPos origin, WPos target, DateTime activation, uint color)
    {
        var direction = target - origin;
        if (direction.LengthSq() < 0.01f)
        {
            return;
        }

        var shape = new AOEShapeRect(direction.Length(), HalfWidth);
        var rotation = Angle.FromDirection(direction);
        // risky: true —— GenericAOEs 基类 AddAIHints 会逐段 AddForbiddenZone（矩形+该段结算时刻 activation），
        // 寻路提前规划前往安全点，且禁区带 activation 时间感知自动处理"该段结算后解除"
        destination.Add(new(shape, origin, rotation, activation, color, risky: true, shapeDistance: shape.Distance(origin, rotation)));
    }

    // 方向匹配吸附：对 8 个候选停点，取 (停点−当前位置) 方向与实际移动方向(快照→当前)夹角最小者；
    // 排除同轮内已被其他段吸附占用的停点（4/6 小怪各占一点，互斥），提高区分度。
    // 移动方向本质 = (停点−出生点) 方向，方向匹配不受交点距离放大影响。
    private WPos SnapToKnown(WPos current, WPos snapshot, int selfIndex)
    {
        var moveDir = (current - snapshot).Normalized();
        var best = current;
        var bestAngle = float.MaxValue;
        foreach (var p in KnownLandingPoints)
        {
            // 占用排除：其他段（j != self）已吸附到该点（非 Snapshot）则不再作为候选
            var occupied = false;
            for (var j = 0; j < _landing.Count; ++j)
            {
                if (j == selfIndex || _landing[j].Precision == LandingPrecision.Snapshot)
                {
                    continue;
                }
                if ((p - _landing[j].Pos).LengthSq() < 0.5f)
                {
                    occupied = true;
                    break;
                }
            }
            if (occupied)
            {
                continue;
            }

            var dirToPoint = (p - current).Normalized();
            var angle = MathF.Acos(Math.Clamp(moveDir.Dot(dirToPoint), -1f, 1f));
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = p;
            }
        }
        return best;
    }

    // 吸附检查点序列（位移平方）：2y=4 → 5y=25 → 10y=100 → 15y=225，之后不再重评估
    private static float NextCheckpoint(float dispSq)
    {
        if (dispSq < 25f) return 25f;
        if (dispSq < 100f) return 100f;
        if (dispSq < 225f) return 225f;
        return float.MaxValue;
    }

    private void SettleSegment(int index)
    {
        if (index < 0 || index >= _landing.Count)
        {
            return;
        }
        // 用小怪实际坐标替换预测落点（第三级精度），后续段以实际为准；
        // 注意：_settled（窗口滚动）由 C5F8 推进 / C4C4 兜底，不在此处推进
        if (index < _order.Count && WorldState.Actors.Find(_order[index]) is { } daughter)
        {
            _landing[index] = (daughter.Position, LandingPrecision.Actual);
        }
    }
}

// Two origins execute the 0-10, 10-20 and 20-30 waves at two-second intervals. Draw the entire
// upcoming sequence, but only make the currently resolving pair risky so automation never treats
// all three concentric regions as simultaneously forbidden.
sealed class BedrockUplift(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Circle = new(10f);
    private static readonly AOEShapeDonut Middle = new(10f, 20f);
    private static readonly AOEShapeDonut Outer = new(20f, 30f);

    protected override int MaxDisplayed => 6;
    protected override double RiskyActivationWindow => 0.25d;

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.BedrockUpliftCircle => new(Circle),
        (uint)AID.BedrockUpliftMiddle => new(Middle),
        (uint)AID.BedrockUpliftOuter => new(Outer),
        _ => null
    };
}

// Each helper carries a different subset of players, so use the boss visual once per raidwide.
sealed class Implosion(BossModule module) : Components.RaidwideCast(module, (uint)AID.ImplosionVisual);

// The daughters normally die before this finishes. If one survives, the cast is a raidwide enrage;
// keeping it as predicted damage also makes automation prioritize the already-drawn adds.
sealed class VenomEruption(BossModule module) : Components.RaidwideCast(module, (uint)AID.VenomEruption);
sealed class Daughters(BossModule module) : Components.Adds(module, (uint)OID.Daughter, 1);

sealed class WebOfTerrorStates : StateMachineBuilder
{
    public WebOfTerrorStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<LethalBoundary>()
            .ActivateOnEnter<ArachnidWeb>()
            .ActivateOnEnter<Conformity>()
            .ActivateOnEnter<ArachnidFunnelPath>()
            .ActivateOnEnter<BedrockUplift>()
            .ActivateOnEnter<Implosion>()
            .ActivateOnEnter<VenomEruption>()
            .ActivateOnEnter<Daughters>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(WebOfTerrorStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 55u,
    SortOrder = 12)]
public sealed class WebOfTerror(WorldState ws, Actor primary) : BossModule(ws, primary, new(170f, -136f), new ArenaBoundsCircle(25f));
