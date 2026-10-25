// Ported from BossmodRebornCN f6914f3e8. See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// High first board, ARR 2026-09-10/11 (groups 22/23/24/25/26/28/30/31).
// Only confirmed Action/Omen geometry belongs here. CastType 13 has no reliable
// cone angle; 48721/48808/48816 have no documented inner radius yet.
class HighCrucibleCastAOEs(BossModule module, Battle battle) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    // Icefall waves arrive 2.2s apart and coexist in the cast list. Only the
    // first wave is forbidden; persistent ice is handled by its own component.
    protected override double RiskyActivationWindow => battle is Battle.B22 or Battle.B25 or Battle.B29 ? 0.5 : double.PositiveInfinity;
    // Use cast-packet locations for these replay-validated attacks: a moving
    // caster can still be short of the actual endpoint when casting starts.
    protected override AOEConfig? ConfigFor(uint actionID) => (battle, actionID) switch
    {
        (Battle.B22, 48656) => new(new AOEShapeCircle(10f), true), // 掉落 | Range 0
        (Battle.B22, 48665) => new(new AOEShapeCircle(15f), true), // 魔力爆散（魔导书 48664 前置），原来漏画 | Range 0
        (Battle.B23, 48679) => new(new AOEShapeCircle(6f), true), // 酸雾 | Range 0
        (Battle.B23, 48682) => new(new AOEShapeRect(13f, 4f), true), // 藤鞭穿刺 | Range 0
        (Battle.B24, 48693) => new(new AOEShapeCircle(6f), true), // 酸雨 | Range 100
        (Battle.B25, 48697 or 48699) => new(new AOEShapeRect(100f, 7.5f), true), // 冰寒折磨 | Range 0
        (Battle.B25, 48708) => new(new AOEShapeCircle(9f), true), // 无终的枯朽 | Range 100
        (Battle.B25, 48712) => new(new AOEShapeCircle(5f), true), // 寒冰覆盖 | Range 100
        (Battle.B26, 48715) => new(new AOEShapeCircle(6f), true), // Miasma placement; persistent mines have their own lifetime.
        (Battle.B26, 48727) => new(new AOEShapeRect(60f, 3.5f), true), // 荒芜新星 | Range 0
        (Battle.B26, 48729) => new(new AOEShapeCircle(6f), true), // 沥青海详情行 | Range 100
        (Battle.B28, 48750) => new(new AOEShapeRect(5f, 2.5f), true), // 冲击波 | Range 0
        (Battle.B28, 48753) => new(new AOEShapeDonut(5f, 50f), true), // 核心环光 gl_sircle_5005_j2 | Range 0
        (Battle.B28, 48755) => new(new AOEShapeCircle(20f), true), // 平原震裂 | Range 0
        (Battle.B28, 48768) => new(new AOEShapeCone(40f, 30f.Degrees()), true), // 岩突 gl_fan060 | Range 0
        (Battle.B30, 48791) => new(new AOEShapeCircle(5f), true),
        (Battle.B30, 48796) => new(new AOEShapeCone(40f, 10f.Degrees()), true),
        (Battle.B30, 48799) => new(new AOEShapeCone(40f, 90f.Degrees()), true), // 烈火怒骂 gl_fan180 | Range 0
        (Battle.B31, 48812 or 48819) => new(new AOEShapeCircle(6f), true), // 浓烟呛呕 / 蠕动的痰 | Range 100
        (Battle.B31, 48814) => new(new AOEShapeRect(48f, 10f), true), // 低温俯冲 | Range 0
        _ => null
    };

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // The margin affects navigation only, not the displayed client geometry.
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

// 48690 has a 4.7s cast; 48691 is the actual 45x12 breath, with no cast.
// Keep the warning through the visual cast's end, until the damage event (~0.2s later).
sealed class CrucibleRottenStench(BossModule module) : Components.GenericAOEs(module, 48691u)
{
    private static readonly AOEShapeRect Shape = new(45f, 6f);
    private readonly List<AOEInstance> _aoes = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.RemoveAll(a => a.Activation.AddSeconds(2d) < WorldState.CurrentTime);
        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != 48690 || spell.EventHappened)
            return;
        _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        _aoes.Add(new(Shape, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell, 0.2d), actorID: caster.InstanceID));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48690 && spell.Interrupted)
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID); // Interrupted visual
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
            ++NumCasts;
        }
    }

    public override void OnActorDestroyed(Actor actor) => _aoes.RemoveAll(a => a.ActorID == actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// Miasma NPCs exist before their placement and initially share the boss position.
// 48715 is the initial placement blast. Once placed, avoid contact only: the
// R6 explosion is conditional on touching a ball, not a persistent ground AOE.
// 48716 consumes the ball and applies Gradual Panic (5178).
sealed class CrucibleMiasma(BossModule module) : Components.GenericAOEs(module, 48716u, "避开瘴气，防止恐慌叠层！")
{
    public const float ContactRadius = 1.8f; // Navigation clearance, not the explosion radius.
    // A 0.5y grid seals the narrow but walkable gap between a corner ball and
    // the fence. Refine this encounter's grid instead of shrinking the ball.
    private static readonly ArenaBoundsSquare MineNavigation = new(19.7f, mapResolution: 0.2f);
    public ArenaBounds? NavigationBounds => ArmedPositions().Any() ? MineNavigation : null;
    private readonly HashSet<ulong> _armed = [];
    private readonly HashSet<ulong> _resolved = [];

    public override void Update()
    {
        // Tether 426 links placed mines to the boss; recover them on late load.
        foreach (var mine in Module.Enemies(19651))
            if (mine.Tether.ID == 426 && !mine.IsDeadOrDestroyed && !_resolved.Contains(mine.InstanceID))
                _armed.Add(mine.InstanceID);
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => [];

    public IEnumerable<WPos> ArmedPositions() => _armed
        .Select(id => WorldState.Actors.Find(id))
        .Where(source => source is { IsDeadOrDestroyed: false })
        .Select(source => source!.Position);

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (source.OID == 19651 && tether.ID == 426 && !_resolved.Contains(source.InstanceID))
            _armed.Add(source.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster.OID != 19651u)
            return;
        if (spell.Action.ID == 48714)
        {
            _resolved.Remove(caster.InstanceID);
            _armed.Add(caster.InstanceID);
        }
        else if (spell.Action.ID == WatchedAction)
        {
            _resolved.Add(caster.InstanceID);
            _armed.Remove(caster.InstanceID);
            ++NumCasts;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        // 补充布防路径：48714 的命中事件里 payload 施法者是石像鬼本体[19650]，所以上面那条
        // 按 19651 过滤的 OnEventCast 分支实际上永远不会触发；读条结束的施法者才是池子本体[19651]，
        // 且此时已经转移完毕（符合“只在转移后才布防”的要求）。
        if (spell.Action.ID == 48714 && (spell.EventHappened || spell.NPCRemainingTime <= 0.5f) && caster.OID == 19651u)
        {
            _resolved.Remove(caster.InstanceID);
            _armed.Add(caster.InstanceID);
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var position in ArmedPositions())
            hints.TemporaryObstacles.Add(new SDCircle(position, ContactRadius));
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        var armed = false;
        foreach (var position in ArmedPositions())
        {
            armed = true;
            if (actor.Position.InCircle(position, ContactRadius))
            {
                hints.Add(WarningText, true);
                return;
            }
        }
        if (armed)
            hints.Add(WarningText, false);
    }

    public override void OnActorDestroyed(Actor actor) => _armed.Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// EObj 0x1EC0D2 shows its type ~10s before impact. 00010002 -> circle R8;
// 00400080 -> donut 3-11. 00040008/00040100 are their final animation phases.
// All six final casts last only 0.7s, so use EAnim for preparation and reconcile
// against the real cast position/time when available (also supports late loading).
sealed class CrucibleRockRain(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    private const uint RockOID = 0x1EC0D2;
    private static readonly AOEShapeCircle Circle = new(8f);
    private static readonly AOEShapeDonut Donut = new(3f, 11f);
    private readonly Dictionary<ulong, AOEInstance> _predicted = [];
    private readonly HashSet<ulong> _resolved = [];
    private readonly List<AOEInstance> _displayed = [];

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        48757 => new(Circle, true),
        48758 => new(Donut, true),
        _ => null
    };

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        foreach (var aoe in base.ActiveAOEs(slot, actor))
            _displayed.Add(aoe);
        foreach (var (id, aoe) in _predicted.ToArray())
        {
            if (aoe.Activation.AddSeconds(2d) < WorldState.CurrentTime)
                _predicted.Remove(id);
            else if (!_displayed.Any(a => a.Origin.AlmostEqual(aoe.Origin, 0.5f)))
                _displayed.Add(aoe);
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (actor.OID != RockOID || _resolved.Contains(actor.InstanceID))
            return;
        AOEShape? shape = state switch
        {
            0x00010002 or 0x00040008 => Circle,
            0x00400080 or 0x00040100 => Donut,
            _ => null
        };
        if (shape == null)
            return;
        var finalPhase = state is 0x00040008 or 0x00040100;
        var activation = WorldState.FutureTime(finalPhase ? 3.1d : 10d);
        if (_predicted.TryGetValue(actor.InstanceID, out var previous) && previous.Activation < activation)
            activation = previous.Activation; // Duplicate/reordered EAnim must not postpone a warning.
        _predicted[actor.InstanceID] = new(shape, actor.Position, activation: activation, actorID: actor.InstanceID);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (ConfigFor(spell.Action.ID) != null)
            ResolveAt(spell.LocXZ);
        base.OnCastStarted(caster, spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (ConfigFor(spell.Action.ID) != null)
            ResolveAt(caster.Position);
        base.OnEventCast(caster, spell);
    }

    private void ResolveAt(WPos position)
    {
        foreach (var rock in Module.Enemies(RockOID))
            if (rock.Position.AlmostEqual(position, 0.5f))
            {
                _predicted.Remove(rock.InstanceID);
                _resolved.Add(rock.InstanceID);
            }
    }

    public override void OnActorCreated(Actor actor) => _resolved.Remove(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor)
    {
        _predicted.Remove(actor.InstanceID);
        _resolved.Remove(actor.InstanceID);
        base.OnActorDestroyed(actor);
    }

    public override void OnActorEState(Actor actor, ushort state)
    {
        if (actor.OID == RockOID && state == 7)
        {
            _predicted.Remove(actor.InstanceID);
            _resolved.Add(actor.InstanceID);
        }
    }

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

// 48748 starts the two rows of walls. 48751 sweeps the 10y central corridor
// 13s later without a cast. The recorded arena and attack are fixed north/south.
// MapEffects 17..28 enable the twelve 5x5 footprints, in pairs from north to south.
sealed class CrucibleRockWalls(BossModule module) : Components.GenericAOEs(module, 48751u)
{
    private static readonly AOEShapeRect Collapse = new(30f, 5f);
    private static readonly AOEShapeRect Wall = new(2.5f, 2.5f, 2.5f);
    private readonly Dictionary<byte, WPos> _walls = [];
    private AOEInstance? _collapse;
    private ulong _casterID;
    private readonly List<AOEInstance> _aoes = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_collapse is { } expired && expired.Activation.AddSeconds(2d) < WorldState.CurrentTime)
            _collapse = null;
        _aoes.Clear();
        if (_collapse is { } collapse)
            _aoes.Add(collapse);
        foreach (var position in _walls.Values)
            _aoes.Add(new(Wall, position));
        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48748 && !spell.EventHappened)
        {
            _casterID = caster.InstanceID;
            _collapse = new(Collapse, Module.Center - new WDir(0f, 15f), activation: Module.CastFinishAt(spell, 10d));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48748 && spell.Interrupted)
            _collapse = null;
    }

    public override void OnMapEffect(byte index, uint state)
    {
        if (index is < 17 or > 28)
            return;
        if (state == 0x00020001)
        {
            var offset = index - 17;
            _walls[index] = Module.Center + new WDir(offset % 2 == 0 ? 7.5f : -7.5f, -12.5f + 5f * (offset / 2));
        }
        else if (state == 0x00080004)
            _walls.Remove(index);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            _collapse = null;
            _walls.Clear();
            ++NumCasts;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.InstanceID == _casterID)
        {
            _collapse = null;
            _walls.Clear(); // 48751 命中事件由 9020 辅助体发出，可能缺失；施法者消失时一并清掉石墙脚印
        }
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        _ = ActiveAOEs(slot, actor);
        if (_collapse is { } collapse)
            CrucibleScorpionAI.Avoid(hints, collapse);
        foreach (var position in _walls.Values)
            hints.TemporaryObstacles.Add(Wall.Distance(position, default));
    }
}

// Effect payloads reference Knockback rows 3 (20y) and 243 (17y), both away
// from the source. Use the safe field, not the larger electric-fence display bounds.
abstract class HighCrucibleKnockback(BossModule module, uint actionID, float distance) : Components.SimpleKnockbacks(module, actionID, distance)
{
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        // Cast completion can precede the effect packet; keep the landing constraint until impact.
        if (spell.Action.ID == WatchedAction && spell.Interrupted)
            Casters.RemoveAll(kb => kb.ActorID == caster.InstanceID);
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        Casters.RemoveAll(kb => kb.Activation.AddSeconds(2d) < WorldState.CurrentTime);
        return base.ActiveKnockbacks(slot, actor);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction || spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        Casters.RemoveAll(kb => kb.ActorID == caster.InstanceID);
        base.OnCastStarted(caster, spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
            Casters.RemoveAll(kb => kb.ActorID == caster.InstanceID);
        base.OnEventCast(caster, spell);
    }

    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(kb => kb.ActorID == actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

sealed class CrucibleGargoyleKnockback(BossModule module) : HighCrucibleKnockback(module, 48725u, 20f)
{
    private int _predictionStage;

    // The fifth hit arrives 12s after 50695 starts (four ARR sequences).
    // Predict from the opener, then replace it with the final helper cast.
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
            return;
        var (stage, delay) = spell.Action.ID switch
        {
            50695 => (1, 9.5),
            50696 => (2, 9.0),
            48724 => (3, 0.6),
            48725 => (4, 0.0),
            _ => (0, 0.0)
        };
        if (stage == 0)
            return;
        _ = ActiveKnockbacks(0, caster);
        if (Casters.Count > 0 && stage < _predictionStage)
            return;
        var activation = Module.CastFinishAt(spell, delay);
        if (activation <= WorldState.CurrentTime)
            return;
        Casters.Clear();
        Casters.Add(new(spell.LocXZ, Distance, activation, actorID: caster.InstanceID));
        _predictionStage = stage;
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        // Keep the landing constraint through normal cast end until the hit.
        if (spell.Interrupted
            && spell.Action.ID is 50695 or 50696 or 48724 or 48725)
            Casters.Clear();
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48725)
        {
            Casters.Clear();
            _predictionStage = 0;
            ++NumCasts;
        }
        else if (spell.Action.ID == 50696 && Casters.Count == 0)
        {
            Casters.Add(new(caster.Position, Distance, WorldState.FutureTime(9), actorID: caster.InstanceID));
            _predictionStage = 2;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == 19650)
            Casters.Clear();
        else
            base.OnActorDestroyed(actor);
    }

    public override void AddGlobalHints(GlobalHints hints)
    {
        if (ActiveKnockbacks(0, Module.PrimaryActor).Length > 0)
            hints.Add("五连斩最后一击20米击退：提前靠中，避开瘴气与电网！");
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
    {
        if (!pos.InRect(Module.Center, 19.7f, 19.7f))
            return true;
        return Module.FindComponent<CrucibleMiasma>()?.ArmedPositions()
            .Any(p => pos.InCircle(p, CrucibleMiasma.ContactRadius)) ?? false;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var positions = Module.FindComponent<CrucibleMiasma>()?.ArmedPositions().ToArray() ?? [];
        foreach (var kb in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, kb.Activation))
                hints.AddForbiddenConstraint(new SafeRoute(Module.Center, kb.Origin, Distance, positions), kb.Activation.AddSeconds(-1));
    }

    // Check the whole displacement segment so the route cannot clip a ball
    // even if its final landing point is clear. Use contact clearance only.
    private sealed class SafeRoute(WPos center, WPos origin, float distance, WPos[] mines) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            var offset = p - origin;
            var length = offset.Length();
            if (length < 0.5f)
                return 0;
            var direction = offset / length;
            var landing = p + distance * direction;
            if (!landing.InRect(center, 19.7f, 19.7f))
                return 0;
            foreach (var mine in mines)
            {
                var toMine = mine - p;
                var along = Math.Clamp(toMine.X * direction.X + toMine.Z * direction.Z, 0, distance);
                if (mine.InCircle(p + along * direction, CrucibleMiasma.ContactRadius))
                    return 0;
            }
            return 1;
        }
    }
}

sealed class CrucibleGolemKnockback(BossModule module) : HighCrucibleKnockback(module, 50649u, 17f)
{
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InRect(Module.Center, 20f, 15f);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var kb in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, kb.Activation))
                hints.AddForbiddenConstraint(new SDKnockbackInAABBRectAwayFromOrigin(
                    Module.Center, kb.Origin, Distance, 19.7f, 14.7f), kb.Activation.AddSeconds(-0.25d));
    }
}

// 48809/48810 leave EObj 0x1EB704 (b0371). These objects appear before the
// next damage effect and persist; do not clear them when the boss finishes casting.
// Routing reacts immediately; hazards wait 2 seconds except for the fourth confirmed bait.
sealed class CrucibleDragonPoison(BossModule module) : Components.GenericAOEs(module, warningText: "离开毒池！")
{
    private const uint PoisonOID = 0x1EB704;
    private static readonly AOEShapeCircle Shape = new(6f);
    private readonly Dictionary<Actor, DateTime> _activations = [];

    public void ActivateImmediately(Actor pool) => _activations[pool] = WorldState.CurrentTime;

    private void ObservePool(Actor actor)
    {
        if (actor.OID != PoisonOID)
            return;
        if (actor.EventState == 7 || actor.IsDeadOrDestroyed)
            _activations.Remove(actor);
        else
            _activations.TryAdd(actor, WorldState.FutureTime(2));
    }

    public override void Update()
    {
        foreach (var pool in Module.Enemies(PoisonOID))
            ObservePool(pool);
    }

    public override void OnActorCreated(Actor actor) => ObservePool(actor);
    public override void OnActorEventStateChange(Actor actor, byte value) => ObservePool(actor);
    public override void OnActorDeath(Actor actor) => _activations.Remove(actor);
    public override void OnActorDestroyed(Actor actor) => _activations.Remove(actor);

    private readonly List<AOEInstance> _aoes = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        foreach (var (pool, activation) in _activations)
            if (WorldState.CurrentTime >= activation && pool.EventState != 7 && !pool.IsDeadOrDestroyed)
                _aoes.Add(new(Shape, pool.Position, pool.Rotation));
        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Keep waiting pools out of navigation entirely: a future forbidden zone
        // could make the AI leave early instead of following the next bait point.
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            hints.TemporaryObstacles.Add(new SDCircle(aoe.Origin, Shape.Radius));
    }
}

// Landing hits outside first (48816), then the R6 center (48828) ~1.5s later.
// The donut's inner radius is not in the client table. Keep the confirmed center
// preview non-risky until the outer hit, so it doesn't push players out too early.
sealed class CrucibleDragonLanding(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    private DateTime _outerActivation;
    private bool _outerPending;
    private ulong _outerCasterID;
    private WPos _outerOrigin;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID == 48828
        ? new(new AOEShapeCircle(6f), true) : null;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _ = base.ActiveAOEs(slot, actor);
        var waitForOuter = _outerPending && WorldState.CurrentTime <= _outerActivation.AddSeconds(0.5d);
        foreach (var pending in Pending)
            pending.AOE.Risky = !waitForOuter;
        // Base returns value copies, so refresh after changing the pending risk flags.
        return base.ActiveAOEs(slot, actor);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48816 && !spell.EventHappened)
        {
            _outerPending = true;
            _outerCasterID = caster.InstanceID;
            _outerActivation = Module.CastFinishAt(spell);
            _outerOrigin = spell.LocXZ;
        }
        base.OnCastStarted(caster, spell);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48816 && spell.Interrupted)
            _outerPending = false;
        base.OnCastFinished(caster, spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48816)
            _outerPending = false;
        base.OnEventCast(caster, spell);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.InstanceID == _outerCasterID)
            _outerPending = false;
        base.OnActorDestroyed(actor);
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_outerPending && WorldState.CurrentTime <= _outerActivation.AddSeconds(0.5))
        {
            // Conservative inner destination, not a claim about the unknown
            // ring's exact inner edge. Prefer the outside of the inner pocket
            // promptly after the outer damage packet releases this constraint.
            hints.AddForbiddenConstraint(new SDInvertedCircle(_outerOrigin, 5), _outerActivation.AddSeconds(-0.5));
            var origin = _outerOrigin;
            hints.GoalZones.Add(p => Math.Min(5, (p - origin).Length()));
        }
        base.AddAOEForbiddenZones(slot, actor, assignment, hints);
    }
}

// Cast hints cover confirmed non-geometric mechanics without treating raidwides,
// pulls, or unknown donut/cone dimensions as an all-arena forbidden zone.
sealed class CrucibleOwlGustHint(BossModule module) : Components.CastHints(module, [48667u], "暴风：准备击退，注意电网边界！");

// ARR 18:02:48 effect 31 references Knockback row 242: 25y, away from source.
// This exceeds the arena radius; prioritize the feather before cast completion.
sealed class CrucibleOwlGust(BossModule module) : HighCrucibleKnockback(module, 48667u, 25f);

sealed class CrucibleOwlGustPriority(BossModule module) : BossComponent(module)
{
    private IEnumerable<Actor> Sources() => Module.Enemies(19640).Where(a => !a.IsDeadOrDestroyed
        && a.IsTargetable && a.CastInfo is { Action.ID: 48667, EventHappened: false } cast
        && Module.CastFinishAt(cast).AddSeconds(0.5) >= WorldState.CurrentTime);
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var source in Sources())
            if (hints.FindEnemy(source) is { Priority: >= 0 } enemy)
                enemy.Priority = Math.Max(enemy.Priority, 8);
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Sources().Any())
            hints.Add("暴风：25米击退，优先击杀正在读条的羽毛！", true);
    }
}
sealed class CrucibleOwlChapterHint(BossModule module) : Components.CastHints(module, [48657u, 48658u, 48669u], "章节魔法：利用场地效果应对，注意减伤！");
sealed class CrucibleFlowerTrapHint(BossModule module) : Components.CastHints(module, [48683u], "鲜花陷阱：准备吸引与捕食！");
sealed class CrucibleGargoyleSlashHint(BossModule module) : Components.CastHints(module, [48717u], "突进后连续斩击，注意首领朝向！");
sealed class CrucibleGargoyleRingHint(BossModule module) : Components.CastHints(module, [48721u], "钢铁后靠近首领，躲避月环！");
sealed class CrucibleGargoyleKnockbackHint(BossModule module) : Components.CastHints(module, [48725u], "20米击退：靠近中心，注意电网与瘴气！");
sealed class CrucibleGolemKnockbackHint(BossModule module) : Components.CastHints(module, [50649u], "17米击退：调整站位，注意电网！");
sealed class CrucibleGolemSelfDestructHint(BossModule module) : Components.CastHints(module, [48766u], "自爆：优先击杀正在读条的巨像！");
sealed class CrucibleDragonBreathHint(BossModule module) : Components.CastHints(module, [48807u], "猛毒吐息：注意连续毒浪！");
sealed class CrucibleDragonLandingHint(BossModule module) : Components.CastHints(module, [48816u], "空降：先靠近首领，再离开中央圆圈！");
