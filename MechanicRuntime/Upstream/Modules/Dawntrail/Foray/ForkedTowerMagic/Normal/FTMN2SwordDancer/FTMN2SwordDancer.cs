// 魔之塔（The Forked Tower: Magic，国服"两岐塔 魔之塔"）Normal 第 2 战：剑舞者（Sword Dancer）。
// 场地中心 (600, 704)、boss 模型 0x4D76（BNpcName 14820）等实体数据来自 2026-08-06 国服回放实测
// （ZoneID 1346 新月岛北部）。OID/AID/SID 枚举由 The Combat Reborn Team (LTS) 数据导入生成。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN2SwordDancer;

[ModuleInfo(BossModuleInfo.Maturity.Contributed, // 恢复显示继续测试（2026-08-09）
    StatesType = typeof(SwordDancerStates),
    ConfigType = null, // 如需要可替换为 typeof(SwordDancerConfig)
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    StatusIDType = typeof(SID),
    TetherIDType = typeof(TetherID),
    IconIDType = null, // 如需要可替换为 typeof(IconID)
    PrimaryActorOID = (uint)OID.SwordDancer,
    Contributors = "The Combat Reborn Team (LTS)",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1093u,
    NameID = 14820u,
    SortOrder = 2,
    PlanLevel = 0)]
[SkipLocalsInit]
// 场地圆形 R24：2026-08-06 回放实测，原 Circle(25f) 外扩 1y，按实测修正。
// boss 可目标化（回放 ATG+），CheckPull 默认即可。
// 死亡兜底（2026-08-07 深查修复）：boss 死亡（DIE+）时强制结束状态机（StateMachine.Reset），
// 保证模块被 BMM 卸载（BMM 仅当 ActiveState==null 时卸载）——覆盖状态机卡在中间相位、
// boss 提前死亡等场景，避免雷达被 boss2 持续占用挡掉后续 boss3/4。
public sealed class SwordDancer : BossModule
{
    public SwordDancer(WorldState ws, Actor primary) : base(ws, primary, new(600f, 704f), new ArenaBoundsCircle(23.7f))
    {
        // 舞动之剑全部列为敌人（2026-08-07 用户要求：方便查询对应情况）——
        // 预填充 RelevantEnemies：4D77 投剑突进/回旋、4D79 回转、4D7A 跃进步法四剑、4D7C 八剑突进
        // 233C 剑舞者分身（2026-08-09 用户要求：列入敌对列表方便对照剑舞——49614 剑舞直条施法者即 233C Helper）
        Enemies(new uint[] { (uint)OID.DancingSword4, (uint)OID.DancingSword3, (uint)OID.DancingSword2, (uint)OID.DancingSword, (uint)OID.DancingSword5 });
        ActivateComponent<CycloswordsPreview>(); // 风旋剑出鞘钢月预判（按剑形态，跨相位常驻）
        ActivateComponent<ThrownSwords>(); // 投剑短/长矩形预警（跨相位常驻）
    }

    protected override void UpdateModule()
    {
        if (PrimaryActor.IsDeadOrDestroyed && StateMachine.ActiveState != null)
        {
            StateMachine.Reset();
        }
    }
}

// ==================== 组件（形状/时机均来自 2026-08-06 三场回放实测核对） ====================

// 剑技风暴：全屏 AoE（开战/循环收尾，回放确认全屏无落点，读条 5.0s）
sealed class SwordStorm(BossModule module) : Components.RaidwideCast(module, (uint)AID.SwordStorm1, "剑技风暴：全屏伤害");

// 投剑（2026-08-07 用户参数：50525 短 11×7 / 50526 长 21.5×7，剑面向为基准；回放落点验证长度）：
// 4D77 剑（实体在中心 (600,704)）与 boss 49559/49560 投剑同帧施放（波1 一对、波2/3 两对）；
// 方向=落点方向（回放 CST+ 落点为 8 个基点 ±11.5/±21.5；Angle.FromDirection 为几何方向，无游戏 rotation 的 180° 换算问题）；
// 投剑长度动态自适应（2026-08-09 用户反馈波1 互换：50525/50526 的 AID→长短映射在波1（开战）互换反常——
// 回放三场确认波1 50525→21.5、50526→11.5，波2/3 正常反向；改为长度=实际落点距离（(spell.LocXZ-caster.Position).Length()），
// AID 不再映射长短，波1 自动正确）。
// AI 预警由 GenericAOEs 基类自动处理（risky）。
sealed class ThrownSwords(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect AiShape = new(24f, 3.5f); // AI 统一长矩形（覆盖到墙）
    private readonly List<AOEInstance> _aoes = [with(8)];

    public override bool KeepOnPhaseChange => true; // 每轮投剑均触发，跨相位常驻（模块构造激活）

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    // AI 视觉统一长矩形（2026-08-07 用户要求：50525/50526 存在反常互换，AI 两种 id 都按 24 长覆盖到场边驱赶 AI，
    // 更符合人为控制走位；雷达保持短/长区分）
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var a in _aoes)
        {
            hints.AddForbiddenZone(AiShape, a.Origin, a.Rotation, a.Activation); // 剑面向 24 长半宽 3.5，AI 避开整个路径被驱赶到两侧
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is not (uint)AID.Rush and not (uint)AID.Rush1) // 50525/50526 均监听，长度不按 id 映射
        {
            return;
        }

        // 突进方向改用落点（2026-08-07 修复：剑 rotation 字段恒定 -180，方向编码在落点 dest）
        // 长度动态自适应（2026-08-09）：= 落点距离（11.5 或 21.5），不再依赖 AID 固定映射（波1 互换自动适配）；
        // 雷达（动态长度）与 AI（统一 24）均使用落点方向——AddAIHints 取 a.Rotation
        var dir = Angle.FromDirection(spell.LocXZ - caster.Position);
        var shape = new AOEShapeRect((spell.LocXZ - caster.Position).Length(), 3.5f); // 长度=实际落点距离、半宽 3.5
        _aoes.Add(new(shape, caster.Position, dir, Module.CastFinishAt(spell), actorID: caster.InstanceID, shapeDistance: shape.Distance(caster.Position, dir)));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.Rush or (uint)AID.Rush1)
        {
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }
}

// 秘法剑：boss 位移到 4 边中点（±11.5/21.5y）后 Helper 49585 在落点放 96x96 矩形（Rect 48x48，读条 5.5s）。
// 回放实测（筱筱/Ucey 受击位置）确认矩形从落点沿 cast 方向延伸、半宽 48；玩家站矩形覆盖的半场对面即可
// （XML 提示"去左手侧/右手侧"），AI 禁入区自动引导避让。
sealed class MartialMystique(BossModule module) : Components.SimpleAOEs(module, (uint)AID.MartialMystique2, new AOEShapeRect(48f, 48f));

// 风旋剑（2026-08-09 用户修正：49586"风旋剑出鞘"为机制开始标记、无法预测任何 AOE 区域，不预警——
// 原 CycloswordsPreview 的 R15 圈已删除）：49586 读完 → 4D79 剑出现并开始剑刃朝外/朝内旋转
// （回放确认剑挂 3558 状态、TARG 选玩家）→ 依据剑刃朝向确定 AOE 范围 → 49587"风旋剑"读完
// → 剑读条 49592（钢铁 R15）/49589（月环 15~60）结算（回放时序 04 场第三轮：
// 49586 完成 06:15:10.19 → 剑出现 06:15:11.05 → 49587 完成 06:15:18.39 → 剑读条 06:15:18.39 起、1.6s 结算）。
// 剑刃朝向回放无法区分（剑静止 rotation 无一致规律：月环时东剑朝内/北剑朝外/中心剑朝南；旋转动画不回放），
// 故按剑读条 id 直接预警（读条开始即画，提前量 1.6s）；AOE 形状已由受击目标验证。

// 回转-月环：4D79 剑在自身位置放 donut 15~60（贴剑 15y 内安全）。回放实测：剑在中心时全员站中心
// 无受击；剑在东 11.5y 时 20.5y 外玩家受击（15<20.5<60 环内 ✓）。
// 49590 月环变体（2026-08-09 回放补充：双剑轮另一 id 同形——08-09 07:07:59 轮 DD 剑 49590+模型 5、
// 玩家距剑 11y/6.8y 在 15y 内安全区无受击，与 49589 同形）。
// 月环内缘=钢铁外缘（2026-08-07 用户实测修正）：钢铁 R15（SpinOut）→ 月环 donut 内缘 15（原 5-60 内缘偏小）
sealed class SpinRing(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.Spin, (uint)AID.Spin3], new AOEShapeDonut(15f, 60f));

// 回转-钢铁：4D79 剑在自身位置放 R15 圆（远离 15y）。回放实测：剑在中心时 4.3y 处玩家受击
// 确认 R15 覆盖；剑在西 11.5y 时 10.6y 处玩家受击同样在圆内。
sealed class SpinOut(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Spin1, 15f);

// 回转-钢铁（R20）：XML 标注 WeaponId 7/1F 时 R20（C1B9），三场回放未出现，按数据备用
sealed class SpinOutFar(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Spin2, 20f);

// 风旋剑出鞘钢月预判（2026-08-09 逆向 ACT：WeaponId 字段 4/5=月环 7=钢铁R15 1F=钢铁R20；出鞘即画、结算读条清除）。
// 回放验证（04 场，MDLS 事件 = Actor.ModelState.ModelState，来源 FFXIVClientStructs Character->Timeline.ModelState）：
// 06:15:07.105 剑98 模型切 4 / 剑97 切 7（与 06:15:07.215 出鞘 49586 读条开始同帧）→ 其后剑98 放 49589 月环 ✓
// 剑97 放 49592 钢铁 ✓；06:12:36 切 4→月环、06:12:52 切 7→钢铁、结算后恢复 33（三组全验证）。
// 4D79 剑常驻存在（06:11:39 生成、无销毁，出鞘时必在场，无需 fallback）；出鞘时剑已在基点（东/西 11.5y）。
// 预判 AOE 以剑实体位置为 origin、纯视觉（不 risky，AI 规避仍由 SpinRing/SpinOut 在结算读条时处理）；
// 49592/49589 结算读条开始时按剑清除。
sealed class CycloswordsPreview(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _displayed = [with(4)];

    public override bool KeepOnPhaseChange => true; // 每轮风旋剑出鞘均触发，跨相位常驻（模块构造激活）

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_displayed);

    // 形态映射：4/5 → 月环 15~60、7 → 钢铁 R15、1F(31) → 钢铁 R20（ACT 查证，回放验证 4/7）
    private static AOEShape? ShapeFor(byte modelState) => modelState switch
    {
        4 or 5 => new AOEShapeDonut(15f, 60f),
        7 => new AOEShapeCircle(15f),
        0x1F => new AOEShapeCircle(20f),
        _ => null,
    };

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.CycloswordsUnsheathed) // 49586 出鞘读条开始：按剑形态预判钢月（提前 ~12s）
        {
            _displayed.Clear();
            foreach (var sword in Module.Enemies((uint)OID.DancingSword3)) // 4D79 回转剑（施放 49589 月环 / 49592/93 钢铁）
            {
                var shape = ShapeFor(sword.ModelState.ModelState);
                if (shape != null)
                {
                    _displayed.Add(new(shape, sword.Position, default, default, actorID: sword.InstanceID)); // 以剑位置为 origin，显示到结算读条开始
                }
            }
        }
        else if (spell.Action.ID is (uint)AID.Spin or (uint)AID.Spin3 or (uint)AID.Spin1 or (uint)AID.Spin2) // 49589/49590/49592/49593 结算读条开始 → 清除预判（实际 AOE 由 SpinRing/SpinOut 接管）
        {
            _displayed.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }

    // AI 视觉提前（2026-08-09 用户要求：出鞘读条结束即加 AI 禁区，结算读条仅 ~0.7s 需提前就位）——
    // 出鞘读完时预判 AOE 置 Risky=true，基类 AddAIHints 按剑位置/形状立即加 ForbiddenZone（activation=default 死区）
    // 双保险清除（2026-08-09 用户实测双 AOE 仅清一个）：结算读条开始（OnCastStarted）与结束（OnCastFinished）
    // 均按剑 InstanceID 移除预判圈，覆盖读条开始时组件才收到/事件顺序异常等边缘场景
    // 双 AOE 清除 v2（2026-08-09 用户案例 07:07:59 400254DD 残留根因：DD 结算读条为 49590"回转"（月环变体 id）——
    // 不在原清除列表 49589/49592/49593 → 预判圈不匹配 caster.InstanceID 残留；已补 49590 并同步 SpinRing 监听）
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.CycloswordsUnsheathed)
        {
            for (var i = 0; i < _displayed.Count; ++i)
            {
                _displayed[i] = _displayed[i] with { Risky = true };
            }
        }
        else if (spell.Action.ID is (uint)AID.Spin or (uint)AID.Spin3 or (uint)AID.Spin1 or (uint)AID.Spin2) // 49589/49590/49592/49593 结算读条结束：再清一次（双保险）
        {
            _displayed.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }
}

// 剑舞（2026-08-12 合并上游 EAnim 方案升级"前 3 刀提前全知"）：机制链 = EAnim 标记全知预判 + 49609 首刀 0° 兜底 + 49614 按序清除。
// 事件物件 0x1EC033 发 EAnim(0x00010002) 标记（上游 codex 实测：每刀一物件、按顺序刷出，真实读条前预绘）——
// 收齐 4 个标记即全知 4 刀方向与时间（收齐后 6.4/8.9/11.4/13.9s 结算，间隔 2.5s），
// 前 3 刀立即提前绘制（雷达 + AI 视觉，紧迫度分级：最先生效深黄、其余浅黄）；第 4 刀待首刀（首个 49614）结算后入列显示
// （避免 4 刀全显遮全场，且与本地 hideLast 语义一致）；方向 = 事件物件 rotation（游戏角；Rect 前后对称 180° 无差别，
// 无需换算）——首刀仍以本地实测恒 0°（BossMod 180°）为准（2026-08-09 用户查证；标记 1 rotation 应为游戏 0° 北，
// 与本地一致，若回放发现偏差改取 markers[0].Rotation）。
// 兜底互备：49609 读条缺失时 EAnim 标记仍可全知；EAnim 标记缺失/未收齐时退回 49609 首刀 0° 预警 + 49614 动态入列
// （读条开始入列、结算移除，方向 = spell.Rotation + 180° 换算）。
// 清除统一以 49614 读条事件为准：结算（OnCastFinished）按序移除队列首（EAnim 预画与兜底入列统一按结算顺序对齐）。
// 残留 bug 修复（2026-08-09 用户实测）：原"结算只递进不移除"致矩形固化到下一次剑舞——GenericAOEs 基类
// 绘制/禁区不按 activation 自动过滤（DrawArenaBackground/AddAIHints 直接遍历 ActiveAOEs），须组件在结算时移除。
// 方向修正（2026-08-07 修复"半个矩形"）：spell.Rotation 为游戏角度（0=北），BossMod 角度 0=南，差 180°——
// 回放核对：施法者=场地中心 (600,704)、落点=中心沿方向 30y → Rect(30,10,30) 以中心为 origin 向两侧各 30 横穿全场。
sealed class SwordDance(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect Shape = new(30f, 10f, 30f); // 横穿全场（宽 20）
    private static readonly AOEShapeRect GuideShape = new(30f, 12f, 30f); // 绿色引导区（宽 24，仅 AI 视觉）
    private static readonly Angle FirstDir = 180f.Degrees(); // 首刀方向：游戏角 0°（北）→ BossMod 角 180°
    private readonly List<(ulong ActorID, WPos Position, Angle Rotation)> _markers = [with(4)]; // EAnim 标记（0x1EC033 事件物件，收齐 4 个即消费清空，可跨轮复用）
    private readonly List<(WPos Origin, Angle Rotation, DateTime Activation, ulong ActorID)> _preview = [with(4)]; // EAnim 全知 4 刀（位置/方向/时间/来源）
    private bool _previewReady; // 本轮到 4 标记收齐（EAnim 方案生效：前 3 刀已入列）
    private readonly List<AOEInstance> _slashes = [with(4)]; // 未结算的刀（EAnim 预画 / 49614 兜底入列），按结算顺序
    private readonly List<AOEInstance> _displayed = [with(4)]; // 雷达
    private readonly List<AOEInstance> _ai = [with(4)]; // AI 禁区
    private bool _firstActive; // 首刀预警区/绿色引导区有效（49609 读条开始 → 首个 49614 结算）
    private int _resolved; // 已结算刀数（按序移除、第 4 刀入列时机、首刀结算判定）

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_displayed);

    // EAnim 标记收集（上游 codex 方案，2026-08-12 吸收）：0x1EC033 事件物件发 EAnim(0x00010002) 预绘标记，
    // 收齐 4 个即全知 4 刀方向与时间（6.4/8.9/11.4/13.9s）——前 3 刀提前入列（雷达 + AI 视觉），第 4 刀待首刀结算
    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (state != 0x00010002 || actor.OID != (uint)OID.Actor1ec033)
        {
            return;
        }
        if (_markers.Any(m => m.ActorID == actor.InstanceID))
        {
            return;
        }

        _markers.Add((actor.InstanceID, actor.Position, actor.Rotation));
        if (_markers.Count < 4)
        {
            return;
        }

        // 4 标记收齐：全知 4 刀（方向 = 事件物件 rotation；首刀 = 本地实测恒 0°、origin = 场地中心）
        List<(ulong ActorID, WPos Position, Angle Rotation)> markers = [.. _markers];
        _markers.Clear(); // 消费后清空，可收下一轮标记
        _previewReady = true;
        var baseTime = WorldState.CurrentTime;
        _preview.Clear();
        for (var i = 0; i < 4; ++i)
        {
            var rotation = i == 0 ? FirstDir : markers[i].Rotation; // 首刀以本地实测 0° 为准（标记 1 应为游戏 0° 北）
            var origin = i == 0 ? Module.Center : markers[i].Position; // 首刀以场地中心为 origin（回放实测 49614 施法者=中心）
            _preview.Add((origin, rotation, baseTime.AddSeconds(6.4d + 2.5d * i), markers[i].ActorID));
        }
        _slashes.Clear();
        for (var i = 0; i < 3; ++i) // 前 3 刀提前全知入列；第 4 刀待首刀结算后显示
        {
            var p = _preview[i];
            _slashes.Add(new(Shape, p.Origin, p.Rotation, p.Activation, actorID: p.ActorID));
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.SwordDance1) // 49609 剑舞：新一轮重置 + 首刀恒为 0°（北）预警/引导
        {
            _slashes.Clear();
            _preview.Clear();
            _previewReady = false;
            _resolved = 0;
            _firstActive = true;
        }
        else if (spell.Action.ID == (uint)AID.SwordDance6) // 49614 直条劈下：EAnim 未提供该刀（标记缺失/未收齐）时动态入列兜底
        {
            if (!_previewReady)
            {
                _slashes.Add(new(Shape, caster.Position, spell.Rotation + 180f.Degrees(), Module.CastFinishAt(spell), actorID: caster.InstanceID));
            }
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.SwordDance6)
        {
            if (_slashes.Count > 0)
            {
                _slashes.RemoveAt(0); // 按序移除当前刀（EAnim 预画与兜底统一按结算顺序，修复固化残留）
            }
            ++_resolved;
            if (_firstActive)
            {
                _firstActive = false; // 首刀（首个 49614）结算：清除 0° 预警区与绿色引导区（后续由危险区接管）
            }
            if (_previewReady && _resolved == 1 && _preview.Count >= 4)
            {
                var p = _preview[3]; // 首刀已结算：第 4 刀入列显示（方向 = 第 4 标记 rotation）
                _slashes.Add(new(Shape, p.Origin, p.Rotation, p.Activation, actorID: p.ActorID));
            }
        }
    }

    public override void Update()
    {
        _displayed.Clear();
        _ai.Clear();
        if (_firstActive && !_previewReady) // 首刀 0° 预警区（49609 → EAnim 收齐窗口 / EAnim 缺失全程；收齐后由 _slashes 首刀承担，避免重复）
        {
            var first = new AOEInstance(Shape, Module.Center, FirstDir, default, Colors.Danger, true);
            _displayed.Add(first);
            _ai.Add(first);
        }

        var count = _slashes.Count;
        if (count == 0)
        {
            return;
        }

        var hideLast = count >= 4; // 兜底路径 4 道全劈（EAnim 路径最多 3 道在列）→ 第 4 道暂不显示
        for (var i = 0; i < count; ++i)
        {
            if (hideLast && i == count - 1)
            {
                continue;
            }

            var aoe = _slashes[i];
            var urgent = i == 0; // 当前最先生效（最早劈下）深黄
            _displayed.Add(urgent ? aoe with { Color = Colors.Danger, Risky = true } : aoe with { Color = default, Risky = false });
        }

        foreach (var a in _slashes) // AI：未结算刀全部禁入
        {
            _ai.Add(a with { Risky = true });
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_firstActive) // 绿色引导区（2026-08-09 用户设计：仅 AI 视觉 GoalZones，不画雷达；引导 AI 靠近 0° 位置）
        {
            hints.GoalZones.Add(p => GuideShape.Check(p, Module.Center, FirstDir) ? 1f : 0f);
        }

        foreach (var a in _ai)
        {
            hints.AddForbiddenZone(a.Shape.Distance(a.Origin, a.Rotation), a.Activation);
        }
    }
}

// 戳地：跃进步法后 4 把 4D7A 剑在 4 边中点（±18y）同时放 R5 圆（读条 3.6s，贴剑 5y 外）
sealed class Pierce(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Pierce, 5f);

// 跃步调整（2026-08-09 用户：不需要连续箭头，按剑读条生成单箭头；引导扇确认实现）。
// 4D7A 剑 49599（+50359 Helper 成对）按跳跃顺序结算击退 24y（来源=对应剑，远离剑方向，顶墙停止）；
// ABCD 动态记录：按跳跃顺序（49596 第 1 跳 / 49597 跳 2-4，瞬发 CST!，dest=落点；点位固定（四边中点 ±18）
// 但顺序不固定——回放 04 场：北→南→西→东、0557 场：南→东→西→北，不作顺序假设）；
// 引导区 v4（2026-08-09 用户要求：安全区改圆形——30° 扇安全区过小，改 3y 圆外全禁）：
// 根因链——49599 击退读条 2.0s、间隔 2.5s 不重叠，v1（在读条双剑）与 v2（_order.Count>_resolved+1）条件
// 均恒假（_order.Count 恒 = _resolved + 在读条数）；v3 改由跳跃 dest 记录（跳跃顺序=击退顺序，提前 ~7s 全齐），
// 安全区 = 以当前来源 a 为圆心、半径 3y 的圆形（绿色 ZoneCircleOutline + GoalZones 圆内引导 + inverted circle 圆外禁入）；
// 最后一段 D：以 D 为圆心 3y 圆（D 在读条时显示）；
// 击退箭头（2026-08-09 用户调整）：每把剑读条（CST+ 入列）时画该剑单箭头——从玩家当前位置出发、远离该剑方向、
// 24y、黄线+最终落点标记（boss1 样式），结算（CST!）移除；
// 其他区域：击退未开始（跳跃完成提前）或已结算 → 禁区立即死区；首段击退在读条 → 普通紧迫度（该击退结算时刻）。
sealed class Swordspear(BossModule module) : Components.GenericKnockback(module, stopAtWall: true)
{
    private readonly List<Knockback> _casters = [with(4)]; // 在读条击退（CST+ 入列、CST! 移除）
    private readonly List<WPos> _order = [with(4)]; // 49599 读条顺序（=ABCD）的剑位置（读条不重叠故单独记录，供引导扇取下一跳）
    private int _resolved; // 已结算来源数（紧迫度递进）

    public bool Active => _casters.Count != 0;

    // 引导区 v5（2026-08-09 用户澄清：绿色引导 30° 扇 + 禁入区圆形，互不影响）。
    // v3 根因：_order 按击退读条（49599 CST+）记录，读条不重叠致 _order.Count > _resolved+1 恒假；
    // v3：_order 改由跳跃（49596 第 1 跳 / 49597 跳 2-4，瞬发 CST!）的 dest 记录——跳跃顺序 = 击退顺序
    // （回放 04 场验证：跳跃落点 43.65/45.11/45.93/46.5x = 剑95 北/剑93 南/剑92 西/剑94 东，与 49599 读条
    // 53.49/56.01/58.50/01.07 完全一致），跳跃完成即 4 点全齐 → 击退①读条前 ~7s 就显示引导区。
    // 绿色引导区 = 以当前来源 a（_order[_resolved]）为圆心、指向下一来源 b 的 30° 扇区（半径 3y）；
    // 禁入区 = 以 a 为圆心半径 3y 圆外全禁（inverted circle，2026-08-09 用户要求保留圆形）；
    // 最后一段（D）：引导扇朝场中 (600,704)，仅 D 在读条时显示（D 结算后机制结束）。
    private static readonly WPos Center = new(600f, 704f);
    private const float GuideRadius = 6f; // 引导扇半径/禁区圆半径 6y（2026-08-09 用户要求放大：3y→6y）

    private (WPos center, Angle dir)? GuideSector()
    {
        if (_order.Count < 2)
        {
            return null; // ABCD 未齐（跳跃未完成）
        }

        var a = _order[Math.Min(_resolved, _order.Count - 1)];
        if (_resolved >= _order.Count - 1) // 最后一段（D）：引导扇朝场中
        {
            if (_casters.Count == 0)
            {
                return null; // D 已结算，机制结束
            }

            var dir = Center - a;
            return dir == default ? null : (a, Angle.FromDirection(dir));
        }

        var b = _order[_resolved + 1];
        return (a, Angle.FromDirection(b - a));
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor) => CollectionsMarshal.AsSpan(_casters);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Steelsbreath1) // 49599 4D7A 剑击退读条
        {
            _casters.Add(new(caster.Position, 24f, Module.CastFinishAt(spell), kind: Kind.AwayFromOrigin, actorID: caster.InstanceID));
            // 顺序已在跳跃阶段记录（LeapingLift1/2 OnCastFinished），此处不再重复
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Steelsbreath1) // 49599 击退结算
        {
            var idx = _casters.FindIndex(k => k.ActorID == caster.InstanceID);
            if (idx >= 0)
            {
                _casters.RemoveAt(idx); // 该来源击退已结算
                ++_resolved;
            }
        }
    }

    // v3 失效根因修复（2026-08-09）：49596/49597 为瞬发技能（无 CST+，只有 CST! = BossMod CastEvent 事件）——
    // 原用 OnCastFinished（对应 CST- 读条结束）监听永不触发，_order 恒空 → 引导扇不显示；
    // 改 OnEventCast 监听（CST! = OpCastEvent），TargetPos=落点=击退来源顺序
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.LeapingLift1 or (uint)AID.LeapingLift2) // 49596 第 1 跳 / 49597 跳 2-4
        {
            _order.Add(new WPos(spell.TargetPos.X, spell.TargetPos.Z)); // 跳跃落点顺序 = ABCD（回放 04 场验证：43.65/45.11 北/南/西/东 = 49599 读条顺序，提前 ~7s）
        }
    }

    // 绿色引导扇绘制 + 击退箭头（2026-08-09 用户澄清：引导区 30° 扇、禁入区圆形互不影响；单箭头，无连续链；v3 提前显示）
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (GuideSector() is (var center, var dir))
        {
            Arena.ZoneConeOutline(center, default, GuideRadius, dir, 15f.Degrees(), Colors.Safe); // 绿色引导扇（全角 30°、半径 3y）
        }

        foreach (var kb in _casters) // 每把在读条剑各画一个箭头：玩家位置出发、远离该剑 24y、黄线+最终落点标记
        {
            var from = pc.Position;
            var away = from - kb.Origin;
            if (away == default)
            {
                continue;
            }

            DrawKnockback(from, from + kb.Distance * away.Normalized(), pc.Rotation, Arena); // boss1 样式（黄线+落点标记）
        }
    }

    // 绿色引导扇（GoalZones）+ 圆形禁区（2026-08-09 用户澄清：引导区 30° 扇、禁入区圆形，互不影响）
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (GuideSector() is not (var center, var dir))
        {
            return; // ABCD 未齐或机制结束
        }

        // 绿色引导扇（全角 30°、半径 3y）：GoalZones 引导 AI 站扇内
        var cone = new AOEShapeCone(GuideRadius, 15f.Degrees());
        hints.GoalZones.Add(p => cone.Check(p, center, dir) ? 1f : 0f);

        // 圆形禁区（inverted circle，以当前来源为圆心 3y 圆外全禁）：击退未开始（_casters 空，跳跃完成后提前）
        // 或已结算 → 立即死区；首段击退在读条 → 普通紧迫度（该击退结算时刻）
        var activation = _resolved > 0 || _casters.Count == 0 ? default : _casters[0].Activation;
        var inverted = new AOEShapeCircle(GuideRadius, invertForbiddenZone: true);
        hints.AddForbiddenZone(inverted, center, default, activation);
    }
}

// 突进：4D7C 剑 8 把同时放 Rect 30x6（半宽 3，读条 4.0s）。
// 回放实测：横排波次（中心线 x=579~621 间隔 6y）交替朝南/朝北（每半场 4 条宽 6 间隔 6，站空隙）；
// 竖排波次（x=600 线 z=683~725 间隔 6y）全部朝 -90°（西），覆盖西半场，全员去东半场（该波无人受击）。
// 剑位置在中心线、方向沿 cast rotation 延伸 30y——回放受击者（迷途砂/埃拉诺尔/·银杏子·等）逐一验证。
sealed class Rush(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Rush2, new AOEShapeRect(30f, 3f), maxCasts: 8);

// 回旋=环扇（2026-08-09 用户实测：长 18.5~24/短 8.5~13.5，顺逆弧角；圆心场地中心 (600,704)，推翻 Cone 扇形实现）。
// 4D77 剑在基点（短=±11.5、长=±21.5）施放 8 种剑技 id，转动弧 = 环带扇形（读条 3.5s）：
// 起始角 = 从中心指向剑位置的方向（2026-08-09 回放验证：顺 id 剑移动目标=起点顺 90°（如 49563 剑北→落点东）、
// 逆 id=起点逆 90°（如 49568 剑南→落点东），落点=弧终点；收尾 id（78/67.5）落点仍为 90° 位置、弧角按用户实测收窄）；
// 顺=顺时针扫、逆=逆时针扫（BossMod 角 0=南、+90=东、顺时针=角度递减：顺弧中心=起点-半角、逆弧=起点+半角）。
// 清除时机（2026-08-12 用户实测修复）：动画判定——读条 3.2s 期间剑停在起点（回放 08-12 09:47:55.83 CST+ 剑在
// (600,725.49)、09:47:59.32 CST! 时剑仍在起点），读条结束（CST!）后 ~0.3s 起飞沿弧飞 ~0.8s 到达落点才结算；
// 原 OnCastFinished/OnEventCast 读条结束即清除危险区 → AI 在飞剑飞行期间走进环扇吃伤害；
// 改为轮询 4D77 剑位置到达落点（cast 落点，距 ≤1y）才清除（Update 到达判定 + 3s 兜底超时）。
sealed class Turn(BossModule module) : Components.GenericAOEs(module, warningText: "躲避回旋环扇")
{
    private static readonly WPos Center = new(600f, 704f); // 转动圆心 = 场地中心

    // 8 个剑技 id → (内径, 外径, 全角°, 顺/逆)
    private static (float inner, float outer, float angle, bool cw) Param(uint aid) => aid switch
    {
        (uint)AID.Turn => (8.5f, 13.5f, 90f, true), // 49563 短顺90
        (uint)AID.Turn9 => (18.5f, 24f, 90f, true), // 49565 长顺90
        (uint)AID.Turn10 => (8.5f, 13.5f, 90f, false), // 49566 短逆90
        (uint)AID.Turn3 => (18.5f, 24f, 90f, false), // 49568 长逆90
        (uint)AID.Turn11 => (18.5f, 24f, 78f, true), // 49571 长顺78收尾
        (uint)AID.Turn12 => (8.5f, 13.5f, 67.5f, false), // 49572 短逆67.5收尾
        (uint)AID.Turn13 => (18.5f, 24f, 78f, false), // 49574 长逆78收尾
        (uint)AID.Turn4 => (8.5f, 13.5f, 67.5f, true), // 49569 短顺67.5收尾
        _ => default,
    };

    private readonly List<AOEInstance> _aoes = [];
    private readonly List<(ulong ActorID, WPos Destination, DateTime CastEnd)> _flying = [with(4)]; // 读条结束后的飞剑（动画飞行中，到达落点才结算）
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var p = Param(spell.Action.ID);
        if (p.angle == 0)
        {
            return;
        }

        // 同剑新读条开始时清除旧状态（2026-08-17 修复：同剑连续读条时旧的 _flying 残留会按 ActorID
        // 一刀切误删新危险区——剑已到旧落点才触发新读条，旧刀必已失效，先清再入列）
        _flying.RemoveAll(f => f.ActorID == caster.InstanceID);
        _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);

        // 起始角 = 从中心指向剑位置方向；顺=顺时针（BossMod 角递减）→ 弧中心=起点-半角、逆 → 起点+半角
        var start = Angle.FromDirection(caster.Position - Center);
        var rot = p.cw ? start - (p.angle / 2f).Degrees() : start + (p.angle / 2f).Degrees();
        var shape = new AOEShapeDonutSector(p.inner, p.outer, (p.angle / 2f).Degrees());
        _aoes.Add(new(shape, Center, rot, Module.CastFinishAt(spell), actorID: caster.InstanceID, shapeDistance: shape.Distance(Center, rot)));
        // 落点 = cast 落点（08-13 回放验证：读条期间剑停在起点，读条结束（CST!）后起飞沿弧飞 ~0.5-1s 到达落点才结算）
        _flying.Add((caster.InstanceID, spell.LocXZ, Module.CastFinishAt(spell)));
    }

    public override void Update()
    {
        // 飞剑到达落点（距落点 ≤1y）→ 危险区结算清除（2026-08-12 修复：原 OnCastFinished/OnEventCast 读条结束即清除，
        // 但回放 08-12 09:47:59 实测读条结束时剑尚在起点、CST! 后 ~0.3s 才起飞飞行、~0.8s 到达落点——
        // 立即清除致 AI 在飞剑飞行期间走进环扇吃伤害；改轮询剑位置到达落点再清除）；兜底超时 3s（防剑实体缺失）
        // v2（2026-08-13 修复）：倒序 for + 移除内联——原 foreach 遍历 _flying 中调 TryResolve（内部 flying.RemoveAt
        // 修改同一 List）触发枚举器版本检查抛 InvalidOperationException → 组件异常 → BMM 判模块崩溃卸载 → 雷达消失、
        // 后续预警全无（用户实测）；事件回调上下文（OnCastFinished/OnEventCast）的 TryResolve 不受影响，保留
        for (var i = _flying.Count - 1; i >= 0; --i)
        {
            var f = _flying[i];
            var arrived = false;
            foreach (var sword in Module.Enemies((uint)OID.DancingSword4))
            {
                if (sword.InstanceID == f.ActorID && !sword.IsDeadOrDestroyed && (sword.Position - f.Destination).LengthSq() <= 1f)
                {
                    arrived = true;
                    break;
                }
            }
            if (arrived || WorldState.CurrentTime > f.CastEnd.AddSeconds(3d)) // 到达落点或兜底超时
            {
                _aoes.RemoveAll(a => a.ActorID == f.ActorID);
                _flying.RemoveAt(i);
            }
        }
    }

    // 到达判定：剑（4D77，按 InstanceID）位置距落点 ≤1y 或兜底超时 → 移除危险区与飞行项
    // （仅事件回调上下文使用：OnCastFinished/OnEventCast 不在任何遍历中，RemoveAt 安全）
    private void TryResolve(ulong actorID)
    {
        var flying = _flying;
        var count = flying.Count;
        for (var i = 0; i < count; ++i)
        {
            var f = flying[i];
            if (f.ActorID != actorID)
            {
                continue;
            }

            var arrived = false;
            foreach (var sword in Module.Enemies((uint)OID.DancingSword4))
            {
                if (sword.InstanceID == actorID && !sword.IsDeadOrDestroyed && (sword.Position - f.Destination).LengthSq() <= 1f)
                {
                    arrived = true;
                    break;
                }
            }
            if (arrived || WorldState.CurrentTime > f.CastEnd.AddSeconds(3d)) // 到达落点或兜底超时
            {
                _aoes.RemoveAll(a => a.ActorID == actorID);
                flying.RemoveAt(i);
                return;
            }
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (Param(spell.Action.ID).angle != 0)
        {
            TryResolve(caster.InstanceID); // 读条结束时剑通常未起飞（起点），到达判定由 Update 完成；事件触发时已到达则立即清除
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (Param(spell.Action.ID).angle != 0)
        {
            ++NumCasts;
            TryResolve(caster.InstanceID); // AIE+ 与读条结束同刻（剑未起飞）→ 通常不触发清除，由 Update 到达判定接管
        }
    }
}

// 场地中心弱引导：AI 尽量靠近场地中心（半径 15，权重 0.1，不强制）
sealed class CenterGoal(BossModule module) : BossComponent(module)
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        hints.GoalZones.Add(AIHints.GoalSingleTarget(Module.Arena.Center, 15f, 0.1f));
    }
}
